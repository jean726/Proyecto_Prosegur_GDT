using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Models;

// ---------- CU05 Hojas de ruta ----------

public class HojaAsignacionPendienteVM
{
    public int HojaAsignacionId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public DateTime FechaHoraEmision { get; set; }
    public int Rutas { get; set; }
    public int RutasSinHoja { get; set; }
}

public class HojaRutaFilaVM
{
    public int Id { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public int Tarjetas { get; set; }
    public EstadoHojaRuta Estado { get; set; }
    public DateTime FechaEmisionAsignacion { get; set; }
}

public class HojasRutaIndexVM
{
    public bool PuedeGestionar { get; set; }
    public List<HojaAsignacionPendienteVM> Pendientes { get; set; } = new();
    public List<HojaRutaFilaVM> Hojas { get; set; } = new();
}

public class TarjetaHojaRutaVM
{
    public int TarjetaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Direccion { get; set; } = "—";
    public string? Zona { get; set; }
    public DateOnly? FechaCita { get; set; }
    public TimeOnly? HoraCita { get; set; }
    public string? LugarCita { get; set; }
    public EstadoTarjeta Estado { get; set; }
    public string? Observacion { get; set; }   // discrepancia detectada por el sistema
}

public class DetalleHojaRutaVM
{
    public int Id { get; set; }
    public bool PuedeGestionar { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public string Repartidor { get; set; } = "—";
    public TipoRepartidor TipoRepartidor { get; set; }
    public string Sector { get; set; } = "—";
    public DateTime FechaEmisionAsignacion { get; set; }
    public EstadoHojaRuta Estado { get; set; }
    public string? Discrepancia { get; set; }
    public string? AprobadaPor { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public string? FirmaRegistradaPor { get; set; }
    public DateTime? FechaFirma { get; set; }
    public string? MotivoSinFirma { get; set; }
    public List<TarjetaHojaRutaVM> Tarjetas { get; set; } = new();
    public int TarjetasConDiscrepancia => Tarjetas.Count(t => t.Observacion is not null);
}

public class MotivoVM
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Indique el detalle."), StringLength(300, MinimumLength = 5, ErrorMessage = "El detalle debe tener entre 5 y 300 caracteres.")]
    public string Texto { get; set; } = string.Empty;
}

// ---------- CU06 Reporte de fraude ----------

public class HojaRevisionFilaVM
{
    public int HojaRutaId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public int Tarjetas { get; set; }
    public int Reportes { get; set; }
    public EstadoHojaRuta Estado { get; set; }
}

public class ReporteFraudeFilaVM
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Repartidor { get; set; } = "—";
    public TipoIndicadorFraude Tipo { get; set; }
    public EstadoReporteFraude Estado { get; set; }
    public DateTime FechaHora { get; set; }
}

public class FraudeIndexVM
{
    public List<HojaRevisionFilaVM> Hojas { get; set; } = new();
    public List<ReporteFraudeFilaVM> Reportes { get; set; } = new();
    public List<SuplantacionFilaVM> Suplantaciones { get; set; } = new();
}

// B2 (CU30): sospecha de suplantación reportada por el repartidor desde la app móvil
public class SuplantacionFilaVM
{
    public int Id { get; set; }
    public int HojaRutaId { get; set; }
    public int TarjetaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Repartidor { get; set; } = "—";
    public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
}

public class RevisarHojaVM
{
    public int HojaRutaId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public EstadoHojaRuta Estado { get; set; }
    public List<TarjetaRevisionVM> Tarjetas { get; set; } = new();
}

public class TarjetaRevisionVM
{
    public int TarjetaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Direccion { get; set; } = "—";
    public EstadoTarjeta Estado { get; set; }
    public int? ReporteId { get; set; }
}

public class NuevoReporteFraudeVM
{
    public int HojaRutaId { get; set; }
    public int TarjetaId { get; set; }
    // B2: reporte de suplantación de campo que origina este reporte (se marca como atendido al generarlo)
    public int? SuplantacionId { get; set; }

    [Required(ErrorMessage = "Seleccione el indicador de fraude detectado.")]
    public TipoIndicadorFraude? Tipo { get; set; }

    [Required(ErrorMessage = "Describa la irregularidad detectada."),
     StringLength(500, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 500 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    // Solo lectura: contexto de la tarjeta y ficha del cliente (RN-04: el Verificador consulta la ficha)
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Documento { get; set; } = "—";
    public string? Telefono { get; set; }
    public string Direccion { get; set; } = "—";
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public string? CitaTexto { get; set; }
    public string? CodigoHojaControl { get; set; }
}

public class DetalleReporteFraudeVM
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Documento { get; set; } = "—";
    public string Direccion { get; set; } = "—";
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public string? CodigoHojaControl { get; set; }
    public int HojaRutaId { get; set; }
    public TipoIndicadorFraude Tipo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Verificador { get; set; } = "—";
    public DateTime FechaHora { get; set; }
    public EstadoReporteFraude Estado { get; set; }
}

// ---------- CU07 / CU08 Reporte de entregas ----------

public class ReporteEntregasFilaVM
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public EstadoReporteEntregas Estado { get; set; }
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int Incidencias { get; set; }
    public DateTime? FechaRemision { get; set; }
}

public class JornadaDisponibleVM
{
    public DateOnly Fecha { get; set; }
    public int HojasListas { get; set; }
    public int HojasPendientes { get; set; }
}

public class ReportesEntregasIndexVM
{
    public List<ReporteEntregasFilaVM> Reportes { get; set; } = new();
    public List<JornadaDisponibleVM> Jornadas { get; set; } = new();
}

public class LineaReporteEntregasVM
{
    public int HojaRutaId { get; set; }
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int Incidencias { get; set; }
    public int Pendientes => Asignadas - Entregadas - Devueltas - Incidencias;
}

public class DetalleReporteEntregasVM
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public EstadoReporteEntregas Estado { get; set; }
    public string GeneradoPor { get; set; } = "—";
    public DateTime FechaGeneracion { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public string? Observacion { get; set; }
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int Incidencias { get; set; }
    public int Pendientes => Asignadas - Entregadas - Devueltas - Incidencias;
    public string? RemitidoPor { get; set; }
    public DateTime? FechaRemision { get; set; }
    public int HojasPendientesJornada { get; set; }
    public List<LineaReporteEntregasVM> Lineas { get; set; } = new();
}

public class ObservacionReporteVM
{
    public int Id { get; set; }
    [StringLength(400, ErrorMessage = "La observación no puede superar 400 caracteres.")]
    public string? Observacion { get; set; }
}

// ---------- Dashboard del Verificador ----------

public record FilaReporteFraudeReciente(int Id, string Codigo, string Tipo, DateTime FechaHora);

public class DashboardVerificadorViewModel
{
    public int HojasPorRevisar { get; set; }
    public int ReportesHoy { get; set; }
    public int ReportesTotal { get; set; }
    public int TarjetasEnRevision { get; set; }
    public List<FilaReporteFraudeReciente> Recientes { get; set; } = new();
}
