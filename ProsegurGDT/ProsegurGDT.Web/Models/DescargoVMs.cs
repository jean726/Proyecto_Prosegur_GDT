using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Models;

// ---------- CU09 Descargo de tarjetas ----------

public class DescargoRutaFilaVM
{
    public int HojaRutaId { get; set; }
    public DateOnly Fecha { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public int Tarjetas { get; set; }
    public int CargosRecibidos { get; set; }
    public int Procesadas { get; set; }
    public EstadoDescargoRuta Estado { get; set; }
    public bool Organizada { get; set; }
}

public class DescargoIndexVM
{
    public List<DescargoRutaFilaVM> Rutas { get; set; } = new();
    public int TotalAsignadas => Rutas.Sum(r => r.Tarjetas);
    public int TotalCargos => Rutas.Sum(r => r.CargosRecibidos);
    public int Pendientes => Rutas.Sum(r => r.Tarjetas - r.Procesadas);
}

public class TarjetaDescargoVM
{
    public int TarjetaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Documento { get; set; } = "—";
    public string Direccion { get; set; } = "—";
    public EstadoTarjeta Estado { get; set; }
    public ResultadoDescargo? Resultado { get; set; }
    public VerificacionDescargo? Verificacion { get; set; }
    public string? ReceptorNombre { get; set; }
    public string? ReceptorDocumento { get; set; }
    public MotivoNoEntrega? Motivo { get; set; }
    public string? Detalle { get; set; }
    public string? ObservacionBuscador { get; set; }
    public bool ConActa { get; set; }
    // B2 (RN-30): resultado registrado por el repartidor desde la app móvil, a confirmar con el cargo físico
    public ResultadoCampo? Campo { get; set; }
    public bool SuplantacionReportada { get; set; }
    public bool Pendiente => Estado == EstadoTarjeta.AsignadaARuta;
}

public class ProcesarRutaVM
{
    public int HojaRutaId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public DateOnly Fecha { get; set; }
    public EstadoDescargoRuta Estado { get; set; }
    public int CargosRecibidos { get; set; }
    public string? Observacion { get; set; }
    public bool Organizada { get; set; }
    public List<TarjetaDescargoVM> Tarjetas { get; set; } = new();
    public bool Cerrada => Estado == EstadoDescargoRuta.Cerrada;
    public int Pendientes => Tarjetas.Count(t => t.Pendiente);
}

public class CargosVM
{
    public int HojaRutaId { get; set; }
    [Range(0, 9999, ErrorMessage = "La cantidad de cargos no es válida.")]
    public int Cantidad { get; set; }
}

public class RegistrarResultadoVM
{
    public int HojaRutaId { get; set; }
    public int TarjetaId { get; set; }
    [Required(ErrorMessage = "Seleccione el resultado de la visita.")]
    public ResultadoDescargo? Resultado { get; set; }
    [StringLength(150)] public string? ReceptorNombre { get; set; }
    [StringLength(20)] public string? ReceptorDocumento { get; set; }
    public MotivoNoEntrega? Motivo { get; set; }
    [StringLength(300, ErrorMessage = "El detalle no puede superar 300 caracteres.")]
    public string? Detalle { get; set; }
}

public class LegajoVM
{
    public int HojaRutaId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public DateOnly Fecha { get; set; }
    public int CargosRecibidos { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? Descargador { get; set; }
    public bool Organizada { get; set; }
    public DateTime? FechaOrganizacion { get; set; }
    public List<TarjetaDescargoVM> Tarjetas { get; set; } = new();
}

// ---------- Verificación del Buscador (RN-13, RN-14) ----------

public class VerificacionFilaVM
{
    public int DescargoTarjetaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Documento { get; set; } = "—";
    public string Direccion { get; set; } = "—";
    public string? ReceptorNombre { get; set; }
    public string? ReceptorDocumento { get; set; }
    public string Repartidor { get; set; } = "—";
    public VerificacionDescargo Estado { get; set; }
    public string? Observacion { get; set; }
    public DateTime FechaHora { get; set; }
}

public class ResolverVerificacionVM
{
    public int Id { get; set; }
    public bool Resuelta { get; set; }
    [Required(ErrorMessage = "Indique el resultado de la verificación física."),
     StringLength(300, MinimumLength = 5, ErrorMessage = "La observación debe tener entre 5 y 300 caracteres.")]
    public string Observacion { get; set; } = string.Empty;
}

// ---------- CU12 Apartado de defraudación ----------

public class ActaFraudeFilaVM
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public OrigenFraude Origen { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public EstadoActaFraude Estado { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string? Ubicacion { get; set; }
    public DateTime? FechaIngreso { get; set; }
}

public class CandidatoFraudeVM
{
    public int TarjetaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public OrigenFraude Origen { get; set; }
    public int? ReporteFraudeId { get; set; }
    public string Detalle { get; set; } = string.Empty;
}

public class DefraudacionIndexVM
{
    public bool PuedeRegistrar { get; set; }
    public bool PuedeIngresar { get; set; }
    public List<CandidatoFraudeVM> Candidatos { get; set; } = new();
    public List<ActaFraudeFilaVM> Actas { get; set; } = new();
}

public class RegistrarActaVM
{
    public int TarjetaId { get; set; }
    public OrigenFraude Origen { get; set; }
    public int? ReporteFraudeId { get; set; }
}

public class IngresoBovedaVM
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Indique la ubicación en bóveda."),
     StringLength(120, MinimumLength = 2, ErrorMessage = "La ubicación debe tener entre 2 y 120 caracteres.")]
    public string Ubicacion { get; set; } = string.Empty;
}

public class InventarioFilaVM
{
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Ubicacion { get; set; } = string.Empty;
    public string EstadoFisico { get; set; } = string.Empty;
    public DateTime FechaActualizacion { get; set; }
}

// ---------- CU10 / CU13 / CU14 Reportes de descargo ----------

public class JornadaDescargoVM
{
    public DateOnly Fecha { get; set; }
    public int Rutas { get; set; }
    public int Cerradas { get; set; }
    public int Organizadas { get; set; }
    public int? ReporteEntregasId { get; set; }
    public int? ReporteDescargoId { get; set; }
    public EstadoReporteDescargo? EstadoDescargo { get; set; }
    public bool TodasCerradas => Rutas > 0 && Cerradas == Rutas;
}

public class ReportesDescargoIndexVM
{
    public List<JornadaDescargoVM> Jornadas { get; set; } = new();
}

public class LineaReporteDescargoVM
{
    public string Repartidor { get; set; } = "—";
    public string Sector { get; set; } = "—";
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int EnCustodia { get; set; }
    public int Incidencias { get; set; }
    public int Fraude { get; set; }
}

public class NoEntregadaVM
{
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = "—";
    public string Resultado { get; set; } = string.Empty;
    public string Motivo { get; set; } = "—";
    public string? Detalle { get; set; }
}

public class DetalleReporteDescargoVM
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public TipoReporteDescargo Tipo { get; set; }
    public EstadoReporteDescargo Estado { get; set; }
    public string GeneradoPor { get; set; } = "—";
    public DateTime FechaGeneracion { get; set; }
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int EnCustodia { get; set; }
    public int Incidencias { get; set; }
    public int Fraude { get; set; }
    public string? BancoDestino { get; set; }
    public string? DerivadoPor { get; set; }
    public DateTime? FechaDerivacion { get; set; }
    public List<LineaReporteDescargoVM> Lineas { get; set; } = new();
    public List<NoEntregadaVM> NoEntregadas { get; set; } = new();
}

// ---------- Dashboards ----------

public record FilaRutaPendiente(int HojaRutaId, string Repartidor, string Sector, int Pendientes, string Estado);

public class DashboardDescargadorViewModel
{
    public int RutasPorProcesar { get; set; }
    public int TarjetasPendientes { get; set; }
    public int RutasCerradas { get; set; }
    public int FraudePorRegistrar { get; set; }
    public int ReportesGenerados { get; set; }
    public List<FilaRutaPendiente> Rutas { get; set; } = new();
}

public class DashboardBovedaViewModel
{
    public int FraudePorIngresar { get; set; }
    public int IngresadasHoy { get; set; }
    public int InventarioTotal { get; set; }
    public int InventarioFraude { get; set; }
    public List<ActaFraudeFilaVM> Pendientes { get; set; } = new();
}
