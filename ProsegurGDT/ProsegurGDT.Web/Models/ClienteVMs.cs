using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProsegurGDT.Web.Domain;
namespace ProsegurGDT.Web.Models;

public class ClienteEditarVM
{
    public int Id { get; set; }
    public string DocumentoIdentidad { get; set; } = "";
    [Required, MaxLength(150), Display(Name = "Nombre completo")] public string NombreCompleto { get; set; } = "";
    [MaxLength(20), Display(Name = "Teléfono")] public string? Telefono { get; set; }
    [Required, MaxLength(250), Display(Name = "Dirección de entrega")] public string Direccion { get; set; } = "";
    public bool DatosCompletos => FichaCliente.DatosCompletos(Telefono, Direccion);
}

public class ImportarFichaVM
{
    public int LoteId { get; set; }
    public IFormFile? Archivo { get; set; }
    public List<SelectListItem> Lotes { get; set; } = new();
    public List<string> Errores { get; set; } = new();
}