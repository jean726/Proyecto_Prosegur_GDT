using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models.Api;

namespace ProsegurGDT.Web.Services.Api;

// Resultado de una operación de la API: Estado HTTP + mensaje en español (+ datos opcionales).
public record ResultadoMovil(int Estado, string Mensaje, object? Datos = null, string? Auditoria = null)
{
    public bool Ok => Estado is >= 200 and < 300;
}

public interface IRepartidorMovilService
{
    Task<List<HojaRutaResumenDto>> ListarHojasAsync(int repartidorId);
    Task<HojaRutaDto?> ObtenerHojaAsync(int repartidorId, int hojaRutaId);
    Task<ResultadoMovil> ConfirmarRecepcionAsync(int repartidorId, string usuarioId, int hojaRutaId);
    Task<ResultadoMovil> RegistrarResultadoAsync(int repartidorId, int hojaRutaId, int tarjetaId, SolicitudResultado s);
    Task<ResultadoMovil> ReportarSuplantacionAsync(int repartidorId, int hojaRutaId, int tarjetaId, SolicitudSuplantacion s);
}

// B2 — Lógica del canal móvil del Repartidor.
// RN-29: solo sus hojas aprobadas o firmadas. RN-30: el resultado de campo es un pre-registro que no toca el estado
// de la tarjeta (solo el Descargador lo cambia, RN-10, vía ITarjetaEstadoService en CU09).
public class RepartidorMovilService : IRepartidorMovilService
{
    private static readonly EstadoHojaRuta[] Visibles = { EstadoHojaRuta.Aprobada, EstadoHojaRuta.Firmada };
    private const string HojaNoDisponible = "La hoja de ruta no existe o no está asignada a usted.";

    private readonly ApplicationDbContext _db;
    private readonly IHojaRutaService _hojas;
    public RepartidorMovilService(ApplicationDbContext db, IHojaRutaService hojas) { _db = db; _hojas = hojas; }

