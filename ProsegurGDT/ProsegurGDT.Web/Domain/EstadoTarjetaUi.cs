namespace ProsegurGDT.Web.Domain;

public static class EstadoTarjetaUi
{
    public static (int Paso, string Tono, string Texto) Describir(EstadoTarjeta estado)
    {
        var (tono, texto) = estado switch
        {
            EstadoTarjeta.IngresadaEnCustodia => ("custodia", "Ingresada en custodia"),
            EstadoTarjeta.NoClasificable => ("riesgo", "No clasificable"),
            EstadoTarjeta.PendienteDeContacto => ("pendiente", "Pendiente de contacto"),
            EstadoTarjeta.IntentosAgotados => ("riesgo", "Intentos agotados"),
            EstadoTarjeta.PendienteDeCita => ("pendiente", "Pendiente de cita"),
            EstadoTarjeta.CitaProgramada => ("custodia", "Cita programada"),
            EstadoTarjeta.AsignadaARuta => ("custodia", "Asignada a ruta"),
            EstadoTarjeta.PendienteDeAsignacion => ("pendiente", "Pendiente de asignación"),
            EstadoTarjeta.Entregada => ("entrega", "Entregada"),
            EstadoTarjeta.Devuelta => ("pendiente", "Devuelta"),
            EstadoTarjeta.EnCustodia => ("custodia", "En custodia"),
            EstadoTarjeta.ConIncidencia => ("riesgo", "Con incidencia"),
            EstadoTarjeta.ApartadaPorFraude => ("riesgo", "Apartada por fraude"),
            EstadoTarjeta.ExtraviadaConIndiciosDeFraude => ("riesgo", "Extraviada con indicios de fraude"),
            _ => ("pendiente", estado.ToString())
        };

        return (EtapasTarjeta.Indice(estado), tono, texto);
    }

    public static string TonoEstado(this EstadoTarjeta estado) => Describir(estado).Tono;

    public static string TextoEstado(this EstadoTarjeta estado) => Describir(estado).Texto;
}
