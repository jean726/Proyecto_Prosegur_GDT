using System.ComponentModel.DataAnnotations;

namespace ProsegurGDT.Web.Domain;

public class Banco
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}