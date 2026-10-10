using System.ComponentModel.DataAnnotations;
namespace ProsegurGDT.Web.Models;

public class UsuarioFilaVM
{
    public string Id { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool Activo { get; set; }
    public bool Bloqueado { get; set; }
}

public class UsuarioCrearVM
{
    [Required, MaxLength(150), Display(Name = "Nombre completo")] public string NombreCompleto { get; set; } = "";
    [Required, MaxLength(20), Display(Name = "Documento de identidad")] public string DocumentoIdentidad { get; set; } = "";
    [Required, EmailAddress, Display(Name = "Correo (será el usuario de acceso)")] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "Contraseña inicial")] public string Password { get; set; } = "";
    [Required, Display(Name = "Rol operativo")] public string Rol { get; set; } = "";
}

public class UsuarioEditarVM
{
    public string Id { get; set; } = "";
    [Required, MaxLength(150), Display(Name = "Nombre completo")] public string NombreCompleto { get; set; } = "";
    [Required, MaxLength(20), Display(Name = "Documento de identidad")] public string DocumentoIdentidad { get; set; } = "";
    [Required, EmailAddress, Display(Name = "Correo (usuario de acceso)")] public string Email { get; set; } = "";
    [DataType(DataType.Password), Display(Name = "Nueva contraseña (vacío = no cambiar)")] public string? NuevaPassword { get; set; }
    [Required, Display(Name = "Rol operativo")] public string Rol { get; set; } = "";
}