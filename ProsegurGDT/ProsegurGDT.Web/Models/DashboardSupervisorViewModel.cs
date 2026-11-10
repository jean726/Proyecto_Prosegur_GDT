using ProsegurGDT.Web.Domain;
namespace ProsegurGDT.Web.Models;

public class DashboardSupervisorViewModel
{
    public int IntentosAgotados { get; set; }
    // Fase 8 — Mensajería
    public int RutasSinHoja { get; set; }
    public int HojasPorAprobar { get; set; }
    public int HojasConDiscrepancia { get; set; }
    public int HojasPorFirmar { get; set; }
    public string? ReporteHoy { get; set; }       // null = aún no diseñado
    public int[] PorEtapa { get; set; } = new int[5];
    public List<FilaLote> LotesRecientes { get; set; } = new();
}

public record FilaLote(string Remito, string Banco, DateOnly Recibido, int Tarjetas, EstadoLote Estado);