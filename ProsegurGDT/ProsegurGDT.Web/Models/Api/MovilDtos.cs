using System.ComponentModel.DataAnnotations;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Models.Api;

// B2 — Contratos de la API móvil /api/v1 (CU22, CU23, CU28–CU31). Fechas en UTC (ISO 8601); enums como texto.

public class SolicitudToken
{
    [Required(ErrorMessage = "Ingrese su correo."), EmailAddress(ErrorMessage = "El correo no es válido.")]
    public string Correo { get; set; } = "";
    [Required(ErrorMessage = "Ingrese su contraseña.")]
    public string Contrasena { get; set; } = "";
}

public record RepartidorDto(int Id, string Nombre, TipoRepartidor Tipo, string? ZonaHabitual);
public record TokenDto(string Token, DateTime ExpiraUtc, RepartidorDto Repartidor);

// CU28
public record HojaRutaResumenDto(int Id, DateOnly Jornada, string Zona, string? Sector, EstadoHojaRuta Estado,
    bool RecepcionConfirmada, int Tarjetas, int ConResultado);

public record CitaDto(DateOnly Fecha, TimeOnly Hora, string Lugar, string? Observaciones);
public record ResultadoCampoDto(ResultadoVisita Resultado, string? ReceptorNombre, string? ReceptorDocumento,
    MotivoNoEntrega? Motivo, string? Detalle, DateTime FechaActualizacionUtc);
public record SuplantacionDto(int Id, EstadoReporteSuplantacion Estado, DateTime FechaHoraUtc);

// Datos mínimos para entregar y verificar identidad (RN-07); no se expone el número de la tarjeta.
public record VisitaDto(int Orden, int TarjetaId, string Codigo, string Titular, string DocumentoTitular,
    string? Telefono, string Direccion, CitaDto? Cita, EstadoTarjeta EstadoTarjeta,
    ResultadoCampoDto? Resultado, SuplantacionDto? Suplantacion, bool Editable);

public record HojaRutaDto(int Id, DateOnly Jornada, string Zona, string? Sector, EstadoHojaRuta Estado,
    bool RecepcionConfirmada, DateTime? FechaConfirmacionUtc, bool DescargoIniciado, IReadOnlyList<VisitaDto> Visitas);

// CU29
public class SolicitudResultado
{
    [Required(ErrorMessage = "Indique el resultado de la visita.")]
    public ResultadoVisita? Resultado { get; set; }
    [StringLength(150, ErrorMessage = "El nombre del receptor no puede superar 150 caracteres.")]
    public string? ReceptorNombre { get; set; }
    [StringLength(20, ErrorMessage = "El documento del receptor no puede superar 20 caracteres.")]
    public string? ReceptorDocumento { get; set; }
    public MotivoNoEntrega? Motivo { get; set; }
    [StringLength(300, ErrorMessage = "El detalle no puede superar 300 caracteres.")]
    public string? Detalle { get; set; }
}

// CU30
public class SolicitudSuplantacion
{
    [Required(ErrorMessage = "Describa lo ocurrido."),
     StringLength(500, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 500 caracteres.")]
    public string Descripcion { get; set; } = "";
}

public record MensajeDto(string Mensaje);
