namespace ProsegurGDT.Movil.Models;

// Copia del contrato de la API /api/v1 (ProsegurGDT.Web/Models/Api/MovilDtos.cs y enums de Domain).
// Si cambia el contrato en la web, hay que actualizar este archivo. Enums como texto; fechas en UTC.

public enum TipoRepartidor { Motorizado = 1, Caminante }
public enum EstadoHojaRuta { PendienteDeAprobacion = 1, PendienteDeCorreccion, Aprobada, Firmada }
public enum ResultadoVisita { Entregada = 1, NoEntregada }
public enum MotivoNoEntrega { ClienteAusente = 1, DireccionIncorrecta, RechazadaPorElCliente, ClienteNoIdentificado, Otro }
public enum EstadoReporteSuplantacion { Pendiente = 1, Atendido, Descartado }
public enum EstadoTarjeta
{
    IngresadaEnCustodia = 1, PendienteDeContacto, IntentosAgotados, PendienteDeCita,
    CitaProgramada, AsignadaARuta, PendienteDeAsignacion, NoClasificable, Entregada,
    Devuelta, EnCustodia, ConIncidencia, ApartadaPorFraude, ExtraviadaConIndiciosDeFraude
}

// CU22
public record SolicitudToken(string Correo, string Contrasena);
public record RepartidorDto(int Id, string Nombre, TipoRepartidor Tipo, string? ZonaHabitual);
public record TokenDto(string Token, DateTime ExpiraUtc, RepartidorDto Repartidor);

// CU28
public record HojaRutaResumenDto(int Id, DateOnly Jornada, string Zona, string? Sector, EstadoHojaRuta Estado,
    bool RecepcionConfirmada, int Tarjetas, int ConResultado);

public record CitaDto(DateOnly Fecha, TimeOnly Hora, string Lugar, string? Observaciones);
public record ResultadoCampoDto(ResultadoVisita Resultado, string? ReceptorNombre, string? ReceptorDocumento,
    MotivoNoEntrega? Motivo, string? Detalle, DateTime FechaActualizacionUtc);
public record SuplantacionDto(int Id, EstadoReporteSuplantacion Estado, DateTime FechaHoraUtc);

public record VisitaDto(int Orden, int TarjetaId, string Codigo, string Titular, string DocumentoTitular,
    string? Telefono, string Direccion, CitaDto? Cita, EstadoTarjeta EstadoTarjeta,
    ResultadoCampoDto? Resultado, SuplantacionDto? Suplantacion, bool Editable);

public record HojaRutaDto(int Id, DateOnly Jornada, string Zona, string? Sector, EstadoHojaRuta Estado,
    bool RecepcionConfirmada, DateTime? FechaConfirmacionUtc, bool DescargoIniciado, IReadOnlyList<VisitaDto> Visitas);

// CU29 y CU30
public record SolicitudResultado(ResultadoVisita Resultado, string? ReceptorNombre, string? ReceptorDocumento,
    MotivoNoEntrega? Motivo, string? Detalle);
public record SolicitudSuplantacion(string Descripcion);

// Respuestas de éxito: CU23 → { mensaje }; CU29–CU31 → { mensaje, datos }.
public record MensajeDto(string Mensaje);

// ProblemDetails / ValidationProblemDetails de la API (mensajes en español).
public class ProblemaDto
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public int? Status { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}
