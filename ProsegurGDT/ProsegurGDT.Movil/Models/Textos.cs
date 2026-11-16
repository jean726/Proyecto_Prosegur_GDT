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

    // Fechas como en el diseño: «15/11/2026» y «15/11/2026 · 08:12» (hora local del dispositivo, Lima).
    public static string Fecha(DateOnly d) => d.ToString("dd/MM/yyyy", Es);

    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    public static string FechaHora(DateTime utc) => Local(utc).ToString("dd/MM/yyyy · HH:mm", Es);

    public static string Hora(TimeOnly h) => h.ToString("HH:mm", Es);

    // «Registrado a las 10:42» si fue hoy; si no, «Registrado el 14/11/2026 · 10:42».
    public static string Registrado(DateTime utc)
    {
        var l = Local(utc);
        return l.Date == DateTime.Now.Date ? $"Registrado a las {l:HH:mm}" : $"Registrado el {FechaHora(utc)}";
    }
}
