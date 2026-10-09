using Microsoft.AspNetCore.Identity;

namespace ProsegurGDT.Web.Data;

public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string? DocumentoIdentidad { get; set; }
    public bool Activo { get; set; } = true;
}