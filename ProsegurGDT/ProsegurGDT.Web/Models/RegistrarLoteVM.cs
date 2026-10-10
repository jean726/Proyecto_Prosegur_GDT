namespace ProsegurGDT.Web.Models;

public class RegistrarLoteVM
{
    public string? NumeroRemito { get; set; }
    public bool RemitoEncontrado { get; set; }
    public int RemitoId { get; set; }
    public string? Entidad { get; set; }
    public DateOnly FechaRecepcion { get; set; }
    public int CantidadDeclarada { get; set; }
    public int? CantidadContada { get; set; }
    public string? Error { get; set; }
}