using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;
namespace ProsegurGDT.Web.Domain;

public enum ResultadoLlamada
{
    Exitoso = 1, ContactadoSinAcuerdo, NoContesta, BuzonDeVoz,
    NumeroIncorrecto, NumeroFueraDeServicio, Otro, DatosInsuficientes
}

public class RegistroLlamada
{
    public int Id { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public ResultadoLlamada Resultado { get; set; }
    [MaxLength(400)] public string? Observaciones { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }
    public DateTime FechaHora { get; set; }
}

public class Cita
{
    public int Id { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public DateOnly FechaEntrega { get; set; }
    public TimeOnly HoraEntrega { get; set; }
    [Required, MaxLength(250)] public string Lugar { get; set; } = string.Empty;
    [MaxLength(400)] public string? Observaciones { get; set; }
    public bool Vigente { get; set; } = true;
    public string RegistradaPorId { get; set; } = string.Empty;
    public ApplicationUser? RegistradaPor { get; set; }
    public DateTime FechaHoraRegistro { get; set; }

}