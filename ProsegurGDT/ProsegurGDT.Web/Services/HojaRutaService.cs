using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoHojaRuta(bool Ok, string Mensaje, string? Detalle = null);

public interface IHojaRutaService
{
    Task<HojasRutaIndexVM> ListarAsync();
    Task<ResultadoHojaRuta> GenerarAsync(int hojaAsignacionId, string usuarioId);
    Task<DetalleHojaRutaVM?> DetalleAsync(int id);
    Task<ResultadoHojaRuta> AprobarAsync(int id, string usuarioId);
    Task<ResultadoHojaRuta> ReportarDiscrepanciaAsync(int id, string texto);
    Task<ResultadoHojaRuta> ResolverAsync(int id);
    Task<ResultadoHojaRuta> RegistrarFirmaAsync(int id, string usuarioId);
    Task<ResultadoHojaRuta> RegistrarSinFirmaAsync(int id, string motivo);
}

// CU05 — El Supervisor genera una hoja de ruta por cada ruta de la hoja de asignación (CU19),
// verifica sus tarjetas contra el sistema, la aprueba (RN-05) y registra la firma del repartidor (RN-06).
public class HojaRutaService : IHojaRutaService
{
    private readonly ApplicationDbContext _db;
    public HojaRutaService(ApplicationDbContext db) => _db = db;

