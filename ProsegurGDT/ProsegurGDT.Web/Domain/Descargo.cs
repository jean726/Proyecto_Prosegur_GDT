using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Domain;

// Fase 9 — Descargo: descargo de tarjetas (CU09), apartado de defraudación (CU12) y reportes (CU10, CU11, CU13, CU14).

public enum EstadoDescargoRuta { Pendiente = 1, EnProceso, PendienteDeDocumentacion, Cerrada }
public enum ResultadoDescargo { Entregada = 1, Devuelta, ConIncidencia, EnCustodia }
public enum MotivoNoEntrega { ClienteAusente = 1, DireccionIncorrecta, RechazadaPorElCliente, ClienteNoIdentificado, Otro }
public enum VerificacionDescargo { Conforme = 1, DerivadaAlBuscador, DiscrepanciaResuelta, DiscrepanciaNoResuelta }
public enum OrigenFraude { DiscrepanciaDeDescargo = 1, ReporteDelVerificador }
public enum EstadoActaFraude { Registrada = 1, IngresadaABoveda }
public enum TipoReporteDescargo { Entregas = 1, Descargo }
public enum EstadoReporteDescargo { Generado = 1, Derivado }

// Una por hoja de ruta firmada incluida en un reporte de entregas remitido (CU08).
public class DescargoRuta
{
    public int Id { get; set; }
    public int HojaRutaId { get; set; }
    public HojaRuta? HojaRuta { get; set; }
    public EstadoDescargoRuta Estado { get; set; }
    public int CargosRecibidos { get; set; }
    [MaxLength(300)] public string? Observacion { get; set; }
    public string? DescargadorId { get; set; }
    public ApplicationUser? Descargador { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaCierre { get; set; }
    public bool DocumentacionOrganizada { get; set; }
    public DateTime? FechaOrganizacion { get; set; }
    public ICollection<DescargoTarjeta> Tarjetas { get; set; } = new List<DescargoTarjeta>();
}

// Resultado verificado de la visita de cada tarjeta (único por tarjeta).
public class DescargoTarjeta
{
    public int Id { get; set; }
    public int DescargoRutaId { get; set; }
    public DescargoRuta? DescargoRuta { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public ResultadoDescargo? Resultado { get; set; }
    [MaxLength(150)] public string? ReceptorNombre { get; set; }
    [MaxLength(20)] public string? ReceptorDocumento { get; set; }
    public VerificacionDescargo Verificacion { get; set; }
    public MotivoNoEntrega? Motivo { get; set; }
    [MaxLength(300)] public string? Detalle { get; set; }
    [MaxLength(300)] public string? ObservacionBuscador { get; set; }
    public string? BuscadorId { get; set; }
    public ApplicationUser? Buscador { get; set; }
    public DateTime? FechaVerificacionBuscador { get; set; }
    public string DescargadorId { get; set; } = string.Empty;
    public ApplicationUser? Descargador { get; set; }
    public DateTime FechaHora { get; set; }
}

// Acta de reporte de fraude + custodia especial en bóveda (único por tarjeta).
public class ActaFraude
{
    public int Id { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public OrigenFraude Origen { get; set; }
    public int? ReporteFraudeId { get; set; }
    public ReporteFraude? ReporteFraude { get; set; }
    [Required, MaxLength(500)] public string Descripcion { get; set; } = string.Empty;
    public string DescargadorId { get; set; } = string.Empty;
    public ApplicationUser? Descargador { get; set; }
    public DateTime FechaRegistro { get; set; }
    public EstadoActaFraude Estado { get; set; }
    public string? BovedaPorId { get; set; }
    public ApplicationUser? BovedaPor { get; set; }
    public DateTime? FechaIngreso { get; set; }
    [MaxLength(120)] public string? UbicacionBoveda { get; set; }
}

// Un reporte por jornada y tipo: «Entregas» (CU10) y «Descargo» consolidado (CU13), derivable al banco (CU14).
public class ReporteDescargo
{
    public int Id { get; set; }
    public DateOnly FechaJornada { get; set; }
    public TipoReporteDescargo Tipo { get; set; }
    public EstadoReporteDescargo Estado { get; set; }
    public string GeneradoPorId { get; set; } = string.Empty;
    public ApplicationUser? GeneradoPor { get; set; }
    public DateTime FechaGeneracion { get; set; }
    [MaxLength(400)] public string? Observacion { get; set; }
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int EnCustodia { get; set; }
    public int Incidencias { get; set; }
    public int Fraude { get; set; }
    [MaxLength(200)] public string? BancoDestino { get; set; }
    public string? DerivadoPorId { get; set; }
    public ApplicationUser? DerivadoPor { get; set; }
    public DateTime? FechaDerivacion { get; set; }
    public ICollection<ReporteDescargoLinea> Lineas { get; set; } = new List<ReporteDescargoLinea>();
}

public class ReporteDescargoLinea
{
    public int Id { get; set; }
    public int ReporteDescargoId { get; set; }
    public ReporteDescargo? ReporteDescargo { get; set; }
    public int DescargoRutaId { get; set; }
    public DescargoRuta? DescargoRuta { get; set; }
    public int RepartidorId { get; set; }
    public Repartidor? Repartidor { get; set; }
    [MaxLength(150)] public string? Sector { get; set; }
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int EnCustodia { get; set; }
    public int Incidencias { get; set; }
    public int Fraude { get; set; }
}
