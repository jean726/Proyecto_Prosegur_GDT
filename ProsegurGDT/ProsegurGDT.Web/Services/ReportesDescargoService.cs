using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoReporteDescargo(bool Ok, string Mensaje, int ReporteId = 0, string? Detalle = null);

public interface IReportesDescargoService
{
    Task<ReportesDescargoIndexVM> ListarAsync();
    Task<ResultadoReporteDescargo> GenerarAsync(DateOnly fecha, TipoReporteDescargo tipo, string usuarioId);
    Task<DetalleReporteDescargoVM?> DetalleAsync(int id);
    Task<ResultadoReporteDescargo> DerivarAlBancoAsync(int id, string usuarioId);
}

// CU10 — reporte de entregas del día; CU13 — reporte de descargo consolidado; CU14 — derivación al banco emisor.
// RN-12: solo el Descargador los genera, al cierre de la jornada, cuando todas las rutas están descargadas y en orden:
// primero el reporte de entregas y después el reporte de descargo.
public class ReportesDescargoService : IReportesDescargoService
{
    private readonly ApplicationDbContext _db;
    public ReportesDescargoService(ApplicationDbContext db) => _db = db;

    private sealed record RutaJornada(HojaRuta Hoja, DescargoRuta? Descargo, List<Tarjeta> Tarjetas);

    public async Task<ReportesDescargoIndexVM> ListarAsync()
    {
        var hojas = await HojasAsync();
        var ids = hojas.Select(h => h.Id).ToList();
        var descargos = await _db.DescargosRuta.AsNoTracking().Where(d => ids.Contains(d.HojaRutaId)).ToDictionaryAsync(d => d.HojaRutaId);
        var reportes = await _db.ReportesDescargo.AsNoTracking().ToListAsync();

        var jornadas = hojas.GroupBy(h => DescargoReglas.Jornada(h)).OrderByDescending(g => g.Key).Select(g =>
        {
            var ds = g.Select(h => descargos.GetValueOrDefault(h.Id)).ToList();
            var ent = reportes.FirstOrDefault(r => r.FechaJornada == g.Key && r.Tipo == TipoReporteDescargo.Entregas);
            var des = reportes.FirstOrDefault(r => r.FechaJornada == g.Key && r.Tipo == TipoReporteDescargo.Descargo);
            return new JornadaDescargoVM
            {
                Fecha = g.Key,
                Rutas = g.Count(),
                Cerradas = ds.Count(d => d?.Estado == EstadoDescargoRuta.Cerrada),
                Organizadas = ds.Count(d => d?.DocumentacionOrganizada == true),
                ReporteEntregasId = ent?.Id,
                ReporteDescargoId = des?.Id,
                EstadoDescargo = des?.Estado
            };
        }).ToList();
        return new ReportesDescargoIndexVM { Jornadas = jornadas };
    }

