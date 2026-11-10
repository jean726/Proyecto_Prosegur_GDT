using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Models;

// ---------- CU17 — Planilla de armado de ruta ----------

public class LotePlanillaFilaVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public int Tarjetas { get; set; }
    public int SinZona { get; set; }
    public int ConAlerta { get; set; }
    public int Excluidas { get; set; }
    public int? PlanillaId { get; set; }
    public DateTime? FechaPlanilla { get; set; }
    public bool TieneReporte { get; set; }
    public string? Bloqueo { get; set; }

    public bool ListaParaArmar => PlanillaId is null && Bloqueo is null;
}

public class ZonaPlanillaVM
{
    public int ZonaId { get; set; }
    public string ZonaNombre { get; set; } = string.Empty;
    public int CantidadTarjetas { get; set; }
    public TipoRepartidorRequerido TipoRepartidorRequerido { get; set; } = TipoRepartidorRequerido.Indiferente;
    public int OrdenVisita { get; set; }
    [MaxLength(300, ErrorMessage = "La observación de la zona no puede superar 300 caracteres.")]
    public string? Observacion { get; set; }
}

public class ArmarPlanillaVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public int TotalTarjetas { get; set; }
    public int SinZona { get; set; }
    public int ConAlerta { get; set; }
    public int Excluidas { get; set; }
    public int? PlanillaId { get; set; }
    [MaxLength(300, ErrorMessage = "La observación no puede superar 300 caracteres.")]
    public string? Observacion { get; set; }
    public List<ZonaPlanillaVM> Zonas { get; set; } = new();

    // Solo para mostrar (no se enlazan desde el formulario)
    public string? Bloqueo { get; set; }
    public List<string> Errores { get; set; } = new();
}

public class DetallePlanillaVM
{
    public int PlanillaId { get; set; }
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string GeneradaPor { get; set; } = "—";
    public DateTime FechaGeneracion { get; set; }
    public EstadoPlanilla Estado { get; set; }
    public string? Observacion { get; set; }
    public int Excluidas { get; set; }
    public bool TieneReporte { get; set; }
    public List<ZonaPlanillaVM> Zonas { get; set; } = new();
    public int TotalTarjetas => Zonas.Sum(z => z.CantidadTarjetas);
}
