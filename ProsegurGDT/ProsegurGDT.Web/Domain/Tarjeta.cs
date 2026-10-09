using System.ComponentModel.DataAnnotations;

namespace ProsegurGDT.Web.Domain;

public class Tarjeta
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string Codigo { get; set; } = string.Empty;
    public int LoteId { get; set; }
    public Lote? Lote { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public EstadoTarjeta Estado { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public ICollection<HistorialEstadoTarjeta> Historial { get; set; } = new List<HistorialEstadoTarjeta>();
}