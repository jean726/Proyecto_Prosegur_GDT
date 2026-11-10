using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoDensidad(bool Ok, string? Error, int ReporteId = 0, string? Detalle = null);

public interface IDensidadService
{
    Task<List<LoteDensidadFilaVM>> ListarAsync();
    Task<ReporteDensidadVM?> VerAsync(int loteId);
    Task<ResultadoDensidad> GenerarAsync(int loteId, string usuarioId);
    Task<ReporteDensidadVM?> ObtenerAsync(int reporteId);
}

// CU18 — Reporte de densidad de entregas por zona. Es una foto (snapshot) de la planilla de CU17;
// el Analista de Rutas lo marca como «Revisado» en CU19 (RN-22).
public class DensidadService : IDensidadService
{
    private readonly ApplicationDbContext _db;
    private readonly IPlanillaArmadoService _planillas;

    public DensidadService(ApplicationDbContext db, IPlanillaArmadoService planillas)
    { _db = db; _planillas = planillas; }

    public async Task<List<LoteDensidadFilaVM>> ListarAsync()
    {
        var planillas = await _db.Planillas
            .Include(p => p.Lote).ThenInclude(l => l!.Remito).ThenInclude(r => r!.Banco)
            .Include(p => p.Zonas)
            .Where(p => p.Estado == EstadoPlanilla.Generada)
            .OrderByDescending(p => p.LoteId).ToListAsync();
        var ids = planillas.Select(p => p.LoteId).ToList();
        var reportes = await _db.ReportesDensidad.Where(r => ids.Contains(r.LoteId))
            .ToDictionaryAsync(r => r.LoteId);

        return planillas.Select(p =>
        {
            reportes.TryGetValue(p.LoteId, out var r);
            return new LoteDensidadFilaVM
            {
                LoteId = p.LoteId,
                CodigoHojaControl = p.Lote?.CodigoHojaControl,
                Remito = p.Lote?.Remito?.Numero,
                Banco = p.Lote?.Remito?.Banco?.Nombre,
                ZonasPlanilla = p.Zonas.Count,
                ReporteId = r?.Id,
                EstadoReporte = r?.Estado,
                FechaReporte = r?.FechaGeneracion
            };
        }).ToList();
    }

    public async Task<ReporteDensidadVM?> VerAsync(int loteId)
    {
        var existente = await _db.ReportesDensidad.Where(r => r.LoteId == loteId).Select(r => r.Id).FirstOrDefaultAsync();
        if (existente != 0) return await ObtenerAsync(existente);

        var lote = await _db.Lotes.Include(l => l.Remito).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote is null) return null;

