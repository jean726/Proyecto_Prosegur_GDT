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

// ---------- CU18 — Reporte de densidad de entregas ----------

public class LoteDensidadFilaVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public int ZonasPlanilla { get; set; }
    public int? ReporteId { get; set; }
    public EstadoReporteDensidad? EstadoReporte { get; set; }
    public DateTime? FechaReporte { get; set; }
}

public class ZonaDensidadVM
{
    public int ZonaId { get; set; }
    public string ZonaNombre { get; set; } = string.Empty;
    public int CantidadTarjetas { get; set; }
    public int CantidadNoClasificables { get; set; }
    public bool TieneAlerta { get; set; }
    public TipoRepartidorRequerido TipoRepartidorRequerido { get; set; }
}

public class ReporteDensidadVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public int? ReporteId { get; set; }
    public EstadoReporteDensidad? Estado { get; set; }
    public string GeneradoPor { get; set; } = "—";
    public DateTime? FechaGeneracion { get; set; }
    public string? RevisadoPor { get; set; }
    public DateTime? FechaRevision { get; set; }
    public List<ZonaDensidadVM> Zonas { get; set; } = new();
    // Motivo por el que aún no se puede generar el reporte (solo si no existe)
    public string? Bloqueo { get; set; }

    public bool Existe => ReporteId is not null;
    public int TotalTarjetas => Zonas.Sum(z => z.CantidadTarjetas);
    public int TotalNoClasificables => Zonas.Sum(z => z.CantidadNoClasificables);
    public int ZonasConAlerta => Zonas.Count(z => z.TieneAlerta);
}

// ---------- CU19 — Hoja de asignación a mensajeros ----------

public enum AccionZonaAsignacion { Asignar = 1, Reagrupar, Postergar }

public class LoteAsignacionFilaVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public int ReporteId { get; set; }
    public EstadoReporteDensidad EstadoReporte { get; set; }
    public int ZonasReporte { get; set; }
    public int? HojaId { get; set; }
    public DateTime? FechaHoja { get; set; }
}

public class RepartidorDisponibleVM
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoRepartidor Tipo { get; set; }
    public string? ZonaHabitual { get; set; }
    public int Capacidad { get; set; }
    public int AsignadasHoy { get; set; }
    public string Etiqueta => $"{Nombre} · {Tipo.Texto()} · capacidad {Capacidad} · {AsignadasHoy} hoy";
}

public class ZonaAsignacionVM
{
    public int ZonaId { get; set; }
    public string ZonaNombre { get; set; } = string.Empty;
    public int CantidadReporte { get; set; }
    public int Asignables { get; set; }          // tarjetas con cita programada: las únicas que entran a una ruta
    public bool TieneAlerta { get; set; }
    public TipoRepartidorRequerido TipoRequerido { get; set; }
    public int OrdenVisita { get; set; }
    public AccionZonaAsignacion Accion { get; set; } = AccionZonaAsignacion.Asignar;
    public int? RepartidorId { get; set; }
    public int? DestinoZonaId { get; set; }
}

public class GenerarHojaVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public DateTime FechaJornada { get; set; }
    public EstadoReporteDensidad? EstadoReporte { get; set; }
    public bool ConfirmarAdvertencias { get; set; }
    public List<ZonaAsignacionVM> Zonas { get; set; } = new();

    // Solo para mostrar (no se enlazan desde el formulario)
    public List<RepartidorDisponibleVM> Repartidores { get; set; } = new();
    public string? Bloqueo { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}

