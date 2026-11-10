using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoReporteEntregas(bool Ok, string Mensaje, int ReporteId = 0, string? Detalle = null);

public interface IReporteEntregasService
{
    Task<ReportesEntregasIndexVM> ListarAsync();
    Task<ResultadoReporteEntregas> CrearAsync(DateOnly fecha, string usuarioId);
    Task<DetalleReporteEntregasVM?> DetalleAsync(int id);
    Task<ResultadoReporteEntregas> ActualizarAsync(int id, string? observacion);
    Task<ResultadoReporteEntregas> RemitirAsync(int id, string usuarioId);
}

// CU07 — El Supervisor diseña el reporte de entregas de la jornada integrando tarjetas asignadas, entregadas,
// devueltas e incidencias (RF10). CU08 — lo remite al área de Descargo al cierre de la jornada (RF11).
// Los resultados (entregada/devuelta/incidencia) los registra el Descargador (Fase 9); mientras tanto
// el reporte refleja el estado actual de cada tarjeta y puede recalcularse hasta que se remite.
public class ReporteEntregasService : IReporteEntregasService
{
    private readonly ApplicationDbContext _db;
    public ReporteEntregasService(ApplicationDbContext db) => _db = db;

    private static readonly EstadoHojaRuta[] Listas = { EstadoHojaRuta.Aprobada, EstadoHojaRuta.Firmada };
    private static readonly EstadoTarjeta[] EstadosIncidencia =
        { EstadoTarjeta.ConIncidencia, EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.ExtraviadaConIndiciosDeFraude };

    private sealed record HojaJornada(HojaRuta Hoja, DateOnly Fecha);

    public async Task<ReportesEntregasIndexVM> ListarAsync()
    {
        var reportes = await _db.ReportesEntregas.AsNoTracking().OrderByDescending(r => r.FechaJornada).ToListAsync();
        var conReporte = reportes.Select(r => r.FechaJornada).ToHashSet();

        var hojas = await HojasPorJornadaAsync(null);
        var jornadas = hojas.GroupBy(h => h.Fecha)
            .Where(g => !conReporte.Contains(g.Key))
            .Select(g => new JornadaDisponibleVM
            {
                Fecha = g.Key,
                HojasListas = g.Count(h => Listas.Contains(h.Hoja.Estado)),
                HojasPendientes = g.Count(h => !Listas.Contains(h.Hoja.Estado))
            })
            .Where(j => j.HojasListas > 0)
            .OrderByDescending(j => j.Fecha).ToList();

        return new ReportesEntregasIndexVM
        {
            Reportes = reportes.Select(r => new ReporteEntregasFilaVM
            {
                Id = r.Id, Fecha = r.FechaJornada, Estado = r.Estado,
                Asignadas = r.Asignadas, Entregadas = r.Entregadas, Devueltas = r.Devueltas, Incidencias = r.Incidencias,
                FechaRemision = r.FechaRemision
            }).ToList(),
            Jornadas = jornadas
        };
    }

    public async Task<ResultadoReporteEntregas> CrearAsync(DateOnly fecha, string usuarioId)
    {
        if (await _db.ReportesEntregas.AnyAsync(r => r.FechaJornada == fecha))
            return new(false, "Ya existe un reporte de entregas para esa jornada.");

        var lineas = await CalcularAsync(fecha);
        if (lineas.Count == 0)
            return new(false, "No hay hojas de ruta aprobadas en esa jornada. Apruebe las hojas de ruta antes de diseñar el reporte.");

        var ahora = DateTime.UtcNow;
        var reporte = new ReporteEntregas
        {
            FechaJornada = fecha,
            Estado = EstadoReporteEntregas.Borrador,
            GeneradoPorId = usuarioId,
            FechaGeneracion = ahora,
            FechaActualizacion = ahora
        };
        Aplicar(reporte, lineas);
        _db.ReportesEntregas.Add(reporte);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return new(false, "Otro usuario creó el reporte de esa jornada al mismo tiempo."); }

