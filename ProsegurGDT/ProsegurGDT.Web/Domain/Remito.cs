using System.ComponentModel.DataAnnotations;

namespace ProsegurGDT.Web.Domain;

public class Remito
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string Numero { get; set; } = string.Empty;
    public int BancoId { get; set; }
    public Banco? Banco { get; set; }
    public int CantidadDeclarada { get; set; }
    public DateOnly FechaEmision { get; set; }
}