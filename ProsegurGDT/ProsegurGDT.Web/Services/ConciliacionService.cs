using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoConciliacion(bool Ok, List<string> Errores, int ConciliacionId = 0, string? Detalle = null);

public interface IConciliacionService
{
    Task<List<LoteConciliacionFilaVM>> ListarAsync();
    Task<VerificarConciliacionVM?> CargarAsync(int loteId, string? codigosEntrada = null);
    Task<ResultadoConciliacion> VerificarAsync(int loteId, string? codigosFisicos, string usuarioId);
    Task<DetalleConciliacionVM?> DetalleAsync(int conciliacionId);
}

// CU20 — El Clasificador cruza las tarjetas físicas con la ficha, la planilla de armado y la hoja de
// asignación (RN-24). Detecta faltantes, duplicados y discrepancias y registra una incidencia por cada
// una; el Buscador las resuelve en CU21. Esta verificación no cambia el estado de ninguna tarjeta.
// Cada verificación crea un registro nuevo: la última es la vigente y se puede repetir mientras tenga incidencias.
public class ConciliacionService : IConciliacionService
{
    private const int MaxCodigos = 5000;
    private readonly ApplicationDbContext _db;

    public ConciliacionService(ApplicationDbContext db) { _db = db; }

    public async Task<List<LoteConciliacionFilaVM>> ListarAsync()
    {
        var hojas = await _db.HojasAsignacion
            .Include(h => h.Lote).ThenInclude(l => l!.Remito).ThenInclude(r => r!.Banco)
            .Where(h => h.Estado == EstadoHojaAsignacion.Emitida)
            .OrderByDescending(h => h.LoteId).ToListAsync();
        var ids = hojas.Select(h => h.LoteId).ToList();
        var todas = await _db.Conciliaciones.Where(c => ids.Contains(c.LoteId))
            .Select(c => new { c.Id, c.LoteId, c.Estado, c.FechaVerificacion, Incidencias = c.Incidencias.Count })
            .OrderBy(c => c.Id).ToListAsync();
        var ultimas = todas.GroupBy(c => c.LoteId).ToDictionary(g => g.Key, g => g.Last());

        return hojas.Select(h =>
        {
            ultimas.TryGetValue(h.LoteId, out var c);
            return new LoteConciliacionFilaVM
            {
                LoteId = h.LoteId,
                CodigoHojaControl = h.Lote?.CodigoHojaControl,
                Remito = h.Lote?.Remito?.Numero,
                Banco = h.Lote?.Remito?.Banco?.Nombre,
                HojaId = h.Id,
                TotalHoja = h.TotalTarjetas,
                ConciliacionId = c?.Id,
                Estado = c?.Estado,
                FechaVerificacion = c?.FechaVerificacion,
                Incidencias = c?.Incidencias ?? 0
            };
        }).ToList();
    }

    public async Task<VerificarConciliacionVM?> CargarAsync(int loteId, string? codigosEntrada = null)
    {
        var ctx = await CargarContextoAsync(loteId);
        if (ctx is null) return null;
        return new VerificarConciliacionVM
        {
            LoteId = loteId,
            CodigoHojaControl = ctx.Lote.CodigoHojaControl,
            Remito = ctx.Lote.Remito?.Numero,
            Banco = ctx.Lote.Remito?.Banco?.Nombre,
            CodigosFisicos = codigosEntrada,
            CantidadFicha = ctx.CantidadFicha,
            CantidadPlanilla = ctx.CantidadPlanilla,
            CantidadHoja = ctx.Hoja?.TotalTarjetas ?? 0,
            CodigosEsperados = ctx.Esperadas.Select(t => t.Codigo).OrderBy(c => c, StringComparer.Ordinal).ToList(),
            Bloqueo = Bloqueo(ctx)
        };
    }