        return new(true, "Reporte de entregas creado.", reporte.Id,
            $"Reporte {reporte.Id} jornada {fecha:dd/MM/yyyy}: {reporte.Asignadas} asignadas, {reporte.Entregadas} entregadas, {reporte.Devueltas} devueltas, {reporte.Incidencias} incidencias");
    }

    public async Task<DetalleReporteEntregasVM?> DetalleAsync(int id)
    {
        var r = await _db.ReportesEntregas.AsNoTracking()
            .Include(x => x.GeneradoPor).Include(x => x.RemitidoPor)
            .Include(x => x.Lineas).ThenInclude(l => l.Repartidor)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return null;

        var pendientes = r.Estado == EstadoReporteEntregas.Borrador
            ? (await HojasPorJornadaAsync(r.FechaJornada)).Count(h => !Listas.Contains(h.Hoja.Estado))
            : 0;

        return new DetalleReporteEntregasVM
        {
            Id = r.Id, Fecha = r.FechaJornada, Estado = r.Estado,
            GeneradoPor = r.GeneradoPor?.NombreCompleto ?? "—",
            FechaGeneracion = r.FechaGeneracion, FechaActualizacion = r.FechaActualizacion,
            Observacion = r.Observacion,
            Asignadas = r.Asignadas, Entregadas = r.Entregadas, Devueltas = r.Devueltas, Incidencias = r.Incidencias,
            RemitidoPor = r.RemitidoPor?.NombreCompleto, FechaRemision = r.FechaRemision,
            HojasPendientesJornada = pendientes,
            Lineas = r.Lineas.OrderBy(l => l.Repartidor!.Nombre).Select(l => new LineaReporteEntregasVM
            {
                HojaRutaId = l.HojaRutaId, Repartidor = l.Repartidor?.Nombre ?? "—", Sector = l.Sector ?? "—",
                Asignadas = l.Asignadas, Entregadas = l.Entregadas, Devueltas = l.Devueltas, Incidencias = l.Incidencias
            }).ToList()
        };
    }

    // Recalcula los totales con el estado actual de las tarjetas y guarda la observación del Supervisor
    public async Task<ResultadoReporteEntregas> ActualizarAsync(int id, string? observacion)
    {
        var r = await _db.ReportesEntregas.Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return new(false, "El reporte no existe.");
        if (r.Estado == EstadoReporteEntregas.Remitido) return new(false, "El reporte ya fue remitido y no puede modificarse.");

        var lineas = await CalcularAsync(r.FechaJornada);
        if (lineas.Count == 0) return new(false, "La jornada ya no tiene hojas de ruta aprobadas.");

        _db.ReportesEntregasLineas.RemoveRange(r.Lineas);
        r.Lineas.Clear();
        Aplicar(r, lineas);
        r.Observacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim();
        r.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new(true, "Reporte actualizado con los datos más recientes.", id,
            $"Reporte {id} recalculado: {r.Asignadas} asignadas, {r.Entregadas} entregadas, {r.Devueltas} devueltas, {r.Incidencias} incidencias");
    }

    public async Task<ResultadoReporteEntregas> RemitirAsync(int id, string usuarioId)
    {
        var r = await _db.ReportesEntregas.Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return new(false, "El reporte no existe.");
        if (r.Estado == EstadoReporteEntregas.Remitido) return new(false, "El reporte ya fue remitido al área de Descargo.");

        // Se congela con los datos al momento del cierre
        var lineas = await CalcularAsync(r.FechaJornada);
        if (lineas.Count == 0) return new(false, "La jornada no tiene hojas de ruta aprobadas; no hay nada que remitir.");
        _db.ReportesEntregasLineas.RemoveRange(r.Lineas);
        r.Lineas.Clear();
        Aplicar(r, lineas);

        var ahora = DateTime.UtcNow;
        r.Estado = EstadoReporteEntregas.Remitido;
        r.RemitidoPorId = usuarioId;
        r.FechaRemision = ahora;
        r.FechaActualizacion = ahora;
        await _db.SaveChangesAsync();
        return new(true, "Reporte de entregas remitido al área de Descargo.", id,
            $"Reporte {id} jornada {r.FechaJornada:dd/MM/yyyy} remitido a Descargo: {r.Asignadas} asignadas, {r.Entregadas} entregadas, {r.Devueltas} devueltas, {r.Incidencias} incidencias");
    }

    // ---------- Apoyo ----------

    private static void Aplicar(ReporteEntregas r, List<ReporteEntregasLinea> lineas)
    {
        foreach (var l in lineas) r.Lineas.Add(l);
        r.Asignadas = lineas.Sum(l => l.Asignadas);
        r.Entregadas = lineas.Sum(l => l.Entregadas);
        r.Devueltas = lineas.Sum(l => l.Devueltas);
        r.Incidencias = lineas.Sum(l => l.Incidencias);
    }

    // Todas las hojas de ruta de una jornada (fecha de emisión de la hoja de asignación, hora de Lima)
    private async Task<List<HojaJornada>> HojasPorJornadaAsync(DateOnly? fecha)
    {
        var q = _db.HojasRuta.AsNoTracking()
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion)
            .AsQueryable();
        if (fecha is not null)
        {
            var desde = fecha.Value.ToDateTime(TimeOnly.MinValue).AddDays(-1);
            var hasta = fecha.Value.ToDateTime(TimeOnly.MinValue).AddDays(2);
            q = q.Where(h => h.AsignacionRuta!.HojaAsignacion!.FechaHoraEmision >= desde
                          && h.AsignacionRuta.HojaAsignacion.FechaHoraEmision < hasta);
        }
        var hojas = await q.ToListAsync();
        var res = hojas.Select(h => new HojaJornada(h,
            DateOnly.FromDateTime(HoraLima.Convertir(h.AsignacionRuta!.HojaAsignacion!.FechaHoraEmision)))).ToList();
        return fecha is null ? res : res.Where(h => h.Fecha == fecha.Value).ToList();
    }

    private async Task<List<ReporteEntregasLinea>> CalcularAsync(DateOnly fecha)
    {
        var hojas = (await HojasPorJornadaAsync(fecha)).Where(h => Listas.Contains(h.Hoja.Estado)).Select(h => h.Hoja).ToList();
        if (hojas.Count == 0) return new();

        var rutaIds = hojas.Select(h => h.AsignacionRutaId).ToList();
        var estados = await _db.AsignacionesTarjeta.AsNoTracking()
            .Where(a => rutaIds.Contains(a.AsignacionRutaId))
            .Select(a => new { a.AsignacionRutaId, a.Tarjeta!.Estado }).ToListAsync();
        var porRuta = estados.GroupBy(e => e.AsignacionRutaId).ToDictionary(g => g.Key, g => g.Select(x => x.Estado).ToList());

        return hojas.Select(h =>
        {
            var lista = porRuta.GetValueOrDefault(h.AsignacionRutaId) ?? new List<EstadoTarjeta>();
            return new ReporteEntregasLinea
            {
                HojaRutaId = h.Id,
                RepartidorId = h.AsignacionRuta!.RepartidorId,
                Sector = h.AsignacionRuta.SectorCobertura,
                Asignadas = lista.Count,
                Entregadas = lista.Count(e => e == EstadoTarjeta.Entregada),
                Devueltas = lista.Count(e => e == EstadoTarjeta.Devuelta),
                Incidencias = lista.Count(e => EstadosIncidencia.Contains(e))
            };
        }).ToList();
    }
}
