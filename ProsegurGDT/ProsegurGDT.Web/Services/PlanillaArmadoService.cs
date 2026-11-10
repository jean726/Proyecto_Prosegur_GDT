using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoPlanilla(bool Ok, List<string> Errores, int PlanillaId = 0, bool Creada = false, string? Detalle = null);

public interface IPlanillaArmadoService
{
    Task<List<LotePlanillaFilaVM>> ListarLotesAsync();
    Task<ArmarPlanillaVM?> CargarAsync(int loteId);
    Task<ResultadoPlanilla> GuardarAsync(ArmarPlanillaVM entrada, string usuarioId);
    Task<DetallePlanillaVM?> DetalleAsync(int planillaId);
}

// CU17 — organiza las entregas del lote por zona y define el tipo de repartidor y el orden de visita.
// No cambia el estado de las tarjetas: eso ocurre en CU19.
public class PlanillaArmadoService : IPlanillaArmadoService
{
    private static readonly EstadoTarjeta[] EstadosFraude = { EstadoTarjeta.NoClasificable, EstadoTarjeta.ApartadaPorFraude };

    private readonly ApplicationDbContext _db;
    public PlanillaArmadoService(ApplicationDbContext db) => _db = db;

    private sealed class ResumenLote
    {
        public int Elegibles { get; set; }
        public int SinZona { get; set; }
        public int ConAlerta { get; set; }
        public int Excluidas { get; set; }
        public Dictionary<int, int> PorZona { get; } = new();
    }

    public async Task<List<LotePlanillaFilaVM>> ListarLotesAsync()
    {
        var lotes = await _db.Lotes.Include(l => l.Remito).ThenInclude(r => r!.Banco)
            .Where(l => l.Estado == EstadoLote.IngresadoEnCustodia && _db.Tarjetas.Any(t => t.LoteId == l.Id))
            .OrderByDescending(l => l.Id).ToListAsync();
        var ids = lotes.Select(l => l.Id).ToList();

        var resumenes = await ResumirAsync(ids);
        var planillas = await _db.Planillas.Where(p => ids.Contains(p.LoteId))
            .ToDictionaryAsync(p => p.LoteId, p => new { p.Id, p.FechaGeneracion });
        var conReporte = (await _db.ReportesDensidad.Where(r => ids.Contains(r.LoteId))
            .Select(r => r.LoteId).ToListAsync()).ToHashSet();

        return lotes.Select(l =>
        {
            var r = resumenes[l.Id];
            planillas.TryGetValue(l.Id, out var p);
            var tieneReporte = conReporte.Contains(l.Id);
            return new LotePlanillaFilaVM
            {
                LoteId = l.Id,
                CodigoHojaControl = l.CodigoHojaControl,
                Remito = l.Remito?.Numero,
                Banco = l.Remito?.Banco?.Nombre,
                Tarjetas = r.Elegibles,
                SinZona = r.SinZona,
                ConAlerta = r.ConAlerta,
                Excluidas = r.Excluidas,
                PlanillaId = p?.Id,
                FechaPlanilla = p?.FechaGeneracion,
                TieneReporte = tieneReporte,
                Bloqueo = p is null ? Bloqueo(l, r, tieneReporte) : null
            };
        }).ToList();
    }

    public async Task<ArmarPlanillaVM?> CargarAsync(int loteId)
    {
        var lote = await _db.Lotes.Include(l => l.Remito).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote is null) return null;

        var r = (await ResumirAsync(new[] { loteId }))[loteId];
        var planilla = await _db.Planillas.Include(p => p.Zonas).FirstOrDefaultAsync(p => p.LoteId == loteId);
        var tieneReporte = await _db.ReportesDensidad.AnyAsync(x => x.LoteId == loteId);
        var nombres = await _db.Zonas.Where(z => r.PorZona.Keys.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Nombre);

        var guardadas = planilla?.Zonas.ToDictionary(z => z.ZonaId);
        var zonas = r.PorZona
            .Select(kv =>
            {
                PlanillaZona? g = null;
                guardadas?.TryGetValue(kv.Key, out g);
                return new ZonaPlanillaVM
                {
                    ZonaId = kv.Key,
                    ZonaNombre = nombres.GetValueOrDefault(kv.Key, $"Zona {kv.Key}"),
                    CantidadTarjetas = kv.Value,
                    TipoRepartidorRequerido = g?.TipoRepartidorRequerido ?? TipoRepartidorRequerido.Indiferente,
                    OrdenVisita = g?.OrdenVisita ?? 0,
                    Observacion = g?.Observacion
                };
            })
            .OrderBy(z => z.OrdenVisita == 0 ? int.MaxValue : z.OrdenVisita).ThenBy(z => z.ZonaNombre)
            .ToList();