    public async Task<ResultadoConciliacion> VerificarAsync(int loteId, string? codigosFisicos, string usuarioId)
    {
        var ctx = await CargarContextoAsync(loteId);
        if (ctx is null) return Fallo("El lote no existe.");
        var bloqueo = Bloqueo(ctx);
        if (bloqueo is not null) return Fallo(bloqueo);

        var leidos = (codigosFisicos ?? string.Empty)
            .Split(new[] { '\r', '\n', ';', ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .ToList();
        if (leidos.Count == 0) return Fallo("Ingrese los códigos de las tarjetas contadas físicamente.");
        if (leidos.Count > MaxCodigos) return Fallo($"No se pueden verificar más de {MaxCodigos} códigos a la vez.");

        var distintos = leidos.Distinct().ToList();
        var conocidas = await _db.Tarjetas.Where(t => distintos.Contains(t.Codigo)).ToListAsync();
        var porCodigo = conocidas.ToDictionary(t => t.Codigo.ToUpperInvariant(), t => t);

        var desconocidos = distintos.Where(c => !porCodigo.ContainsKey(c)).ToList();
        if (desconocidos.Count > 0)
            return Fallo("Los siguientes códigos no existen en el sistema: " + string.Join(", ", desconocidos.Take(10)) +
                         (desconocidos.Count > 10 ? $" y {desconocidos.Count - 10} más" : "") + ". Corríjalos e intente de nuevo.");

        var esperadas = ctx.Esperadas.Select(t => t.Id).ToHashSet();
        var conteo = leidos.GroupBy(c => porCodigo[c].Id).ToDictionary(g => g.Key, g => g.Count());

        var faltantes = esperadas.Where(id => !conteo.ContainsKey(id)).ToList();
        var duplicados = conteo.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
        var discrepancias = conteo.Keys.Where(id => !esperadas.Contains(id)).ToList();

        var conciliacion = new ConciliacionLote
        {
            LoteId = loteId,
            HojaAsignacionId = ctx.Hoja!.Id,
            VerificadaPorId = usuarioId,
            FechaVerificacion = DateTime.UtcNow,
            CantidadFicha = ctx.CantidadFicha,
            CantidadPlanilla = ctx.CantidadPlanilla,
            CantidadHoja = ctx.Hoja.TotalTarjetas,
            CantidadFisica = leidos.Count,
            Faltantes = faltantes.Count,
            Duplicados = duplicados.Count,
            Estado = faltantes.Count + duplicados.Count + discrepancias.Count == 0
                ? EstadoConciliacion.Consistente : EstadoConciliacion.ConFaltantes
        };
        foreach (var id in faltantes) conciliacion.Incidencias.Add(Nueva(TipoIncidencia.Faltante, id));
        foreach (var id in duplicados) conciliacion.Incidencias.Add(Nueva(TipoIncidencia.Duplicado, id));
        foreach (var id in discrepancias) conciliacion.Incidencias.Add(Nueva(TipoIncidencia.Discrepancia, id));
        _db.Conciliaciones.Add(conciliacion);
        await _db.SaveChangesAsync();

        var detalle = $"Conciliación {conciliacion.Id} lote {ctx.Lote.CodigoHojaControl}: {conciliacion.Estado}; ficha {conciliacion.CantidadFicha}, " +
                      $"planilla {conciliacion.CantidadPlanilla}, hoja {conciliacion.CantidadHoja}, física {conciliacion.CantidadFisica}; " +
                      $"{faltantes.Count} faltantes, {duplicados.Count} duplicados, {discrepancias.Count} discrepancias";
        return new(true, new(), conciliacion.Id, detalle);
    }

    public async Task<DetalleConciliacionVM?> DetalleAsync(int conciliacionId)
    {
        var c = await _db.Conciliaciones
            .Include(x => x.Lote).ThenInclude(l => l!.Remito).ThenInclude(r => r!.Banco)
            .Include(x => x.VerificadaPor)
            .Include(x => x.Incidencias).ThenInclude(i => i.Tarjeta).ThenInclude(t => t!.Cliente)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == conciliacionId);
        if (c is null) return null;

        var ultimaId = await _db.Conciliaciones.Where(x => x.LoteId == c.LoteId).MaxAsync(x => x.Id);
        return new DetalleConciliacionVM
        {
            ConciliacionId = c.Id,
            LoteId = c.LoteId,
            CodigoHojaControl = c.Lote?.CodigoHojaControl,
            Remito = c.Lote?.Remito?.Numero,
            Banco = c.Lote?.Remito?.Banco?.Nombre,
            HojaId = c.HojaAsignacionId,
            VerificadaPor = c.VerificadaPor?.NombreCompleto ?? "—",
            FechaVerificacion = c.FechaVerificacion,
            CantidadFicha = c.CantidadFicha,
            CantidadPlanilla = c.CantidadPlanilla,
            CantidadHoja = c.CantidadHoja,
            CantidadFisica = c.CantidadFisica,
            Faltantes = c.Faltantes,
            Duplicados = c.Duplicados,
            Discrepancias = c.Incidencias.Count(i => i.Tipo == TipoIncidencia.Discrepancia),
            Estado = c.Estado,
            EsUltima = c.Id == ultimaId,
            Incidencias = c.Incidencias.OrderBy(i => i.Tipo).ThenBy(i => i.Tarjeta?.Codigo).Select(i => new IncidenciaConciliacionVM
            {
                Codigo = i.Tarjeta?.Codigo ?? "—",
                Cliente = i.Tarjeta?.Cliente?.NombreCompleto ?? "—",
                Tipo = i.Tipo,
                Estado = i.Estado,
                Ubicacion = i.Ubicacion
            }).ToList()
        };
    }

    // ---------- Apoyo ----------

    private sealed class Contexto
    {
        public Lote Lote { get; set; } = null!;
        public HojaAsignacion? Hoja { get; set; }
        public ConciliacionLote? Ultima { get; set; }
        public int CantidadFicha { get; set; }
        public int CantidadPlanilla { get; set; }
        public List<Tarjeta> Esperadas { get; set; } = new();
    }

    private static ResultadoConciliacion Fallo(string mensaje) => new(false, new() { mensaje });

    private static IncidenciaConciliacion Nueva(TipoIncidencia tipo, int tarjetaId) =>
        new() { TarjetaId = tarjetaId, Tipo = tipo, Estado = EstadoIncidencia.Abierta };

    private static string? Bloqueo(Contexto c)
    {
        if (c.Hoja is null)
            return "El lote aún no tiene hoja de asignación. Debe generarla el Analista de Rutas antes de conciliar.";
        if (c.Ultima?.Estado == EstadoConciliacion.Consistente)
            return "El lote ya fue conciliado y es consistente.";
        return null;
    }

    private async Task<Contexto?> CargarContextoAsync(int loteId)
    {
        var lote = await _db.Lotes.Include(l => l.Remito).ThenInclude(r => r!.Banco).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote is null) return null;

        var c = new Contexto { Lote = lote };
        c.Hoja = await _db.HojasAsignacion.FirstOrDefaultAsync(h => h.LoteId == loteId && h.Estado == EstadoHojaAsignacion.Emitida);
        c.Ultima = await _db.Conciliaciones.Where(x => x.LoteId == loteId).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        c.CantidadFicha = await _db.Tarjetas.CountAsync(t => t.LoteId == loteId);
        c.CantidadPlanilla = await _db.ReportesDensidad.Where(r => r.LoteId == loteId)
            .SelectMany(r => r.Zonas).SumAsync(z => (int?)z.CantidadTarjetas) ?? 0;
        if (c.Hoja is not null)
            c.Esperadas = await _db.AsignacionesTarjeta
                .Where(a => a.AsignacionRuta!.HojaAsignacionId == c.Hoja.Id)
                .Select(a => a.Tarjeta!).ToListAsync();
        return c;
    }
}
