using System.ComponentModel.DataAnnotations;

namespace ProsegurGDT.Web.Domain;

public class Cliente
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string NombreCompleto { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string DocumentoIdentidad { get; set; } = string.Empty;
    [MaxLength(20)] public string? Telefono { get; set; }
    [Required, MaxLength(250)] public string Direccion { get; set; } = string.Empty;
    [MaxLength(250)] public string? DireccionNormalizada { get; set; }
}