        // Sin planilla previa: propone un orden consecutivo editable
        if (planilla is null)
            for (var i = 0; i < zonas.Count; i++) zonas[i].OrdenVisita = i + 1;

        return new ArmarPlanillaVM
        {
            LoteId = lote.Id,
            CodigoHojaControl = lote.CodigoHojaControl,
            Remito = lote.Remito?.Numero,
            TotalTarjetas = r.Elegibles,
            SinZona = r.SinZona,
            ConAlerta = r.ConAlerta,
            Excluidas = r.Excluidas,
            PlanillaId = planilla?.Id,
            Observacion = planilla?.Observacion,
            Zonas = zonas,
            Bloqueo = Bloqueo(lote, r, tieneReporte)
        };
    }

    public async Task<ResultadoPlanilla> GuardarAsync(ArmarPlanillaVM entrada, string usuarioId)
    {
        var errores = new List<string>();
        var actual = await CargarAsync(entrada.LoteId);
        if (actual is null) return new(false, new() { "El lote no existe." });
        if (actual.Bloqueo is not null) return new(false, new() { actual.Bloqueo });

        // Las cantidades y las zonas se toman de la base; del formulario solo tipo, orden y observaciones
        var enviadas = entrada.Zonas ?? new();
        if (enviadas.Select(z => z.ZonaId).Distinct().Count() != enviadas.Count
            || !enviadas.Select(z => z.ZonaId).OrderBy(x => x).SequenceEqual(actual.Zonas.Select(z => z.ZonaId).OrderBy(x => x)))
            return new(false, new() { "Las zonas enviadas no coinciden con las del lote. Vuelva a abrir el formulario e intente de nuevo." });

        if (entrada.Observacion is { Length: > 300 })
            errores.Add("La observación general no puede superar 300 caracteres.");

        var porZona = actual.Zonas.ToDictionary(z => z.ZonaId);
        foreach (var z in enviadas)
        {
            var nombre = porZona[z.ZonaId].ZonaNombre;
            if (!Enum.IsDefined(z.TipoRepartidorRequerido))
                errores.Add($"Seleccione un tipo de repartidor válido para la zona {nombre}.");
            if (z.OrdenVisita < 1 || z.OrdenVisita > 999)
                errores.Add($"El orden de visita de la zona {nombre} debe ser un número entre 1 y 999.");
            if (z.Observacion is { Length: > 300 })
                errores.Add($"La observación de la zona {nombre} no puede superar 300 caracteres.");
        }
        foreach (var dup in enviadas.Where(z => z.OrdenVisita >= 1).GroupBy(z => z.OrdenVisita).Where(g => g.Count() > 1))
            errores.Add($"El orden de visita {dup.Key} está repetido en: {string.Join(", ", dup.Select(z => porZona[z.ZonaId].ZonaNombre))}.");
        if (errores.Count > 0) return new(false, errores);

        var planilla = await _db.Planillas.Include(p => p.Zonas).FirstOrDefaultAsync(p => p.LoteId == entrada.LoteId);
        var creada = planilla is null;
        var ahora = DateTime.UtcNow;
        var obs = string.IsNullOrWhiteSpace(entrada.Observacion) ? null : entrada.Observacion.Trim();

        if (planilla is null)
        {
            planilla = new PlanillaArmado { LoteId = entrada.LoteId };
            _db.Planillas.Add(planilla);
        }
        else
        {
            _db.PlanillaZonas.RemoveRange(planilla.Zonas);
            planilla.Zonas.Clear();
        }

        planilla.GeneradaPorId = usuarioId;
        planilla.FechaGeneracion = ahora;
        planilla.Estado = EstadoPlanilla.Generada;
        planilla.Observacion = obs;
        foreach (var z in enviadas.OrderBy(z => z.OrdenVisita))
            planilla.Zonas.Add(new PlanillaZona
            {
                ZonaId = z.ZonaId,
                TipoRepartidorRequerido = z.TipoRepartidorRequerido,
                OrdenVisita = z.OrdenVisita,
                Observacion = string.IsNullOrWhiteSpace(z.Observacion) ? null : z.Observacion.Trim()
            });

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return new(false, new() { "No se pudo guardar la planilla porque otro usuario la modificó al mismo tiempo. Vuelva a abrirla e intente de nuevo." });
        }

        var total = actual.Zonas.Sum(z => z.CantidadTarjetas);
        var detalle = $"Lote {actual.CodigoHojaControl}: {enviadas.Count} zonas, {total} tarjetas. " +
                      string.Join("; ", enviadas.OrderBy(z => z.OrdenVisita)
                          .Select(z => $"{z.OrdenVisita}.{porZona[z.ZonaId].ZonaNombre}({z.TipoRepartidorRequerido})"));
        return new(true, new(), planilla.Id, creada, detalle);
    }

    public async Task<DetallePlanillaVM?> DetalleAsync(int planillaId)
    {
        var p = await _db.Planillas
            .Include(x => x.Lote).ThenInclude(l => l!.Remito)
            .Include(x => x.GeneradaPor)
            .Include(x => x.Zonas).ThenInclude(z => z.Zona)
            .FirstOrDefaultAsync(x => x.Id == planillaId);
        if (p is null) return null;

        // Cantidades vigentes: tarjetas del lote por zona, sin las apartadas por fraude
        var tarjetas = await _db.Tarjetas
            .Where(t => t.LoteId == p.LoteId)
            .Select(t => new { t.Estado, t.ZonaId }).ToListAsync();
        var validas = tarjetas.Where(t => !EstadosFraude.Contains(t.Estado)).ToList();

        return new DetallePlanillaVM
        {
            PlanillaId = p.Id,
            LoteId = p.LoteId,
            CodigoHojaControl = p.Lote?.CodigoHojaControl,
            Remito = p.Lote?.Remito?.Numero,
            GeneradaPor = p.GeneradaPor?.NombreCompleto ?? "—",
            FechaGeneracion = p.FechaGeneracion,
            Estado = p.Estado,
            Observacion = p.Observacion,
            Excluidas = tarjetas.Count - validas.Count,
            TieneReporte = await _db.ReportesDensidad.AnyAsync(r => r.LoteId == p.LoteId),
            Zonas = p.Zonas.OrderBy(z => z.OrdenVisita).Select(z => new ZonaPlanillaVM
            {
                ZonaId = z.ZonaId,
                ZonaNombre = z.Zona?.Nombre ?? $"Zona {z.ZonaId}",
                CantidadTarjetas = validas.Count(t => t.ZonaId == z.ZonaId),
                TipoRepartidorRequerido = z.TipoRepartidorRequerido,
                OrdenVisita = z.OrdenVisita,
                Observacion = z.Observacion
            }).ToList()
        };
    }

    private async Task<Dictionary<int, ResumenLote>> ResumirAsync(IReadOnlyCollection<int> loteIds)
    {
        var resultado = loteIds.ToDictionary(id => id, _ => new ResumenLote());
        if (loteIds.Count == 0) return resultado;

        var tarjetas = await _db.Tarjetas.Where(t => loteIds.Contains(t.LoteId))
            .Select(t => new { t.Id, t.LoteId, t.Estado, t.ZonaId }).ToListAsync();
        var conAlerta = (await _db.Alertas
            .Where(a => a.Decision == DecisionAlerta.Pendiente && loteIds.Contains(a.Tarjeta!.LoteId))
            .Select(a => a.TarjetaId).Distinct().ToListAsync()).ToHashSet();

        foreach (var t in tarjetas)
        {
            var r = resultado[t.LoteId];
            if (EstadosFraude.Contains(t.Estado)) { r.Excluidas++; continue; }
            if (!ReglasClasificacion.Elegibles.Contains(t.Estado)) continue;

            r.Elegibles++;
            if (conAlerta.Contains(t.Id)) r.ConAlerta++;
            if (t.ZonaId is null) { r.SinZona++; continue; }
            r.PorZona[t.ZonaId.Value] = r.PorZona.GetValueOrDefault(t.ZonaId.Value) + 1;
        }
        return resultado;
    }

    private static string? Bloqueo(Lote lote, ResumenLote r, bool tieneReporte)
    {
        if (lote.Estado != EstadoLote.IngresadoEnCustodia)
            return "El lote no está en custodia, por lo que no se puede armar su planilla.";
        if (tieneReporte)
            return "El lote ya tiene reporte de densidad; la planilla ya no puede modificarse.";
        if (r.Elegibles == 0)
            return "El lote no tiene tarjetas disponibles para planificar.";

        var motivos = new List<string>();
        if (r.SinZona > 0)
            motivos.Add($"Existen {r.SinZona} tarjeta(s) sin zona asignada. Complete la clasificación por zona antes de armar la planilla.");
        if (r.ConAlerta > 0)
            motivos.Add($"Existen {r.ConAlerta} tarjeta(s) con alerta de dirección pendiente. El Buscador debe resolverlas antes de armar la planilla.");
        return motivos.Count == 0 ? null : string.Join(" ", motivos);
    }
}
