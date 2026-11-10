using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoFraude(bool Ok, string Mensaje, int ReporteId = 0, string? Detalle = null);

public interface IFraudeService
{
    Task<FraudeIndexVM> ListarAsync();
    Task<RevisarHojaVM?> RevisarAsync(int hojaRutaId);
    Task<NuevoReporteFraudeVM?> NuevoAsync(int hojaRutaId, int tarjetaId);
    Task<ResultadoFraude> CrearAsync(NuevoReporteFraudeVM entrada, string verificadorId);
    Task<DetalleReporteFraudeVM?> DetalleAsync(int id);
}

// CU06 — El Verificador revisa las hojas de ruta aprobadas y genera un reporte de fraude cuando detecta
// anomalías en el cargo de entrega (RF09, RN-08). El reporte queda disponible para Descargo (CU12).
// RN-10: el estado de la tarjeta lo cambia solo el Descargador, por eso aquí no se modifica.
public class FraudeService : IFraudeService
{
    private readonly ApplicationDbContext _db;
    public FraudeService(ApplicationDbContext db) => _db = db;

    private static readonly EstadoHojaRuta[] Revisables = { EstadoHojaRuta.Aprobada, EstadoHojaRuta.Firmada };

    public async Task<FraudeIndexVM> ListarAsync()
    {
        var hojas = await _db.HojasRuta.AsNoTracking()
            .Where(h => Revisables.Contains(h.Estado))
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion).ThenInclude(a => a!.Lote)
            .OrderByDescending(h => h.Id).ToListAsync();
        var reportesPorHoja = await _db.ReportesFraude.GroupBy(r => r.HojaRutaId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        var reportes = await _db.ReportesFraude.AsNoTracking()
            .Include(r => r.Tarjeta).ThenInclude(t => t!.Cliente)
            .Include(r => r.HojaRuta).ThenInclude(h => h!.AsignacionRuta).ThenInclude(a => a!.Repartidor)
            .OrderByDescending(r => r.Id).ToListAsync();

        return new FraudeIndexVM
        {
            Hojas = hojas.Select(h => new HojaRevisionFilaVM
            {
                HojaRutaId = h.Id,
                CodigoHojaControl = h.AsignacionRuta?.HojaAsignacion?.Lote?.CodigoHojaControl,
                Repartidor = h.AsignacionRuta?.Repartidor?.Nombre ?? "—",
                Sector = h.AsignacionRuta?.SectorCobertura ?? "—",
                Tarjetas = h.AsignacionRuta?.CantidadTarjetas ?? 0,
                Reportes = reportesPorHoja.GetValueOrDefault(h.Id),
                Estado = h.Estado
            }).ToList(),
            Reportes = reportes.Select(r => new ReporteFraudeFilaVM
            {
                Id = r.Id,
                Codigo = r.Tarjeta?.Codigo ?? "—",
                Cliente = r.Tarjeta?.Cliente?.NombreCompleto ?? "—",
                Repartidor = r.HojaRuta?.AsignacionRuta?.Repartidor?.Nombre ?? "—",
                Tipo = r.Tipo,
                Estado = r.Estado,
                FechaHora = r.FechaHora
            }).ToList()
        };
    }

    public async Task<RevisarHojaVM?> RevisarAsync(int hojaRutaId)
    {
        var h = await CargarHojaAsync(hojaRutaId);
        if (h is null || !Revisables.Contains(h.Estado)) return null;
        var ruta = h.AsignacionRuta!;

        var tarjetas = await _db.Tarjetas.AsNoTracking()
            .Where(t => _db.AsignacionesTarjeta.Any(a => a.AsignacionRutaId == ruta.Id && a.TarjetaId == t.Id))
            .Include(t => t.Cliente).OrderBy(t => t.Codigo).ToListAsync();
        var ids = tarjetas.Select(t => t.Id).ToList();
        var reportes = await _db.ReportesFraude.Where(r => ids.Contains(r.TarjetaId))
            .ToDictionaryAsync(r => r.TarjetaId, r => r.Id);

        return new RevisarHojaVM
        {
            HojaRutaId = h.Id,
            CodigoHojaControl = ruta.HojaAsignacion?.Lote?.CodigoHojaControl,
            Repartidor = ruta.Repartidor?.Nombre ?? "—",
            Sector = ruta.SectorCobertura ?? "—",
            Estado = h.Estado,
            Tarjetas = tarjetas.Select(t => new TarjetaRevisionVM
            {
                TarjetaId = t.Id,
                Codigo = t.Codigo,
                Cliente = t.Cliente?.NombreCompleto ?? "—",
                Direccion = t.Cliente?.Direccion ?? "—",
                Estado = t.Estado,
                ReporteId = reportes.TryGetValue(t.Id, out var rid) ? rid : null
            }).ToList()
        };
    }

