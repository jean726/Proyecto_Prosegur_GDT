using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Services;

public record ContextoLlamada(
    Tarjeta Tarjeta, ResultadoLlamada Resultado, string? Observaciones,
    DateTime? Fecha, string? Hora, string? Lugar,
    string UsuarioId, DateTime AhoraUtc, int IntentosNoContactoPrevios);

// Strategy: cada resultado de llamada define cómo se valida y qué efecto tiene.
public interface IManejadorResultadoLlamada
{
    bool Soporta(ResultadoLlamada resultado);
    string? Validar(ContextoLlamada ctx);   // devuelve el error o null
    string Aplicar(ContextoLlamada ctx);    // aplica el efecto y devuelve el mensaje de éxito
}

public class ManejadorCitaExitosa : IManejadorResultadoLlamada
{
    private readonly ApplicationDbContext _db;
    private readonly ITarjetaEstadoService _estados;
    public ManejadorCitaExitosa(ApplicationDbContext db, ITarjetaEstadoService estados) { _db = db; _estados = estados; }

    public bool Soporta(ResultadoLlamada r) => r == ResultadoLlamada.Exitoso;

    public string? Validar(ContextoLlamada c)
    {
        if (c.Fecha is null) return "Ingrese la fecha de entrega.";
        if (DateOnly.FromDateTime(c.Fecha.Value) < DateOnly.FromDateTime(HoraLima.Ahora)) return "La fecha de entrega no puede ser anterior a hoy.";
        if (!TimeOnly.TryParse(c.Hora, out _)) return "Ingrese una hora válida.";
        if (string.IsNullOrWhiteSpace(c.Lugar)) return "Ingrese el lugar de entrega.";
        return null;
    }

    public string Aplicar(ContextoLlamada c)
    {
        var fecha = DateOnly.FromDateTime(c.Fecha!.Value);
        var hora = TimeOnly.Parse(c.Hora!);
        var lugar = c.Lugar!.Trim();
        _db.Citas.Add(new Cita
        {
            Tarjeta = c.Tarjeta,
            FechaEntrega = fecha,
            HoraEntrega = hora,
            Lugar = lugar,
            Observaciones = c.Observaciones?.Trim(),
            RegistradaPorId = c.UsuarioId,
            FechaHoraRegistro = c.AhoraUtc
        });
        _estados.Cambiar(c.Tarjeta, EstadoTarjeta.CitaProgramada, c.UsuarioId, "Cita registrada");
        return $"Cita registrada satisfactoriamente para el cliente {c.Tarjeta.Cliente!.NombreCompleto} el {fecha:dd/MM/yyyy} a las {hora:HH\\:mm} en {lugar}.";
    }
}

public class ManejadorContactoSinAcuerdo : IManejadorResultadoLlamada
{
    private readonly ITarjetaEstadoService _estados;
    public ManejadorContactoSinAcuerdo(ITarjetaEstadoService estados) => _estados = estados;

    public bool Soporta(ResultadoLlamada r) => r == ResultadoLlamada.ContactadoSinAcuerdo;

    public string? Validar(ContextoLlamada c) =>
        string.IsNullOrWhiteSpace(c.Observaciones) ? "Las observaciones son obligatorias para este resultado." : null;

    public string Aplicar(ContextoLlamada c)
    {
        _estados.Cambiar(c.Tarjeta, EstadoTarjeta.PendienteDeCita, c.UsuarioId, "Contactado sin acuerdo");
        return "Contacto registrado sin acuerdo. La tarjeta queda pendiente de cita.";
    }
}

public class ManejadorDatosInsuficientes : IManejadorResultadoLlamada
{
    private readonly ITarjetaEstadoService _estados;
    public ManejadorDatosInsuficientes(ITarjetaEstadoService estados) => _estados = estados;

    public bool Soporta(ResultadoLlamada r) => r == ResultadoLlamada.DatosInsuficientes;

    public string? Validar(ContextoLlamada c) =>
        string.IsNullOrWhiteSpace(c.Observaciones) ? "Las observaciones son obligatorias para este resultado." : null;

    public string Aplicar(ContextoLlamada c)
    {
        _estados.Cambiar(c.Tarjeta, EstadoTarjeta.PendienteDeContacto, c.UsuarioId, "Datos de contacto insuficientes");
        return "Registrado. Comunique la situación al Supervisor para corregir la ficha.";
    }
}

public class ManejadorNoContacto : IManejadorResultadoLlamada
{
    private readonly ITarjetaEstadoService _estados;
    private readonly IConfiguracionOperativa _cfg;
    public ManejadorNoContacto(ITarjetaEstadoService estados, IConfiguracionOperativa cfg) { _estados = estados; _cfg = cfg; }

    public bool Soporta(ResultadoLlamada r) => r.EsNoContacto();

    public string? Validar(ContextoLlamada c) =>
        c.Resultado == ResultadoLlamada.Otro && string.IsNullOrWhiteSpace(c.Observaciones)
            ? "Las observaciones son obligatorias para este resultado." : null;

    public string Aplicar(ContextoLlamada c)
    {
        var max = _cfg.MaxIntentosContacto;
        var intento = c.IntentosNoContactoPrevios + 1;
        if (intento >= max)
        {
            _estados.Cambiar(c.Tarjeta, EstadoTarjeta.IntentosAgotados, c.UsuarioId, "Intentos de contacto agotados");
            return "Motivo de no contacto registrado. Intentos agotados: el caso queda pendiente de escalamiento al Supervisor.";
        }
        _estados.Cambiar(c.Tarjeta, EstadoTarjeta.PendienteDeContacto, c.UsuarioId, c.Resultado.Texto());
        return $"Motivo de no contacto registrado (intento {intento} de {max}).";
    }
}