    public async Task<ResultadoReporteDescargo> GenerarAsync(DateOnly fecha, TipoReporteDescargo tipo, string usuarioId)
    {
        if (await _db.ReportesDescargo.AnyAsync(r => r.FechaJornada == fecha && r.Tipo == tipo))
            return new(false, "Ese reporte ya fue generado para la jornada.");

        var rutas = await RutasAsync(fecha);
        if (rutas.Count == 0) return new(false, "No hay hojas de ruta de esa jornada disponibles para el descargo.");
        var abiertas = rutas.Count(r => r.Descargo?.Estado != EstadoDescargoRuta.Cerrada);
        if (abiertas > 0)
            return new(false, $"Aún hay {abiertas} ruta(s) sin cerrar. Los reportes se generan al cierre de la jornada, con todas las rutas descargadas (RN-12).");

        if (tipo == TipoReporteDescargo.Descargo)
        {
            if (!await _db.ReportesDescargo.AnyAsync(r => r.FechaJornada == fecha && r.Tipo == TipoReporteDescargo.Entregas))
                return new(false, "Genere primero el reporte de entregas de la jornada (RN-12).");
            var sinMotivo = await SinMotivoAsync(rutas);
            if (sinMotivo.Count > 0)
                return new(false, "Faltan motivos de no entrega en: " + string.Join(", ", sinMotivo) + ". Regístrelos antes del reporte de descargo (RN-11).");
        }

        var lineas = rutas.OrderBy(r => r.Hoja.AsignacionRuta!.OrdenVisita).Select(r => new ReporteDescargoLinea
        {
            DescargoRutaId = r.Descargo!.Id,
            RepartidorId = r.Hoja.AsignacionRuta!.RepartidorId,
            Sector = r.Hoja.AsignacionRuta.SectorCobertura,
            Asignadas = r.Tarjetas.Count,
            Entregadas = r.Tarjetas.Count(t => t.Estado == EstadoTarjeta.Entregada),
            Devueltas = r.Tarjetas.Count(t => t.Estado == EstadoTarjeta.Devuelta),
            EnCustodia = r.Tarjetas.Count(t => t.Estado == EstadoTarjeta.EnCustodia),
            Incidencias = r.Tarjetas.Count(t => t.Estado == EstadoTarjeta.ConIncidencia || t.Estado == EstadoTarjeta.ExtraviadaConIndiciosDeFraude),
            Fraude = r.Tarjetas.Count(t => t.Estado == EstadoTarjeta.ApartadaPorFraude)
        }).ToList();

        var reporte = new ReporteDescargo
        {
            FechaJornada = fecha, Tipo = tipo, Estado = EstadoReporteDescargo.Generado,
            GeneradoPorId = usuarioId, FechaGeneracion = DateTime.UtcNow,
            Asignadas = lineas.Sum(l => l.Asignadas), Entregadas = lineas.Sum(l => l.Entregadas),
            Devueltas = lineas.Sum(l => l.Devueltas), EnCustodia = lineas.Sum(l => l.EnCustodia),
            Incidencias = lineas.Sum(l => l.Incidencias), Fraude = lineas.Sum(l => l.Fraude)
        };
        foreach (var l in lineas) reporte.Lineas.Add(l);
        _db.ReportesDescargo.Add(reporte);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return new(false, "Otro usuario generó este reporte al mismo tiempo."); }