    // ---------- CU28 ----------
    public async Task<List<HojaRutaResumenDto>> ListarHojasAsync(int repartidorId)
    {
        var hojas = await HojasDe(repartidorId)
            // Una hoja cuyo descargo ya se cerró sale de la app: la ruta terminó
            .Where(h => !_db.DescargosRuta.Any(d => d.HojaRutaId == h.Id && d.Estado == EstadoDescargoRuta.Cerrada))
            .OrderByDescending(h => h.Id).ToListAsync();
        var ids = hojas.Select(h => h.Id).ToList();
        var conResultado = await _db.ResultadosCampo.Where(r => ids.Contains(r.HojaRutaId))
            .GroupBy(r => r.HojaRutaId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        return hojas.Select(h => new HojaRutaResumenDto(h.Id, DescargoReglas.Jornada(h), h.AsignacionRuta!.Zona?.Nombre ?? "—",
            h.AsignacionRuta.SectorCobertura, h.Estado, h.Estado == EstadoHojaRuta.Firmada,
            h.AsignacionRuta.CantidadTarjetas, conResultado.GetValueOrDefault(h.Id))).ToList();
    }

    public async Task<HojaRutaDto?> ObtenerHojaAsync(int repartidorId, int hojaRutaId)
    {
        var h = await HojasDe(repartidorId).FirstOrDefaultAsync(x => x.Id == hojaRutaId);
        if (h is null) return null;
        var ruta = h.AsignacionRuta!;
        var descargoIniciado = await _db.DescargosRuta.AnyAsync(d => d.HojaRutaId == h.Id);

        var tarjetas = await _db.Tarjetas.AsNoTracking()
            .Where(t => _db.AsignacionesTarjeta.Any(a => a.AsignacionRutaId == ruta.Id && a.TarjetaId == t.Id))
            .Include(t => t.Cliente).ToListAsync();
        var ids = tarjetas.Select(t => t.Id).ToList();
        var citas = (await _db.Citas.AsNoTracking().Where(c => c.Vigente && ids.Contains(c.TarjetaId)).ToListAsync())
            .GroupBy(c => c.TarjetaId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());
        var resultados = await _db.ResultadosCampo.AsNoTracking().Where(r => r.HojaRutaId == h.Id).ToDictionaryAsync(r => r.TarjetaId);
        var suplantaciones = (await _db.ReportesSuplantacion.AsNoTracking().Where(r => r.HojaRutaId == h.Id).ToListAsync())
            .GroupBy(r => r.TarjetaId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());

        var visitas = tarjetas
            .Select(t => (t, cita: citas.GetValueOrDefault(t.Id)))
            .OrderBy(x => x.cita?.FechaEntrega ?? DateOnly.MaxValue).ThenBy(x => x.cita?.HoraEntrega ?? TimeOnly.MaxValue).ThenBy(x => x.t.Codigo)
            .Select((x, i) =>
            {
                var r = resultados.GetValueOrDefault(x.t.Id);
                var s = suplantaciones.GetValueOrDefault(x.t.Id);
                return new VisitaDto(i + 1, x.t.Id, x.t.Codigo, x.t.Cliente?.NombreCompleto ?? "—", x.t.Cliente?.DocumentoIdentidad ?? "—",
                    x.t.Cliente?.Telefono, x.t.Cliente?.Direccion ?? "—",
                    x.cita is null ? null : new CitaDto(x.cita.FechaEntrega, x.cita.HoraEntrega, x.cita.Lugar, x.cita.Observaciones),
                    x.t.Estado,
                    r is null ? null : new ResultadoCampoDto(r.Resultado, r.ReceptorNombre, r.ReceptorDocumento, r.Motivo, r.Detalle, Utc(r.FechaActualizacion)),
                    s is null ? null : new SuplantacionDto(s.Id, s.Estado, Utc(s.FechaHora)),
                    Editable: h.Estado == EstadoHojaRuta.Firmada && !descargoIniciado && x.t.Estado == EstadoTarjeta.AsignadaARuta);
            }).ToList();

        return new HojaRutaDto(h.Id, DescargoReglas.Jornada(h), ruta.Zona?.Nombre ?? "—", ruta.SectorCobertura, h.Estado,
            h.Estado == EstadoHojaRuta.Firmada, h.FechaFirma is DateTime f ? Utc(f) : null, descargoIniciado, visitas);
    }

    // ---------- CU31 ----------
    public async Task<ResultadoMovil> ConfirmarRecepcionAsync(int repartidorId, string usuarioId, int hojaRutaId)
    {
        var h = await HojasDe(repartidorId).FirstOrDefaultAsync(x => x.Id == hojaRutaId);
        if (h is null) return new(404, HojaNoDisponible);
        if (h.Estado == EstadoHojaRuta.Firmada) return new(409, "La recepción de esta hoja de ruta ya fue confirmada.");
        var r = await _hojas.RegistrarFirmaAsync(hojaRutaId, usuarioId);
        return r.Ok
            ? new(200, "Recepción de la hoja de ruta confirmada. Puede iniciar el recorrido.", null, $"Hoja de ruta {hojaRutaId} confirmada desde la app móvil")
            : new(409, r.Mensaje);
    }

    // ---------- CU29 ----------
    public async Task<ResultadoMovil> RegistrarResultadoAsync(int repartidorId, int hojaRutaId, int tarjetaId, SolicitudResultado s)
    {
        var (error, h, tarjeta) = await ValidarVisitaAsync(repartidorId, hojaRutaId, tarjetaId);
        if (error is not null) return error;
        if (tarjeta!.Estado != EstadoTarjeta.AsignadaARuta)
            return new(409, $"La tarjeta {tarjeta.Codigo} ya fue procesada en el descargo ({tarjeta.Estado.TextoEstado()}).");
        if (await _db.DescargosRuta.AnyAsync(d => d.HojaRutaId == hojaRutaId))
            return new(409, "El descargo de esta hoja de ruta ya comenzó: el resultado no puede modificarse desde la app.");

        var resultado = s.Resultado!.Value;
        if (!Enum.IsDefined(resultado)) return new(400, "Indique el resultado de la visita.");
        var nombre = Limpio(s.ReceptorNombre);
        var doc = Limpio(s.ReceptorDocumento);
        var detalle = Limpio(s.Detalle);
        MotivoNoEntrega? motivo = null;
        string? aviso = null;

        if (resultado == ResultadoVisita.Entregada)
        {
            // RN-07: sin validación de identidad no hay entrega
            if (nombre is null || doc is null)
                return new(400, "Para registrar la entrega ingrese el nombre y el documento de identidad del receptor.");
            if (!DescargoReglas.IdentidadCoincide(nombre, doc, tarjeta.Cliente!))
                aviso = " Aviso: el receptor no coincide con el titular; el Descargador lo derivará a verificación.";
        }
        else
        {
            // RN-07: toda no entrega requiere motivo
            if (s.Motivo is null || !Enum.IsDefined(s.Motivo.Value)) return new(400, "Seleccione el motivo de no entrega.");
            if (s.Motivo == MotivoNoEntrega.Otro && (detalle?.Length ?? 0) < 5)
                return new(400, "Describa el motivo de no entrega (mínimo 5 caracteres).");
            motivo = s.Motivo;
            nombre = doc = null;
        }

        var ahora = DateTime.UtcNow;
        var r = await _db.ResultadosCampo.FirstOrDefaultAsync(x => x.HojaRutaId == hojaRutaId && x.TarjetaId == tarjetaId);
        var nuevo = r is null;
        if (r is null)
        {
            r = new ResultadoCampo { HojaRutaId = hojaRutaId, TarjetaId = tarjetaId, RepartidorId = repartidorId, FechaRegistro = ahora };
            _db.ResultadosCampo.Add(r);
        }
        r.Resultado = resultado; r.ReceptorNombre = nombre; r.ReceptorDocumento = doc; r.Motivo = motivo; r.Detalle = detalle;
        r.FechaActualizacion = ahora;
        await _db.SaveChangesAsync();

        var texto = resultado == ResultadoVisita.Entregada ? "entregada" : $"no entregada ({motivo!.Value.Texto()})";
        return new(nuevo ? 201 : 200,
            $"Resultado registrado: tarjeta {tarjeta.Codigo} {texto}. Queda pendiente de confirmación en el descargo.{aviso}",
            new ResultadoCampoDto(r.Resultado, r.ReceptorNombre, r.ReceptorDocumento, r.Motivo, r.Detalle, Utc(r.FechaActualizacion)),
            $"Tarjeta {tarjeta.Codigo}: {texto} (hoja de ruta {hojaRutaId}){(nuevo ? "" : "; corrección")}");
    }

    // ---------- CU30 ----------
    public async Task<ResultadoMovil> ReportarSuplantacionAsync(int repartidorId, int hojaRutaId, int tarjetaId, SolicitudSuplantacion s)
    {
        var (error, _, tarjeta) = await ValidarVisitaAsync(repartidorId, hojaRutaId, tarjetaId);
        if (error is not null) return error;
        if (await _db.ReportesSuplantacion.AnyAsync(x => x.TarjetaId == tarjetaId && x.Estado == EstadoReporteSuplantacion.Pendiente))
            return new(409, $"Ya hay un reporte de suplantación pendiente para la tarjeta {tarjeta!.Codigo}. El Verificador lo está evaluando.");

        var rep = new ReporteSuplantacion
        {
            HojaRutaId = hojaRutaId, TarjetaId = tarjetaId, RepartidorId = repartidorId,
            Descripcion = s.Descripcion.Trim(), FechaHora = DateTime.UtcNow, Estado = EstadoReporteSuplantacion.Pendiente
        };
        _db.ReportesSuplantacion.Add(rep);
        await _db.SaveChangesAsync();
        return new(201, $"Reporte de suplantación enviado al Verificador (tarjeta {tarjeta!.Codigo}). No entregue la tarjeta.",
            new SuplantacionDto(rep.Id, rep.Estado, Utc(rep.FechaHora)),
            $"Reporte de suplantación {rep.Id}: tarjeta {tarjeta.Codigo} (hoja de ruta {hojaRutaId})");
    }

    // La visita exige hoja propia con recepción confirmada (RN-06: firma antes de iniciar la ruta) y tarjeta de esa hoja
    private async Task<(ResultadoMovil? Error, HojaRuta? Hoja, Tarjeta? Tarjeta)> ValidarVisitaAsync(int repartidorId, int hojaRutaId, int tarjetaId)
    {
        var h = await HojasDe(repartidorId).FirstOrDefaultAsync(x => x.Id == hojaRutaId);
        if (h is null) return (new(404, HojaNoDisponible), null, null);
        if (h.Estado != EstadoHojaRuta.Firmada)
            return (new(409, "Confirme la recepción de la hoja de ruta antes de registrar visitas."), h, null);
        var pertenece = await _db.AsignacionesTarjeta.AnyAsync(a => a.AsignacionRutaId == h.AsignacionRutaId && a.TarjetaId == tarjetaId);
        if (!pertenece) return (new(404, "La tarjeta no pertenece a esta hoja de ruta."), h, null);
        var t = await _db.Tarjetas.Include(x => x.Cliente).FirstAsync(x => x.Id == tarjetaId);
        return (null, h, t);
    }

    private IQueryable<HojaRuta> HojasDe(int repartidorId) =>
        _db.HojasRuta.AsNoTracking()
            .Where(h => h.AsignacionRuta!.RepartidorId == repartidorId && Visibles.Contains(h.Estado))
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Zona)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion);

    // Las fechas se guardan en UTC pero SQL Server las devuelve sin tipo: se marcan para que el JSON lleve «Z»
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
