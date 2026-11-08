namespace ProsegurGDT.Web.Models;

public class DashboardClasificadorViewModel
{
    public int PorClasificar { get; set; }
    public int YaClasificadas { get; set; }
    public int NoClasificables { get; set; }
    public List<string> CodigosNoClasificables { get; set; } = new();
}