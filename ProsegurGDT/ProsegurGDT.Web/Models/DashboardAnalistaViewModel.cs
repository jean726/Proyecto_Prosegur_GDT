namespace ProsegurGDT.Web.Models;

public record FilaReportePendiente(int LoteId, string? HojaControl, string? Remito, int Zonas, string Estado);
public record FilaRepartidorCarga(string Nombre, string Tipo, int Capacidad, int Asignadas);

public class DashboardAnalistaViewModel
{
    public int ReportesPendientes { get; set; }
    public int HojasHoy { get; set; }
    public int TarjetasEnRutaHoy { get; set; }
    public int RepartidoresActivos { get; set; }
    public List<FilaReportePendiente> Pendientes { get; set; } = new();
    public List<FilaRepartidorCarga> Cargas { get; set; } = new();
}
