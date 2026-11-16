using System.Globalization;

namespace ProsegurGDT.Movil.Models;

// Textos para mostrar (equivalente móvil de EnumExt.Texto() de la web) y formato de fechas en español.
public static class Textos
{
    public static readonly CultureInfo Es = new("es-PE");

    public static string Tipo(TipoRepartidor t) => t == TipoRepartidor.Motorizado ? "Motorizado" : "Caminante";

    public static string Motivo(MotivoNoEntrega m) => m switch
    {
        MotivoNoEntrega.ClienteAusente => "Cliente ausente",
        MotivoNoEntrega.DireccionIncorrecta => "Dirección incorrecta",
        MotivoNoEntrega.RechazadaPorElCliente => "Rechazada por el cliente",
        MotivoNoEntrega.ClienteNoIdentificado => "Cliente no identificado",
        _ => "Otro"
    };

    public static string Suplantacion(EstadoReporteSuplantacion e) => e switch
    {
        EstadoReporteSuplantacion.Pendiente => "Pendiente",
        EstadoReporteSuplantacion.Atendido => "Atendido",
        _ => "Descartado"
    };

    // «sáb. 15 nov.» → «Sáb 15 nov»
    public static string Jornada(DateOnly d)
    {
        var s = d.ToString("ddd d MMM", Es).Replace(".", "");
        return char.ToUpper(s[0], Es) + s[1..];
    }

    // Hora local del dispositivo (Lima) a partir de una fecha UTC de la API.
    public static string FechaHora(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM HH:mm", Es);

    public static string Hora(TimeOnly h) => h.ToString("HH:mm", Es);
}