        var (zonas, bloqueo) = await CalcularAsync(loteId);
        return new ReporteDensidadVM
        {
            LoteId = loteId,
            CodigoHojaControl = lote.CodigoHojaControl,
            Remito = lote.Remito?.Numero,
            Zonas = zonas,
            Bloqueo = bloqueo
        };
    }

    public async Task<ReporteDensidadVM?> ObtenerAsync(int reporteId)
    {
        var r = await _db.ReportesDensidad
            .Include(x => x.Lote).ThenInclude(l => l!.Remito)
            .Include(x => x.GeneradoPor).Include(x => x.RevisadoPor)
            .Include(x => x.Zonas).ThenInclude(z => z.Zona)
            .FirstOrDefaultAsync(x => x.Id == reporteId);
        if (r is null) return null;

        return new ReporteDensidadVM
        {
            LoteId = r.LoteId,
            CodigoHojaControl = r.Lote?.CodigoHojaControl,
            Remito = r.Lote?.Remito?.Numero,
            ReporteId = r.Id,
            Estado = r.Estado,
            GeneradoPor = r.GeneradoPor?.NombreCompleto ?? "—",
            FechaGeneracion = r.FechaGeneracion,
            RevisadoPor = r.RevisadoPor?.NombreCompleto,
            FechaRevision = r.FechaRevision,
            Zonas = r.Zonas.Select(z => new ZonaDensidadVM
            {
                ZonaId = z.ZonaId,
                ZonaNombre = z.Zona?.Nombre ?? $"Zona {z.ZonaId}",
                CantidadTarjetas = z.CantidadTarjetas,
                CantidadNoClasificables = z.CantidadNoClasificables,
                TieneAlerta = z.TieneAlerta,
                TipoRepartidorRequerido = z.TipoRepartidorRequerido
            }).OrderByDescending(z => z.CantidadTarjetas).ThenBy(z => z.ZonaNombre).ToList()
        };
    }

    public async Task<ResultadoDensidad> GenerarAsync(int loteId, string usuarioId)
    {
        if (await _db.ReportesDensidad.AnyAsync(r => r.LoteId == loteId))
            return new(false, "El lote ya tiene un reporte de densidad generado.");

        var (zonas, bloqueo) = await CalcularAsync(loteId);
        if (bloqueo is not null) return new(false, bloqueo);

        var planillaId = await _db.Planillas.Where(p => p.LoteId == loteId).Select(p => p.Id).FirstAsync();
        var reporte = new ReporteDensidad
        {
            LoteId = loteId,
            PlanillaArmadoId = planillaId,
            GeneradoPorId = usuarioId,
            FechaGeneracion = DateTime.UtcNow,
            Estado = EstadoReporteDensidad.Generado
        };
        foreach (var z in zonas)
            reporte.Zonas.Add(new ReporteDensidadZona
            {
                ZonaId = z.ZonaId,
                CantidadTarjetas = z.CantidadTarjetas,
                CantidadNoClasificables = z.CantidadNoClasificables,
                TieneAlerta = z.TieneAlerta,
                TipoRepartidorRequerido = z.TipoRepartidorRequerido
            });
        _db.ReportesDensidad.Add(reporte);

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return new(false, "No se pudo generar el reporte porque otro usuario lo generó al mismo tiempo. Vuelva a abrir el lote.");
        }

        var detalle = $"Lote {loteId}: {zonas.Count} zonas, {zonas.Sum(z => z.CantidadTarjetas)} tarjetas, " +
                      $"{zonas.Sum(z => z.CantidadNoClasificables)} no clasificables, {zonas.Count(z => z.TieneAlerta)} zonas con alerta";
        return new(true, null, reporte.Id, detalle);
    }

    // Calcula las filas del reporte a partir de la planilla vigente. Devuelve el motivo si aún no se puede.
    private async Task<(List<ZonaDensidadVM> Zonas, string? Bloqueo)> CalcularAsync(int loteId)
    {
        var planilla = await _db.Planillas.Include(p => p.Zonas)
            .FirstOrDefaultAsync(p => p.LoteId == loteId && p.Estado == EstadoPlanilla.Generada);
        if (planilla is null)
            return (new(), "El lote aún no tiene una planilla de armado generada. Genérela en «Planilla de armado» antes del reporte de densidad.");

        var actual = await _planillas.CargarAsync(loteId);
        if (actual is null) return (new(), "El lote no existe.");
        if (actual.Bloqueo is not null) return (new(), actual.Bloqueo);

        var enPlanilla = planilla.Zonas.Select(z => z.ZonaId).OrderBy(x => x).ToList();
        var enLote = actual.Zonas.Select(z => z.ZonaId).OrderBy(x => x).ToList();
        if (!enPlanilla.SequenceEqual(enLote))
            return (new(), "La planilla no coincide con la clasificación actual del lote. Modifíquela en «Planilla de armado» antes de generar el reporte.");

        var noClasificables = await _db.Tarjetas
            .Where(t => t.LoteId == loteId && t.Estado == EstadoTarjeta.NoClasificable && t.ZonaId != null)
            .GroupBy(t => t.ZonaId!.Value).Select(g => new { ZonaId = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.ZonaId, x => x.N);
        // RN-19: se marcan las zonas donde el lote tuvo alertas del registro de direcciones fraudulentas
        var conAlerta = (await _db.Alertas
            .Where(a => a.Tarjeta!.LoteId == loteId && a.Tarjeta.ZonaId != null)
            .Select(a => a.Tarjeta!.ZonaId!.Value).Distinct().ToListAsync()).ToHashSet();

        var porZona = planilla.Zonas.ToDictionary(z => z.ZonaId);
        var zonas = actual.Zonas.Select(z => new ZonaDensidadVM
        {
            ZonaId = z.ZonaId,
            ZonaNombre = z.ZonaNombre,
            CantidadTarjetas = z.CantidadTarjetas,
            CantidadNoClasificables = noClasificables.GetValueOrDefault(z.ZonaId),
            TieneAlerta = conAlerta.Contains(z.ZonaId),
            TipoRepartidorRequerido = porZona[z.ZonaId].TipoRepartidorRequerido
        }).OrderByDescending(z => z.CantidadTarjetas).ThenBy(z => z.ZonaNombre).ToList();

        return (zonas, null);
    }
}