    public async Task<HojasRutaIndexVM> ListarAsync()
    {
        var asignaciones = await _db.HojasAsignacion
            .Where(h => h.Estado == EstadoHojaAsignacion.Emitida)
            .Include(h => h.Lote).ThenInclude(l => l!.Remito)
            .Include(h => h.Rutas)
            .OrderByDescending(h => h.Id).ToListAsync();

        var rutasConHoja = (await _db.HojasRuta.Select(h => h.AsignacionRutaId).ToListAsync()).ToHashSet();

        var vm = new HojasRutaIndexVM();
        foreach (var a in asignaciones)
        {
            var sinHoja = a.Rutas.Count(r => !rutasConHoja.Contains(r.Id));
            if (sinHoja > 0)
                vm.Pendientes.Add(new HojaAsignacionPendienteVM
                {
                    HojaAsignacionId = a.Id,
                    CodigoHojaControl = a.Lote?.CodigoHojaControl,
                    Remito = a.Lote?.Remito?.Numero,
                    FechaHoraEmision = a.FechaHoraEmision,
                    Rutas = a.Rutas.Count,
                    RutasSinHoja = sinHoja
                });
        }

        var hojas = await _db.HojasRuta
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion).ThenInclude(a => a!.Lote)
            .OrderByDescending(h => h.Id).ToListAsync();
        vm.Hojas = hojas.Select(h => new HojaRutaFilaVM
        {
            Id = h.Id,
            CodigoHojaControl = h.AsignacionRuta?.HojaAsignacion?.Lote?.CodigoHojaControl,
            Repartidor = h.AsignacionRuta?.Repartidor?.Nombre ?? "—",
            Sector = h.AsignacionRuta?.SectorCobertura ?? "—",
            Tarjetas = h.AsignacionRuta?.CantidadTarjetas ?? 0,
            Estado = h.Estado,
            FechaEmisionAsignacion = h.AsignacionRuta?.HojaAsignacion?.FechaHoraEmision ?? h.FechaGeneracion
        }).ToList();
        return vm;
    }

    public async Task<ResultadoHojaRuta> GenerarAsync(int hojaAsignacionId, string usuarioId)
    {
        var hoja = await _db.HojasAsignacion.Include(h => h.Rutas).Include(h => h.Lote)
            .FirstOrDefaultAsync(h => h.Id == hojaAsignacionId);
        if (hoja is null) return new(false, "La hoja de asignación no existe.");
        if (hoja.Estado != EstadoHojaAsignacion.Emitida) return new(false, "La hoja de asignación no está emitida.");

        var rutaIds = hoja.Rutas.Select(r => r.Id).ToList();
        var existentes = (await _db.HojasRuta.Where(h => rutaIds.Contains(h.AsignacionRutaId))
            .Select(h => h.AsignacionRutaId).ToListAsync()).ToHashSet();
        var nuevas = hoja.Rutas.Where(r => !existentes.Contains(r.Id)).OrderBy(r => r.OrdenVisita).ToList();
        if (nuevas.Count == 0) return new(false, "Todas las rutas de esta hoja ya tienen su hoja de ruta.");

        var ahora = DateTime.UtcNow;
        foreach (var r in nuevas)
            _db.HojasRuta.Add(new HojaRuta
            {
                AsignacionRutaId = r.Id,
                Estado = EstadoHojaRuta.PendienteDeAprobacion,
                GeneradaPorId = usuarioId,
                FechaGeneracion = ahora
            });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return new(false, "Otro usuario generó las hojas de ruta al mismo tiempo. Actualice la pantalla."); }

        return new(true, $"{nuevas.Count} hoja(s) de ruta generadas.",
            $"Hoja de asignación {hoja.Id} lote {hoja.Lote?.CodigoHojaControl}: {nuevas.Count} hojas de ruta");
    }

    public async Task<DetalleHojaRutaVM?> DetalleAsync(int id)
    {
        var h = await CargarAsync(id, seguimiento: false);
        if (h is null) return null;
        var ruta = h.AsignacionRuta!;
        var tarjetas = await ConstruirTarjetasAsync(ruta);

        return new DetalleHojaRutaVM
        {
            Id = h.Id,
            CodigoHojaControl = ruta.HojaAsignacion?.Lote?.CodigoHojaControl,
            Remito = ruta.HojaAsignacion?.Lote?.Remito?.Numero,
            Banco = ruta.HojaAsignacion?.Lote?.Remito?.Banco?.Nombre,
            Repartidor = ruta.Repartidor?.Nombre ?? "—",
            TipoRepartidor = ruta.Repartidor?.Tipo ?? TipoRepartidor.Motorizado,
            Sector = ruta.SectorCobertura ?? "—",
            FechaEmisionAsignacion = ruta.HojaAsignacion?.FechaHoraEmision ?? h.FechaGeneracion,
            Estado = h.Estado,
            Discrepancia = h.Discrepancia,
            AprobadaPor = h.AprobadaPor?.NombreCompleto,
            FechaAprobacion = h.FechaAprobacion,
            FirmaRegistradaPor = h.FirmaRegistradaPor?.NombreCompleto,
            FechaFirma = h.FechaFirma,
            MotivoSinFirma = h.MotivoSinFirma,
            Tarjetas = tarjetas
        };
    }

    public async Task<ResultadoHojaRuta> AprobarAsync(int id, string usuarioId)
    {
        var h = await CargarAsync(id, seguimiento: true);
        if (h is null) return new(false, "La hoja de ruta no existe.");
        if (h.Estado == EstadoHojaRuta.Aprobada || h.Estado == EstadoHojaRuta.Firmada)
            return new(false, "La hoja de ruta ya fue aprobada.");
        if (h.Estado == EstadoHojaRuta.PendienteDeCorreccion)
            return new(false, "La hoja está pendiente de corrección. Resuelva la discrepancia antes de aprobarla.");

        var tarjetas = await ConstruirTarjetasAsync(h.AsignacionRuta!);
        var malas = tarjetas.Where(t => t.Observacion is not null).ToList();
        if (malas.Count > 0)
        {
            var primera = malas[0];
            h.Estado = EstadoHojaRuta.PendienteDeCorreccion;
            h.Discrepancia = Recortar($"Tarjeta {primera.Codigo}: {primera.Observacion}" + (malas.Count > 1 ? $" (+{malas.Count - 1} más)" : ""), 400);
            await _db.SaveChangesAsync();
            return new(false, $"Discrepancia detectada en la tarjeta {primera.Codigo}. Verifique el registro antes de aprobar.",
                $"Hoja de ruta {id} en corrección: {h.Discrepancia}");
        }

        h.Estado = EstadoHojaRuta.Aprobada;
        h.Discrepancia = null;
        h.AprobadaPorId = usuarioId;
        h.FechaAprobacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new(true, "Hoja de ruta aprobada.", $"Hoja de ruta {id} aprobada ({tarjetas.Count} tarjetas)");
    }

    public async Task<ResultadoHojaRuta> ReportarDiscrepanciaAsync(int id, string texto)
    {
        var h = await CargarAsync(id, seguimiento: true);
        if (h is null) return new(false, "La hoja de ruta no existe.");
        if (h.Estado != EstadoHojaRuta.PendienteDeAprobacion)
            return new(false, "Solo se puede reportar una discrepancia en una hoja pendiente de aprobación.");
        h.Estado = EstadoHojaRuta.PendienteDeCorreccion;
        h.Discrepancia = Recortar(texto.Trim(), 400);
        await _db.SaveChangesAsync();
        return new(true, "Discrepancia reportada. La hoja queda pendiente de corrección.", $"Hoja de ruta {id}: {h.Discrepancia}");
    }

    public async Task<ResultadoHojaRuta> ResolverAsync(int id)
    {
        var h = await CargarAsync(id, seguimiento: true);
        if (h is null) return new(false, "La hoja de ruta no existe.");
        if (h.Estado != EstadoHojaRuta.PendienteDeCorreccion) return new(false, "La hoja no está pendiente de corrección.");

        var tarjetas = await ConstruirTarjetasAsync(h.AsignacionRuta!);
        var primera = tarjetas.FirstOrDefault(t => t.Observacion is not null);
        if (primera is not null)
            return new(false, $"La discrepancia persiste en la tarjeta {primera.Codigo}: {primera.Observacion}. Corrija el registro y vuelva a verificar.");

        h.Estado = EstadoHojaRuta.PendienteDeAprobacion;
        h.Discrepancia = null;
        await _db.SaveChangesAsync();
        return new(true, "Discrepancia resuelta. La hoja vuelve a estar pendiente de aprobación.", $"Hoja de ruta {id} corregida");
    }

    public async Task<ResultadoHojaRuta> RegistrarFirmaAsync(int id, string usuarioId)
    {
        var h = await CargarAsync(id, seguimiento: true);
        if (h is null) return new(false, "La hoja de ruta no existe.");
        if (h.Estado != EstadoHojaRuta.Aprobada) return new(false, "Solo se puede registrar la firma de una hoja aprobada y sin firma.");
        h.Estado = EstadoHojaRuta.Firmada;
        h.FirmaRegistradaPorId = usuarioId;
        h.FechaFirma = DateTime.UtcNow;
        h.MotivoSinFirma = null;
        await _db.SaveChangesAsync();
        return new(true, "Firma de conformidad del repartidor registrada.", $"Hoja de ruta {id} firmada por {h.AsignacionRuta?.Repartidor?.Nombre}");
    }

    public async Task<ResultadoHojaRuta> RegistrarSinFirmaAsync(int id, string motivo)
    {
        var h = await CargarAsync(id, seguimiento: true);
        if (h is null) return new(false, "La hoja de ruta no existe.");
        if (h.Estado != EstadoHojaRuta.Aprobada) return new(false, "Solo se puede registrar la falta de firma en una hoja aprobada y sin firma.");
        h.MotivoSinFirma = Recortar(motivo.Trim(), 300);
        await _db.SaveChangesAsync();
        return new(true, "Se registró que el repartidor no firmó. La custodia no se transfiere hasta obtener la firma.",
            $"Hoja de ruta {id} sin firma: {h.MotivoSinFirma}");
    }

    // ---------- Apoyo ----------

    private static string Recortar(string t, int max) => t.Length <= max ? t : t[..max];

    private Task<HojaRuta?> CargarAsync(int id, bool seguimiento)
    {
        IQueryable<HojaRuta> q = _db.HojasRuta
            .Include(h => h.AprobadaPor).Include(h => h.FirmaRegistradaPor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion).ThenInclude(a => a!.Lote).ThenInclude(l => l!.Remito).ThenInclude(r => r!.Banco)
            .AsSplitQuery();
        if (!seguimiento) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(h => h.Id == id);
    }

    // Tarjetas de la ruta con su cita vigente y la verificación contra el sistema (paso 7 de CU05)
    private async Task<List<TarjetaHojaRutaVM>> ConstruirTarjetasAsync(AsignacionRuta ruta)
    {
        var asignadas = await _db.Tarjetas.AsNoTracking()
            .Where(t => _db.AsignacionesTarjeta.Any(a => a.AsignacionRutaId == ruta.Id && a.TarjetaId == t.Id))
            .Include(t => t.Cliente).Include(t => t.Zona).ToListAsync();
        var ids = asignadas.Select(t => t.Id).ToList();

        var citas = (await _db.Citas.AsNoTracking().Where(c => c.Vigente && ids.Contains(c.TarjetaId)).ToListAsync())
            .GroupBy(c => c.TarjetaId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());
        var conAlerta = (await _db.Alertas.AsNoTracking().Where(a => a.Decision == DecisionAlerta.Pendiente && ids.Contains(a.TarjetaId))
            .Select(a => a.TarjetaId).Distinct().ToListAsync()).ToHashSet();

        return asignadas.Select(t =>
        {
            citas.TryGetValue(t.Id, out var cita);
            string? obs = null;
            if (t.Estado != EstadoTarjeta.AsignadaARuta) obs = $"su estado es «{t.Estado.TextoEstado()}» y debe ser «Asignada a ruta»";
            else if (cita is null) obs = "no tiene una cita vigente";
            else if (conAlerta.Contains(t.Id)) obs = "tiene una alerta de dirección pendiente";
            else if ((t.Cliente?.Direccion?.Trim().Length ?? 0) < 8) obs = "la dirección de entrega del cliente está incompleta";
            return new TarjetaHojaRutaVM
            {
                TarjetaId = t.Id,
                Codigo = t.Codigo,
                Cliente = t.Cliente?.NombreCompleto ?? "—",
                Direccion = t.Cliente?.Direccion ?? "—",
                Zona = t.Zona?.Nombre,
                FechaCita = cita?.FechaEntrega,
                HoraCita = cita?.HoraEntrega,
                LugarCita = cita?.Lugar,
                Estado = t.Estado,
                Observacion = obs
            };
        })
        .OrderBy(t => t.FechaCita ?? DateOnly.MaxValue).ThenBy(t => t.HoraCita ?? TimeOnly.MaxValue).ThenBy(t => t.Codigo)
        .ToList();
    }
}
