namespace ProsegurGDT.Web.Models;

public class DashboardTelefonistaViewModel
{
    public int PendientesContacto { get; set; }
    public int IntentosAgotados { get; set; }
    public int CitasProgramadas { get; set; }
    public List<FilaCita> ProximasCitas { get; set; } = new();
}

public record FilaCita(string Tarjeta, string Cliente, DateOnly Fecha, TimeOnly Hora);