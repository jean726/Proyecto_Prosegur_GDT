using Microsoft.AspNetCore.Mvc.Rendering;
using ProsegurGDT.Web.Domain;
namespace ProsegurGDT.Web.Models;

public class LoteClasifFilaVM
{
    public Lote Lote { get; set; } = null!;
    public int Total { get; set; }
    public int SinZona { get; set; }
    public int ConAlerta { get; set; }
    public int NoClasificables { get; set; }
}

public class FilaClasifVM
{
    public Tarjeta Tarjeta { get; set; } = null!;
    public int? ZonaId { get; set; }
    public AlertaDireccion? Alerta { get; set; }
    public bool Faltante { get; set; }
}

public class ClasificarLoteVM
{
    public Lote Lote { get; set; } = null!;
    public List<FilaClasifVM> Filas { get; set; } = new();
    public List<SelectListItem> Zonas { get; set; } = new();
    public string? Error { get; set; }
}