using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public interface IDefraudacionService
{
    Task<DefraudacionIndexVM> ListarAsync();
    Task<ResultadoDescargoOp> RegistrarAsync(RegistrarActaVM entrada, string usuarioId);
    Task<ResultadoDescargoOp> IngresarABovedaAsync(int actaId, string ubicacion, string usuarioId);
    Task<List<InventarioFilaVM>> InventarioAsync();
}

// CU12 — El Descargador registra en el acta de reporte de fraude las tarjetas con discrepancia no resuelta (CU09) o
// reportadas por el Verificador (CU06); el Encargado de Bóveda las ingresa a custodia especial con etiqueta de alerta
// y las diferencia del inventario regular (RN-15, RF16, RF17).
public class DefraudacionService : IDefraudacionService
{
    private readonly ApplicationDbContext _db;
    private readonly ITarjetaEstadoService _estados;

    public DefraudacionService(ApplicationDbContext db, ITarjetaEstadoService estados)
    { _db = db; _estados = estados; }

    public const string EstadoFisicoFraude = "Fraude · etiqueta especial";

    public async Task<DefraudacionIndexVM> ListarAsync()
    {
        var conActa = (await _db.ActasFraude.Select(a => a.TarjetaId).ToListAsync()).ToHashSet();

        var noResueltas = await _db.DescargosTarjeta.AsNoTracking()
            .Where(x => x.Verificacion == VerificacionDescargo.DiscrepanciaNoResuelta)
            .Include(x => x.Tarjeta).ThenInclude(t => t!.Cliente).ToListAsync();
        var reportes = await _db.ReportesFraude.AsNoTracking()
            .Where(r => r.Estado == EstadoReporteFraude.Generado)
            .Include(r => r.Tarjeta).ThenInclude(t => t!.Cliente).ToListAsync();

        var vm = new DefraudacionIndexVM();
        vm.Candidatos.AddRange(noResueltas.Where(x => !conActa.Contains(x.TarjetaId)).Select(x => new CandidatoFraudeVM
        {
            TarjetaId = x.TarjetaId, Codigo = x.Tarjeta!.Codigo, Cliente = x.Tarjeta.Cliente?.NombreCompleto ?? "—",
            Origen = OrigenFraude.DiscrepanciaDeDescargo,
            Detalle = $"Receptor {x.ReceptorNombre} ({x.ReceptorDocumento}) no coincide con el titular. Buscador: {x.ObservacionBuscador}"
        }));
        vm.Candidatos.AddRange(reportes.Where(r => !conActa.Contains(r.TarjetaId)).Select(r => new CandidatoFraudeVM
        {
            TarjetaId = r.TarjetaId, Codigo = r.Tarjeta!.Codigo, Cliente = r.Tarjeta.Cliente?.NombreCompleto ?? "—",
            Origen = OrigenFraude.ReporteDelVerificador, ReporteFraudeId = r.Id,
            Detalle = $"{r.Tipo.Texto()}: {r.Descripcion}"
        }));

        var actas = await _db.ActasFraude.AsNoTracking()
            .Include(a => a.Tarjeta).ThenInclude(t => t!.Cliente)
            .OrderBy(a => a.Estado).ThenByDescending(a => a.Id).ToListAsync();
        vm.Actas = actas.Select(Fila).ToList();
        return vm;
    }

