using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Domain;

public class Lote
{
    public int Id { get; set; }
    public int RemitoId { get; set; }
    public Remito? Remito { get; set; }
    public DateOnly FechaRecepcion { get; set; }
    public int CantidadDeclarada { get; set; }
    public int CantidadContada { get; set; }
    public EstadoLote Estado { get; set; }
    [MaxLength(30)] public string? CodigoHojaControl { get; set; }
    public string RegistradoPorId { get; set; } = string.Empty;
    public ApplicationUser? RegistradoPor { get; set; }
    public DateTime FechaHoraRegistro { get; set; }
    public ICollection<Tarjeta> Tarjetas { get; set; } = new List<Tarjeta>();
}