    public async Task<NuevoReporteFraudeVM?> NuevoAsync(int hojaRutaId, int tarjetaId)
    {
        var h = await CargarHojaAsync(hojaRutaId);
        if (h is null || !Revisables.Contains(h.Estado)) return null;
        var asignada = await _db.AsignacionesTarjeta.AnyAsync(a => a.AsignacionRutaId == h.AsignacionRutaId && a.TarjetaId == tarjetaId);
        if (!asignada) return null;

        var t = await _db.Tarjetas.AsNoTracking().Include(x => x.Cliente).FirstAsync(x => x.Id == tarjetaId);
        var cita = (await _db.Citas.AsNoTracking().Where(c => c.TarjetaId == tarjetaId && c.Vigente).ToListAsync())
            .OrderByDescending(c => c.Id).FirstOrDefault();
        var ruta = h.AsignacionRuta!;

        return new NuevoReporteFraudeVM
        {
            HojaRutaId = hojaRutaId,
            TarjetaId = tarjetaId,
            Codigo = t.Codigo,
            Cliente = t.Cliente?.NombreCompleto ?? "—",
            Documento = t.Cliente?.DocumentoIdentidad ?? "—",
            Telefono = t.Cliente?.Telefono,
            Direccion = t.Cliente?.Direccion ?? "—",
            Repartidor = ruta.Repartidor?.Nombre ?? "—",
            Sector = ruta.SectorCobertura ?? "—",
            CodigoHojaControl = ruta.HojaAsignacion?.Lote?.CodigoHojaControl,
            CitaTexto = cita is null ? null : $"{cita.FechaEntrega:dd/MM/yyyy} {cita.HoraEntrega:HH\\:mm} · {cita.Lugar}"
        };
    }

    public async Task<ResultadoFraude> CrearAsync(NuevoReporteFraudeVM entrada, string verificadorId)
    {
        var h = await CargarHojaAsync(entrada.HojaRutaId);
        if (h is null || !Revisables.Contains(h.Estado))
            return new(false, "La hoja de ruta no existe o aún no está aprobada.");
        var asignada = await _db.AsignacionesTarjeta.AnyAsync(a => a.AsignacionRutaId == h.AsignacionRutaId && a.TarjetaId == entrada.TarjetaId);
        if (!asignada) return new(false, "La tarjeta no pertenece a esta hoja de ruta.");
        if (await _db.ReportesFraude.AnyAsync(r => r.TarjetaId == entrada.TarjetaId))
            return new(false, "Ya existe un reporte de fraude para esta tarjeta.");
        if (entrada.Tipo is null || !Enum.IsDefined(entrada.Tipo.Value)) return new(false, "Seleccione el indicador de fraude.");

        var tarjeta = await _db.Tarjetas.AsNoTracking().FirstAsync(t => t.Id == entrada.TarjetaId);
        var reporte = new ReporteFraude
        {
            TarjetaId = entrada.TarjetaId,
            HojaRutaId = entrada.HojaRutaId,
            Tipo = entrada.Tipo.Value,
            Descripcion = entrada.Descripcion.Trim(),
            VerificadorId = verificadorId,
            FechaHora = DateTime.UtcNow,
            Estado = EstadoReporteFraude.Generado
        };
        _db.ReportesFraude.Add(reporte);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return new(false, "Ya existe un reporte de fraude para esta tarjeta."); }

        return new(true, "Reporte de fraude generado. Queda disponible para el área de Descargo.", reporte.Id,
            $"Reporte {reporte.Id}: tarjeta {tarjeta.Codigo}, {reporte.Tipo.Texto()}");
    }

    public async Task<DetalleReporteFraudeVM?> DetalleAsync(int id)
    {
        var r = await _db.ReportesFraude.AsNoTracking()
            .Include(x => x.Verificador)
            .Include(x => x.Tarjeta).ThenInclude(t => t!.Cliente)
            .Include(x => x.HojaRuta).ThenInclude(h => h!.AsignacionRuta).ThenInclude(a => a!.Repartidor)
            .Include(x => x.HojaRuta).ThenInclude(h => h!.AsignacionRuta).ThenInclude(a => a!.HojaAsignacion).ThenInclude(a => a!.Lote)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return null;
        var ruta = r.HojaRuta?.AsignacionRuta;
        return new DetalleReporteFraudeVM
        {
            Id = r.Id,
            Codigo = r.Tarjeta?.Codigo ?? "—",
            Cliente = r.Tarjeta?.Cliente?.NombreCompleto ?? "—",
            Documento = r.Tarjeta?.Cliente?.DocumentoIdentidad ?? "—",
            Direccion = r.Tarjeta?.Cliente?.Direccion ?? "—",
            Repartidor = ruta?.Repartidor?.Nombre ?? "—",
            Sector = ruta?.SectorCobertura ?? "—",
            CodigoHojaControl = ruta?.HojaAsignacion?.Lote?.CodigoHojaControl,
            HojaRutaId = r.HojaRutaId,
            Tipo = r.Tipo,
            Descripcion = r.Descripcion,
            Verificador = r.Verificador?.NombreCompleto ?? "—",
            FechaHora = r.FechaHora,
            Estado = r.Estado
        };
    }

    private Task<HojaRuta?> CargarHojaAsync(int id) =>
        _db.HojasRuta.AsNoTracking()
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion).ThenInclude(a => a!.Lote)
            .AsSplitQuery().FirstOrDefaultAsync(h => h.Id == id);
}