    public async Task<ResultadoDescargoOp> RegistrarAsync(RegistrarActaVM e, string usuarioId)
    {
        if (await _db.ActasFraude.AnyAsync(a => a.TarjetaId == e.TarjetaId))
            return new(false, "La tarjeta ya figura en el acta de fraude.");
        var tarjeta = await _db.Tarjetas.Include(t => t.Cliente).FirstOrDefaultAsync(t => t.Id == e.TarjetaId);
        if (tarjeta is null) return new(false, "La tarjeta no existe.");

        string descripcion;
        ReporteFraude? reporte = null;
        if (e.Origen == OrigenFraude.DiscrepanciaDeDescargo)
        {
            var dt = await _db.DescargosTarjeta.FirstOrDefaultAsync(x => x.TarjetaId == e.TarjetaId && x.Verificacion == VerificacionDescargo.DiscrepanciaNoResuelta);
            if (dt is null) return new(false, "La tarjeta no tiene una discrepancia no resuelta por el Buscador.");
            descripcion = $"Discrepancia de identidad no resuelta: receptor {dt.ReceptorNombre} ({dt.ReceptorDocumento}) frente al titular {tarjeta.Cliente?.NombreCompleto} ({tarjeta.Cliente?.DocumentoIdentidad}). Verificación del Buscador: {dt.ObservacionBuscador}";
        }
        else
        {
            reporte = await _db.ReportesFraude.FirstOrDefaultAsync(r => r.Id == e.ReporteFraudeId && r.TarjetaId == e.TarjetaId && r.Estado == EstadoReporteFraude.Generado);
            if (reporte is null) return new(false, "El reporte de fraude no existe o ya fue incorporado.");
            descripcion = $"Reporte del Verificador ({reporte.Tipo.Texto()}): {reporte.Descripcion}";
        }

        try { _estados.Cambiar(tarjeta, EstadoTarjeta.ApartadaPorFraude, usuarioId, "Apartada por fraude (acta de reporte de fraude)"); }
        catch (InvalidOperationException)
        {
            return new(false, $"La tarjeta {tarjeta.Codigo} está «{tarjeta.Estado.TextoEstado()}» y no puede apartarse por fraude desde ese estado.");
        }

        _db.ActasFraude.Add(new ActaFraude
        {
            TarjetaId = tarjeta.Id,
            Origen = e.Origen,
            ReporteFraudeId = reporte?.Id,
            Descripcion = descripcion.Length > 500 ? descripcion[..500] : descripcion,
            DescargadorId = usuarioId,
            FechaRegistro = DateTime.UtcNow,
            Estado = EstadoActaFraude.Registrada
        });
        if (reporte is not null) reporte.Estado = EstadoReporteFraude.RecibidoPorDescargo;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return new(false, "La tarjeta ya figura en el acta de fraude."); }
        return new(true, $"Tarjeta {tarjeta.Codigo} registrada en el acta de fraude y apartada del flujo normal.",
            $"Acta de fraude: tarjeta {tarjeta.Codigo} ({e.Origen.Texto()})");
    }

    public async Task<ResultadoDescargoOp> IngresarABovedaAsync(int actaId, string ubicacion, string usuarioId)
    {
        var a = await _db.ActasFraude.Include(x => x.Tarjeta).FirstOrDefaultAsync(x => x.Id == actaId);
        if (a is null) return new(false, "El acta no existe.");
        if (a.Estado == EstadoActaFraude.IngresadaABoveda) return new(false, "La tarjeta ya ingresó a la bóveda.");

        var ahora = DateTime.UtcNow;
        var inv = await _db.InventarioBoveda.FirstOrDefaultAsync(i => i.TarjetaId == a.TarjetaId);
        if (inv is null)
            _db.InventarioBoveda.Add(new InventarioBoveda { TarjetaId = a.TarjetaId, Ubicacion = ubicacion.Trim(), EstadoFisico = EstadoFisicoFraude, FechaActualizacion = ahora });
        else { inv.Ubicacion = ubicacion.Trim(); inv.EstadoFisico = EstadoFisicoFraude; inv.FechaActualizacion = ahora; }

        a.Estado = EstadoActaFraude.IngresadaABoveda;
        a.BovedaPorId = usuarioId;
        a.FechaIngreso = ahora;
        a.UbicacionBoveda = ubicacion.Trim();
        await _db.SaveChangesAsync();
        return new(true, $"Tarjeta {a.Tarjeta?.Codigo} ingresada a custodia especial con etiqueta de fraude.",
            $"Bóveda: tarjeta {a.Tarjeta?.Codigo} en {a.UbicacionBoveda} (fraude)");
    }

    public async Task<List<InventarioFilaVM>> InventarioAsync()
    {
        var lista = await _db.InventarioBoveda.AsNoTracking()
            .Include(i => i.Tarjeta).ThenInclude(t => t!.Cliente)
            .OrderByDescending(i => i.EstadoFisico == EstadoFisicoFraude).ThenBy(i => i.Ubicacion).ThenBy(i => i.Tarjeta!.Codigo).ToListAsync();
        return lista.Select(i => new InventarioFilaVM
        {
            Codigo = i.Tarjeta?.Codigo ?? "—", Cliente = i.Tarjeta?.Cliente?.NombreCompleto ?? "—",
            Ubicacion = i.Ubicacion, EstadoFisico = i.EstadoFisico, FechaActualizacion = i.FechaActualizacion
        }).ToList();
    }

    private static ActaFraudeFilaVM Fila(ActaFraude a) => new()
    {
        Id = a.Id, Codigo = a.Tarjeta?.Codigo ?? "—", Cliente = a.Tarjeta?.Cliente?.NombreCompleto ?? "—",
        Origen = a.Origen, Descripcion = a.Descripcion, Estado = a.Estado, FechaRegistro = a.FechaRegistro,
        Ubicacion = a.UbicacionBoveda, FechaIngreso = a.FechaIngreso
    };
}
