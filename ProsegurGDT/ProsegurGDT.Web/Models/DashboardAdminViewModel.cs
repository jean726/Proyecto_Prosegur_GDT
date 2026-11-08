namespace ProsegurGDT.Web.Models;

public class DashboardAdminViewModel
{
    public int Activas { get; set; }
    public int Desactivadas { get; set; }
    public int Bloqueadas { get; set; }
    public List<FilaRol> PorRol { get; set; } = new();
    public List<FilaAccion> UltimasAcciones { get; set; } = new();
}

public record FilaRol(string Rol, int Total);
public record FilaAccion(string Accion, string? Detalle, string Usuario, DateTime FechaLocal);