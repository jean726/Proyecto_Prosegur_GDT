using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Domain;

public class HistorialEstadoTarjeta
{
    public int Id { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public EstadoTarjeta? EstadoAnterior { get; set; }
    public EstadoTarjeta EstadoNuevo { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }
    public DateTime FechaHora { get; set; }
    [MaxLength(300)] public string? Observacion { get; set; }
}