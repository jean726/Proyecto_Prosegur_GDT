using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Domain;

// Fase 7 — Clasificación: planilla, densidad, hoja de asignación, conciliación y bóveda.

public enum TipoRepartidor { Motorizado = 1, Caminante }
public enum TipoRepartidorRequerido { Indiferente = 1, Motorizado, Caminante }
public enum EstadoPlanilla { EnArmado = 1, Generada }
public enum EstadoReporteDensidad { Generado = 1, Revisado }
public enum EstadoHojaAsignacion { Emitida = 1, Anulada }
public enum EstadoAsignacionRuta { Asignada = 1, Reagrupada, Postergada }
public enum EstadoConciliacion { Consistente = 1, ConFaltantes }
public enum TipoIncidencia { Faltante = 1, Duplicado, Discrepancia }
public enum EstadoIncidencia { Abierta = 1, Localizada, NoLocalizada }

public class Repartidor
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Nombre { get; set; } = string.Empty;
    public TipoRepartidor Tipo { get; set; }
    public int? ZonaHabitualId { get; set; }
    public Zona? ZonaHabitual { get; set; }
    public int CapacidadTarjetas { get; set; } = 20;
    public bool Activo { get; set; } = true;
    // B2: cuenta de Identity con rol Repartidor que usa la app móvil (opcional, única)
    public string? UsuarioId { get; set; }
    public ApplicationUser? Usuario { get; set; }
    public ICollection<AsignacionRuta> Asignaciones { get; set; } = new List<AsignacionRuta>();
}

public class PlanillaArmado
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public Lote? Lote { get; set; }
    public string GeneradaPorId { get; set; } = string.Empty;
    public ApplicationUser? GeneradaPor { get; set; }
    public DateTime FechaGeneracion { get; set; }
    public EstadoPlanilla Estado { get; set; }
    [MaxLength(300)] public string? Observacion { get; set; }
    public ICollection<PlanillaZona> Zonas { get; set; } = new List<PlanillaZona>();
}

public class PlanillaZona
{
    public int Id { get; set; }
    public int PlanillaArmadoId { get; set; }
    public PlanillaArmado? PlanillaArmado { get; set; }
    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }
    public TipoRepartidorRequerido TipoRepartidorRequerido { get; set; }
    public int OrdenVisita { get; set; }
    [MaxLength(300)] public string? Observacion { get; set; }
}

public class ReporteDensidad
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public Lote? Lote { get; set; }
    public int PlanillaArmadoId { get; set; }
    public PlanillaArmado? PlanillaArmado { get; set; }
    public string GeneradoPorId { get; set; } = string.Empty;
    public ApplicationUser? GeneradoPor { get; set; }
    public DateTime FechaGeneracion { get; set; }
    public string? RevisadoPorId { get; set; }
    public ApplicationUser? RevisadoPor { get; set; }
    public DateTime? FechaRevision { get; set; }
    public EstadoReporteDensidad Estado { get; set; }
    public ICollection<ReporteDensidadZona> Zonas { get; set; } = new List<ReporteDensidadZona>();
}

public class ReporteDensidadZona
{
    public int Id { get; set; }
    public int ReporteDensidadId { get; set; }
    public ReporteDensidad? ReporteDensidad { get; set; }
    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }
    public int CantidadTarjetas { get; set; }
    public int CantidadNoClasificables { get; set; }
    public bool TieneAlerta { get; set; }
    public TipoRepartidorRequerido TipoRepartidorRequerido { get; set; }
}

public class HojaAsignacion
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public Lote? Lote { get; set; }
    public int ReporteDensidadId { get; set; }
    public ReporteDensidad? ReporteDensidad { get; set; }
    public string GeneradaPorId { get; set; } = string.Empty;
    public ApplicationUser? GeneradaPor { get; set; }
    public DateTime FechaHoraEmision { get; set; }
    public EstadoHojaAsignacion Estado { get; set; }
    public int TotalTarjetas { get; set; }
    public int TotalRutas { get; set; }
    public ICollection<AsignacionRuta> Rutas { get; set; } = new List<AsignacionRuta>();
}

public class AsignacionRuta
{
    public int Id { get; set; }
    public int HojaAsignacionId { get; set; }
    public HojaAsignacion? HojaAsignacion { get; set; }
    public int RepartidorId { get; set; }
    public Repartidor? Repartidor { get; set; }
    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }
    [MaxLength(150)] public string? SectorCobertura { get; set; }
    public int CantidadTarjetas { get; set; }
    public int OrdenVisita { get; set; }
    public EstadoAsignacionRuta Estado { get; set; }
    public ICollection<AsignacionTarjeta> Tarjetas { get; set; } = new List<AsignacionTarjeta>();
}

public class AsignacionTarjeta
{
    public int Id { get; set; }
    public int AsignacionRutaId { get; set; }
    public AsignacionRuta? AsignacionRuta { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public DateTime FechaAsignacion { get; set; }
}

public class ConciliacionLote
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public Lote? Lote { get; set; }
    public int HojaAsignacionId { get; set; }
    public HojaAsignacion? HojaAsignacion { get; set; }
    public string VerificadaPorId { get; set; } = string.Empty;
    public ApplicationUser? VerificadaPor { get; set; }
    public DateTime FechaVerificacion { get; set; }
    public int CantidadFicha { get; set; }
    public int CantidadPlanilla { get; set; }
    public int CantidadHoja { get; set; }
    public int CantidadFisica { get; set; }
    public int Faltantes { get; set; }
    public int Duplicados { get; set; }
    public EstadoConciliacion Estado { get; set; }
    public ICollection<IncidenciaConciliacion> Incidencias { get; set; } = new List<IncidenciaConciliacion>();
}

public class IncidenciaConciliacion
{
    public int Id { get; set; }
    public int ConciliacionLoteId { get; set; }
    public ConciliacionLote? ConciliacionLote { get; set; }
    public int? TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    public TipoIncidencia Tipo { get; set; }
    public EstadoIncidencia Estado { get; set; }
    public string? BuscadorId { get; set; }
    public ApplicationUser? Buscador { get; set; }
    [MaxLength(200)] public string? Ubicacion { get; set; }
    public DateTime? FechaResolucion { get; set; }
}

public class InventarioBoveda
{
    public int Id { get; set; }
    public int TarjetaId { get; set; }
    public Tarjeta? Tarjeta { get; set; }
    [Required, MaxLength(120)] public string Ubicacion { get; set; } = string.Empty;
    [MaxLength(40)] public string EstadoFisico { get; set; } = "En custodia";
    public DateTime FechaActualizacion { get; set; }
}