public class TarjetaRutaVM
{
    public string Codigo { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
}

public class RutaHojaVM
{
    public int Orden { get; set; }
    public string Repartidor { get; set; } = string.Empty;
    public TipoRepartidor TipoRepartidor { get; set; }
    public string Sector { get; set; } = string.Empty;
    public int CantidadTarjetas { get; set; }
    public EstadoAsignacionRuta Estado { get; set; }
    public List<TarjetaRutaVM> Tarjetas { get; set; } = new();
}

public class DetalleHojaVM
{
    public int HojaId { get; set; }
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public string GeneradaPor { get; set; } = "—";
    public DateTime FechaHoraEmision { get; set; }
    public EstadoHojaAsignacion Estado { get; set; }
    public int TotalTarjetas { get; set; }
    public int TotalRutas { get; set; }
    public List<RutaHojaVM> Rutas { get; set; } = new();
    public List<(string Zona, int Cantidad)> Postergadas { get; set; } = new();
}

// ---------- CU20 — Conciliación de tarjetas ----------

public class LoteConciliacionFilaVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public int HojaId { get; set; }
    public int TotalHoja { get; set; }
    public int? ConciliacionId { get; set; }
    public EstadoConciliacion? Estado { get; set; }
    public DateTime? FechaVerificacion { get; set; }
    public int Incidencias { get; set; }

    public bool Pendiente => Estado is null;
    public bool PuedeReconciliar => Estado == EstadoConciliacion.ConFaltantes;
}

public class VerificarConciliacionVM
{
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }

    [Required(ErrorMessage = "Ingrese los códigos de las tarjetas contadas físicamente.")]
    public string? CodigosFisicos { get; set; }

    // Solo para mostrar (no se enlazan desde el formulario)
    public int CantidadFicha { get; set; }
    public int CantidadPlanilla { get; set; }
    public int CantidadHoja { get; set; }
    public List<string> CodigosEsperados { get; set; } = new();
    public string? Bloqueo { get; set; }
    public List<string> Errores { get; set; } = new();
}

public class IncidenciaConciliacionVM
{
    public string Codigo { get; set; } = "—";
    public string Cliente { get; set; } = "—";
    public TipoIncidencia Tipo { get; set; }
    public EstadoIncidencia Estado { get; set; }
    public string? Ubicacion { get; set; }
}

public class DetalleConciliacionVM
{
    public int ConciliacionId { get; set; }
    public int LoteId { get; set; }
    public string? CodigoHojaControl { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public int HojaId { get; set; }
    public string VerificadaPor { get; set; } = "—";
    public DateTime FechaVerificacion { get; set; }
    public int CantidadFicha { get; set; }
    public int CantidadPlanilla { get; set; }
    public int CantidadHoja { get; set; }
    public int CantidadFisica { get; set; }
    public int Faltantes { get; set; }
    public int Duplicados { get; set; }
    public int Discrepancias { get; set; }
    public EstadoConciliacion Estado { get; set; }
    public bool EsUltima { get; set; }
    public List<IncidenciaConciliacionVM> Incidencias { get; set; } = new();
}

// ---------- CU21 — Localización de tarjetas faltantes ----------

public class FaltanteFilaVM
{
    public int IncidenciaId { get; set; }
    public string Codigo { get; set; } = "—";
    public string Cliente { get; set; } = "—";
    public string? LoteHoja { get; set; }
    public string? Remito { get; set; }
    public EstadoIncidencia Estado { get; set; }
    public string? Ubicacion { get; set; }
}

public class HistorialFaltanteVM
{
    public DateTime FechaHora { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}

public class LocalizarFaltanteVM
{
    public int IncidenciaId { get; set; }
    public string Codigo { get; set; } = "—";
    public string Cliente { get; set; } = "—";
    public string? LoteHoja { get; set; }
    public string? Remito { get; set; }
    public string? Banco { get; set; }
    public EstadoTarjeta EstadoTarjeta { get; set; }
    public EstadoIncidencia Estado { get; set; }
    public string? UbicacionRegistrada { get; set; }
    public string? UbicacionBoveda { get; set; }
    public string? EstadoFisicoBoveda { get; set; }
    public List<HistorialFaltanteVM> Historial { get; set; } = new();

    [Required(ErrorMessage = "Ingrese la ubicación donde se encontró la tarjeta.")]
    [MaxLength(120, ErrorMessage = "La ubicación no puede superar 120 caracteres.")]
    public string? Ubicacion { get; set; }
}
