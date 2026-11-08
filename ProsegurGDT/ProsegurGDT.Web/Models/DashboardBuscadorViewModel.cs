namespace ProsegurGDT.Web.Models;

public class DashboardBuscadorViewModel
{
    public int AlertasPendientes { get; set; }
    public int DireccionesPorValidar { get; set; }
    public int DireccionesValidadas { get; set; }
    public List<FilaAlertaCritica> AlertasCriticas { get; set; } = new();
}

public record FilaAlertaCritica(string Tarjeta, string Direccion, double Puntaje);