using System.ComponentModel.DataAnnotations;
namespace ProsegurGDT.Web.Domain;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime FechaHoraUtc { get; set; }
    [MaxLength(450)] public string? UsuarioId { get; set; }
    [MaxLength(150)] public string UsuarioNombre { get; set; } = string.Empty;
    [Required, MaxLength(60)] public string Accion { get; set; } = string.Empty;
    [MaxLength(60)] public string? Entidad { get; set; }
    [MaxLength(60)] public string? EntidadId { get; set; }
    [MaxLength(500)] public string? Detalle { get; set; }
}