using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Domain;

// Fase 8 — Mensajería: hoja de ruta (CU05), reporte de fraude (CU06) y reporte de entregas (CU07/CU08).

public enum EstadoHojaRuta { PendienteDeAprobacion = 1, PendienteDeCorreccion, Aprobada, Firmada }
public enum TipoIndicadorFraude { IdentidadNoCoincide = 1, DocumentoInconsistente, DireccionSospechosa, EntregaFueraDeCita, Otro }
public enum EstadoReporteFraude { Generado = 1, RecibidoPorDescargo }
public enum EstadoReporteEntregas { Borrador = 1, Remitido }

// Una hoja de ruta por cada ruta (repartidor) de una hoja de asignación (CU19).
public class HojaRuta
{
    public int Id { get; set; }
    public int AsignacionRutaId { get; set; }
    public AsignacionRuta? AsignacionRuta { get; set; }
    public EstadoHojaRuta Estado { get; set; }
    public string GeneradaPorId { get; set; } = string.Empty;
    public ApplicationUser? GeneradaPor { get; set; }
    public DateTime FechaGeneracion { get; set; }
    [MaxLength(400)] public string? Discrepancia { get; set; }
    public string? AprobadaPorId { get; set; }
    public ApplicationUser? AprobadaPor { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public string? FirmaRegistradaPorId { get; set; }
    public ApplicationUser? FirmaRegistradaPor { get; set; }
    public DateTime? FechaFirma { get; set; }
    [MaxLength(300)] public string? MotivoSinFirma { get; set; }
}

public class ReporteFraude
{
    public int Id { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public int HojaRutaId { get; set; }
    public HojaRuta? HojaRuta { get; set; }
    public TipoIndicadorFraude Tipo { get; set; }
    [Required, MaxLength(500)] public string Descripcion { get; set; } = string.Empty;
    public string VerificadorId { get; set; } = string.Empty;
    public ApplicationUser? Verificador { get; set; }
    public DateTime FechaHora { get; set; }
    public EstadoReporteFraude Estado { get; set; }
}

// Un reporte por jornada (fecha de emisión de las hojas, hora de Lima).
public class ReporteEntregas
{
    public int Id { get; set; }
    public DateOnly FechaJornada { get; set; }
    public EstadoReporteEntregas Estado { get; set; }
    public string GeneradoPorId { get; set; } = string.Empty;
    public ApplicationUser? GeneradoPor { get; set; }
    public DateTime FechaGeneracion { get; set; }
    public DateTime FechaActualizacion { get; set; }
    [MaxLength(400)] public string? Observacion { get; set; }
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int Incidencias { get; set; }
    public string? RemitidoPorId { get; set; }
    public ApplicationUser? RemitidoPor { get; set; }
    public DateTime? FechaRemision { get; set; }
    public ICollection<ReporteEntregasLinea> Lineas { get; set; } = new List<ReporteEntregasLinea>();
}

public class ReporteEntregasLinea
{
    public int Id { get; set; }
    public int ReporteEntregasId { get; set; }
    public ReporteEntregas? ReporteEntregas { get; set; }
    public int HojaRutaId { get; set; }
    public HojaRuta? HojaRuta { get; set; }
    public int RepartidorId { get; set; }
    public Repartidor? Repartidor { get; set; }
    [MaxLength(150)] public string? Sector { get; set; }
    public int Asignadas { get; set; }
    public int Entregadas { get; set; }
    public int Devueltas { get; set; }
    public int Incidencias { get; set; }
}
