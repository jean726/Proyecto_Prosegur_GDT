using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Domain;

// B2 — Canal móvil del Repartidor (CU28–CU31, RN-29 y RN-30).

public enum ResultadoVisita { Entregada = 1, NoEntregada }
public enum EstadoReporteSuplantacion { Pendiente = 1, Atendido, Descartado }

// CU29 — Resultado de la visita registrado en campo. Es un pre-registro (RN-30): no cambia el estado de la
// tarjeta; el Descargador lo ve precargado en CU09 y lo confirma con el cargo físico.
public class ResultadoCampo
{
    public int Id { get; set; }
    public int HojaRutaId { get; set; }
    public HojaRuta? HojaRuta { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public int RepartidorId { get; set; }
    public Repartidor? Repartidor { get; set; }
    public ResultadoVisita Resultado { get; set; }
    [MaxLength(150)] public string? ReceptorNombre { get; set; }
    [MaxLength(20)] public string? ReceptorDocumento { get; set; }
    public MotivoNoEntrega? Motivo { get; set; }
    [MaxLength(300)] public string? Detalle { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

// CU30 — Sospecha de suplantación reportada desde el punto de entrega. El Verificador la evalúa (RN-08)
// y, si procede, genera el Reporte de Fraude (CU06).
public class ReporteSuplantacion
{
    public int Id { get; set; }
    public int HojaRutaId { get; set; }
    public HojaRuta? HojaRuta { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public int RepartidorId { get; set; }
    public Repartidor? Repartidor { get; set; }
    [Required, MaxLength(500)] public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public EstadoReporteSuplantacion Estado { get; set; }
    public int? ReporteFraudeId { get; set; }
    public ReporteFraude? ReporteFraude { get; set; }
    public string? AtendidoPorId { get; set; }
    public ApplicationUser? AtendidoPor { get; set; }
    public DateTime? FechaAtencion { get; set; }
    [MaxLength(300)] public string? Observacion { get; set; }
}