        var nombre = tipo == TipoReporteDescargo.Entregas ? "Reporte de entregas" : "Reporte de descargo";
        return new(true, $"{nombre} de la jornada {fecha:dd/MM/yyyy} generado.", reporte.Id,
            $"{nombre} {reporte.Id} jornada {fecha:dd/MM/yyyy}: {reporte.Entregadas} entregadas, {reporte.Devueltas} devueltas, {reporte.EnCustodia} en custodia, {reporte.Incidencias} incidencias, {reporte.Fraude} fraude");
    }

    public async Task<DetalleReporteDescargoVM?> DetalleAsync(int id)
    {
        var r = await _db.ReportesDescargo.AsNoTracking()
            .Include(x => x.GeneradoPor).Include(x => x.DerivadoPor)
            .Include(x => x.Lineas).ThenInclude(l => l.Repartidor)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return null;

        var vm = new DetalleReporteDescargoVM
        {
            Id = r.Id, Fecha = r.FechaJornada, Tipo = r.Tipo, Estado = r.Estado,
            GeneradoPor = r.GeneradoPor?.NombreCompleto ?? "—", FechaGeneracion = r.FechaGeneracion,
            Asignadas = r.Asignadas, Entregadas = r.Entregadas, Devueltas = r.Devueltas,
            EnCustodia = r.EnCustodia, Incidencias = r.Incidencias, Fraude = r.Fraude,
            BancoDestino = r.BancoDestino, DerivadoPor = r.DerivadoPor?.NombreCompleto, FechaDerivacion = r.FechaDerivacion,
            Lineas = r.Lineas.OrderBy(l => l.Repartidor!.Nombre).Select(l => new LineaReporteDescargoVM
            {
                Repartidor = l.Repartidor?.Nombre ?? "—", Sector = l.Sector ?? "—", Asignadas = l.Asignadas,
                Entregadas = l.Entregadas, Devueltas = l.Devueltas, EnCustodia = l.EnCustodia, Incidencias = l.Incidencias, Fraude = l.Fraude
            }).ToList()
        };

        if (r.Tipo == TipoReporteDescargo.Descargo)
        {
            var rutaIds = r.Lineas.Select(l => l.DescargoRutaId).ToList();
            var dts = await _db.DescargosTarjeta.AsNoTracking()
                .Where(x => rutaIds.Contains(x.DescargoRutaId) && x.Resultado != null && x.Resultado != ResultadoDescargo.Entregada)
                .Include(x => x.Tarjeta).ThenInclude(t => t!.Cliente).OrderBy(x => x.Tarjeta!.Codigo).ToListAsync();
            vm.NoEntregadas = dts.Select(x => new NoEntregadaVM
            {
                Codigo = x.Tarjeta?.Codigo ?? "—", Cliente = x.Tarjeta?.Cliente?.NombreCompleto ?? "—",
                Resultado = x.Resultado!.Value.Texto(), Motivo = x.Motivo?.Texto() ?? "—", Detalle = x.Detalle
            }).ToList();
        }
        return vm;
    }

    public async Task<ResultadoReporteDescargo> DerivarAlBancoAsync(int id, string usuarioId)
    {
        var r = await _db.ReportesDescargo.Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return new(false, "El reporte no existe.");
        if (r.Tipo != TipoReporteDescargo.Descargo) return new(false, "Solo se deriva al banco el reporte de descargo consolidado.");
        if (r.Estado == EstadoReporteDescargo.Derivado) return new(false, "El reporte ya fue derivado al banco emisor.");

        var rutaIds = r.Lineas.Select(l => l.DescargoRutaId).ToList();
        var bancos = await _db.DescargosRuta.Where(d => rutaIds.Contains(d.Id))
            .Select(d => d.HojaRuta!.AsignacionRuta!.HojaAsignacion!.Lote!.Remito!.Banco!.Nombre).Distinct().ToListAsync();
        var destino = bancos.Count == 0 ? "Banco emisor" : string.Join(", ", bancos.OrderBy(b => b));

        r.Estado = EstadoReporteDescargo.Derivado;
        r.BancoDestino = destino.Length > 200 ? destino[..200] : destino;
        r.DerivadoPorId = usuarioId;
        r.FechaDerivacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new(true, $"Reporte de descargo derivado a {r.BancoDestino} para la activación de las tarjetas entregadas.", id,
            $"Reporte de descargo {id} derivado a {r.BancoDestino}: {r.Entregadas} entregadas");
    }

    // ---------- Apoyo ----------

    private async Task<List<HojaRuta>> HojasAsync() =>
        await DescargoReglas.Elegibles(_db).AsNoTracking()
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion)
            .AsSplitQuery().ToListAsync();

    private async Task<List<RutaJornada>> RutasAsync(DateOnly fecha)
    {
        var hojas = (await HojasAsync()).Where(h => DescargoReglas.Jornada(h) == fecha).ToList();
        var hojaIds = hojas.Select(h => h.Id).ToList();
        var rutaIds = hojas.Select(h => h.AsignacionRutaId).ToList();
        var descargos = await _db.DescargosRuta.AsNoTracking().Where(d => hojaIds.Contains(d.HojaRutaId)).ToDictionaryAsync(d => d.HojaRutaId);
        var pares = await _db.AsignacionesTarjeta.AsNoTracking().Where(a => rutaIds.Contains(a.AsignacionRutaId))
            .Select(a => new { a.AsignacionRutaId, a.Tarjeta }).ToListAsync();
        var porRuta = pares.GroupBy(p => p.AsignacionRutaId).ToDictionary(g => g.Key, g => g.Select(p => p.Tarjeta!).ToList());
        return hojas.Select(h => new RutaJornada(h, descargos.GetValueOrDefault(h.Id), porRuta.GetValueOrDefault(h.AsignacionRutaId) ?? new())).ToList();
    }

    // RN-11: cada tarjeta no entregada (devuelta, en custodia o con incidencia) debe tener su motivo registrado
    private async Task<List<string>> SinMotivoAsync(List<RutaJornada> rutas)
    {
        var estadosNoEntrega = new[] { EstadoTarjeta.Devuelta, EstadoTarjeta.EnCustodia, EstadoTarjeta.ConIncidencia };
        var candidatas = rutas.SelectMany(r => r.Tarjetas).Where(t => estadosNoEntrega.Contains(t.Estado)).ToList();
        var ids = candidatas.Select(t => t.Id).ToList();
        var conMotivo = (await _db.DescargosTarjeta.AsNoTracking().Where(d => ids.Contains(d.TarjetaId) && d.Motivo != null)
            .Select(d => d.TarjetaId).ToListAsync()).ToHashSet();
        return candidatas.Where(t => !conMotivo.Contains(t.Id)).Select(t => t.Codigo).OrderBy(c => c).ToList();
    }
}
