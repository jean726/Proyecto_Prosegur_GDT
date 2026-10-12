using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;
namespace ProsegurGDT.Web.Domain;

public enum EstadoDireccion { Pendiente = 1, Validada, Descartada }
public enum NivelAlerta { Media = 1, Alta }
public enum DecisionAlerta { Pendiente = 1, Confirmada, Descartada }

public class Zona
{
    public int Id { get; set; }
    [Required, MaxLength(60)] public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public class DireccionFraudulenta
{
    public int Id { get; set; }
    [Required, MaxLength(250)] public string DireccionOriginal { get; set; } = string.Empty;
    [Required, MaxLength(250)] public string DireccionNormalizada { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Motivo { get; set; } = string.Empty;
    [MaxLength(120)] public string? Origen { get; set; }
    public EstadoDireccion Estado { get; set; }
    public byte[]? Vector { get; set; }
    [MaxLength(40)] public string? ModeloVersion { get; set; }
    public string RegistradaPorId { get; set; } = string.Empty;
    public ApplicationUser? RegistradaPor { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string? ValidadaPorId { get; set; }
    public ApplicationUser? ValidadaPor { get; set; }
    public DateTime? FechaValidacion { get; set; }
}

public class AlertaDireccion
{
    public int Id { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public int DireccionFraudulentaId { get; set; }
    public DireccionFraudulenta? DireccionFraudulenta { get; set; }
    public double Puntaje { get; set; }
    public NivelAlerta Nivel { get; set; }
    [MaxLength(40)] public string Metodo { get; set; } = string.Empty;
    [MaxLength(200)] public string? Detalle { get; set; }
    public DecisionAlerta Decision { get; set; }
    public DateTime FechaGeneracion { get; set; }
    public string? DecididoPorId { get; set; }
    public ApplicationUser? DecididoPor { get; set; }
    public DateTime? FechaDecision { get; set; }
}

public static class ReglasClasificacion
{
    // Tarjetas que todavía pueden clasificarse por zona
    public static readonly EstadoTarjeta[] Elegibles =
    {
        EstadoTarjeta.IngresadaEnCustodia, EstadoTarjeta.PendienteDeContacto,
        EstadoTarjeta.PendienteDeCita, EstadoTarjeta.CitaProgramada
    };
}