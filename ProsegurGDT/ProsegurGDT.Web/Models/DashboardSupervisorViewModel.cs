using ProsegurGDT.Web.Domain;
namespace ProsegurGDT.Web.Models;

public class DashboardSupervisorViewModel
{
    public int IntentosAgotados { get; set; }
    public int[] PorEtapa { get; set; } = new int[5];
    public List<FilaLote> LotesRecientes { get; set; } = new();
}

public record FilaLote(string Remito, string Banco, DateOnly Recibido, int Tarjetas, EstadoLote Estado);