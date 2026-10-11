using ProsegurGDT.Web.Domain;
namespace ProsegurGDT.Web.Models;

public class GestionarCitaVM
{
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public List<RegistroLlamada> Historial { get; set; } = new();
    public bool DatosCompletos { get; set; }
    public int IntentosNoContacto { get; set; }
    public int MaxIntentos { get; set; }
    public ResultadoLlamada? Resultado { get; set; }
    public string? Observaciones { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Hora { get; set; }
    public string? Lugar { get; set; }
    public string? Error { get; set; }
}