using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoFaltante(bool Ok, string? Error, string? Detalle = null);

public interface IFaltantesService
{
    Task<List<FaltanteFilaVM>> ListarAsync();
    Task<LocalizarFaltanteVM?> DetalleAsync(int incidenciaId);
    Task<ResultadoFaltante> LocalizarAsync(int incidenciaId, string? ubicacion, string usuarioId);
    Task<ResultadoFaltante> NoLocalizarAsync(int incidenciaId, string usuarioId);
}

// CU21 — El Buscador localiza las tarjetas faltantes detectadas en la conciliación (CU20).
// Localizada: la tarjeta vuelve a custodia y se registra su ubicación en bóveda.
// No localizada: la tarjeta queda como extraviada con indicios de fraude.
// Solo se gestionan las incidencias de tipo Faltante de la conciliación vigente (la última) de cada lote.
public class FaltantesService : IFaltantesService
{
    private readonly ApplicationDbContext _db;
    private readonly ITarjetaEstadoService _estados;

    public FaltantesService(ApplicationDbContext db, ITarjetaEstadoService estados)
    { _db = db; _estados = estados; }

    private IQueryable<IncidenciaConciliacion> Vigentes() =>
        _db.IncidenciasConciliacion.Where(i => i.Tipo == TipoIncidencia.Faltante
            && i.ConciliacionLoteId == _db.Conciliaciones.Where(c => c.LoteId == i.ConciliacionLote!.LoteId).Max(c => c.Id));

    public async Task<List<FaltanteFilaVM>> ListarAsync()
    {
        var lista = await Vigentes()
            .Select(i => new
            {
                i.Id,
                Codigo = i.Tarjeta!.Codigo,
                Cliente = i.Tarjeta.Cliente!.NombreCompleto,
                Hoja = i.ConciliacionLote!.Lote!.CodigoHojaControl,
                Remito = i.ConciliacionLote.Lote.Remito!.Numero,
                i.Estado,
                i.Ubicacion
            }).ToListAsync();

        return lista.OrderBy(i => i.Estado).ThenBy(i => i.Codigo).Select(i => new FaltanteFilaVM
        {
            IncidenciaId = i.Id, Codigo = i.Codigo, Cliente = i.Cliente, LoteHoja = i.Hoja,
            Remito = i.Remito, Estado = i.Estado, Ubicacion = i.Ubicacion
        }).ToList();
    }

    public async Task<LocalizarFaltanteVM?> DetalleAsync(int incidenciaId)
    {
        var i = await _db.IncidenciasConciliacion
            .Include(x => x.Tarjeta).ThenInclude(t => t!.Cliente)
            .Include(x => x.ConciliacionLote).ThenInclude(c => c!.Lote).ThenInclude(l => l!.Remito).ThenInclude(r => r!.Banco)
            .FirstOrDefaultAsync(x => x.Id == incidenciaId && x.Tipo == TipoIncidencia.Faltante);
        if (i?.Tarjeta is null) return null;

        var inventario = await _db.InventarioBoveda.FirstOrDefaultAsync(b => b.TarjetaId == i.TarjetaId);
        var historial = await _db.HistorialEstados.Where(h => h.TarjetaId == i.TarjetaId)
            .OrderByDescending(h => h.Id).Take(10).ToListAsync();
        var lote = i.ConciliacionLote?.Lote;

        return new LocalizarFaltanteVM
        {
            IncidenciaId = i.Id,
            Codigo = i.Tarjeta.Codigo,
            Cliente = i.Tarjeta.Cliente?.NombreCompleto ?? "—",
            LoteHoja = lote?.CodigoHojaControl,
            Remito = lote?.Remito?.Numero,
            Banco = lote?.Remito?.Banco?.Nombre,
            EstadoTarjeta = i.Tarjeta.Estado,
            Estado = i.Estado,
            UbicacionRegistrada = i.Ubicacion,
            UbicacionBoveda = inventario?.Ubicacion,
            EstadoFisicoBoveda = inventario?.EstadoFisico,
            Historial = historial.Select(h => new HistorialFaltanteVM
            {
                FechaHora = h.FechaHora, Estado = h.EstadoNuevo.TextoEstado(), Observacion = h.Observacion
            }).ToList()
        };
    }

    public async Task<ResultadoFaltante> LocalizarAsync(int incidenciaId, string? ubicacion, string usuarioId)
    {
        ubicacion = ubicacion?.Trim();
        if (string.IsNullOrEmpty(ubicacion)) return new(false, "Ingrese la ubicación donde se encontró la tarjeta.");
        if (ubicacion.Length > 120) return new(false, "La ubicación no puede superar 120 caracteres.");

        var (inc, error) = await CargarAbiertaAsync(incidenciaId);
        if (inc is null) return new(false, error);

        try { _estados.Cambiar(inc.Tarjeta!, EstadoTarjeta.EnCustodia, usuarioId, $"Tarjeta faltante localizada en: {ubicacion}"); }
        catch (InvalidOperationException ex) { return new(false, ex.Message); }

        inc.Estado = EstadoIncidencia.Localizada;
        inc.BuscadorId = usuarioId;
        inc.Ubicacion = ubicacion;
        inc.FechaResolucion = DateTime.UtcNow;

        var inv = await _db.InventarioBoveda.FirstOrDefaultAsync(b => b.TarjetaId == inc.TarjetaId);
        if (inv is null)
            _db.InventarioBoveda.Add(new InventarioBoveda { TarjetaId = inc.TarjetaId!.Value, Ubicacion = ubicacion, EstadoFisico = "En custodia", FechaActualizacion = DateTime.UtcNow });
        else
        { inv.Ubicacion = ubicacion; inv.EstadoFisico = "En custodia"; inv.FechaActualizacion = DateTime.UtcNow; }

        await _db.SaveChangesAsync();
        return new(true, null, $"Incidencia {inc.Id}: tarjeta {inc.Tarjeta!.Codigo} localizada en {ubicacion}");
    }

    public async Task<ResultadoFaltante> NoLocalizarAsync(int incidenciaId, string usuarioId)
    {
        var (inc, error) = await CargarAbiertaAsync(incidenciaId);
        if (inc is null) return new(false, error);

        try { _estados.Cambiar(inc.Tarjeta!, EstadoTarjeta.ExtraviadaConIndiciosDeFraude, usuarioId, "Tarjeta faltante no localizada tras la búsqueda"); }
        catch (InvalidOperationException ex) { return new(false, ex.Message); }

        inc.Estado = EstadoIncidencia.NoLocalizada;
        inc.BuscadorId = usuarioId;
        inc.FechaResolucion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new(true, null, $"Incidencia {inc.Id}: tarjeta {inc.Tarjeta!.Codigo} no localizada");
    }

    private async Task<(IncidenciaConciliacion? Inc, string? Error)> CargarAbiertaAsync(int id)
    {
        var inc = await _db.IncidenciasConciliacion.Include(i => i.Tarjeta).Include(i => i.ConciliacionLote)
            .FirstOrDefaultAsync(i => i.Id == id && i.Tipo == TipoIncidencia.Faltante);
        if (inc?.Tarjeta is null) return (null, "La incidencia no existe.");
        if (inc.Estado != EstadoIncidencia.Abierta) return (null, "La incidencia ya fue resuelta.");
        var ultima = await _db.Conciliaciones.Where(c => c.LoteId == inc.ConciliacionLote!.LoteId).MaxAsync(c => c.Id);
        if (inc.ConciliacionLoteId != ultima) return (null, "La incidencia pertenece a una conciliación anterior del lote.");
        return (inc, null);
    }
}
