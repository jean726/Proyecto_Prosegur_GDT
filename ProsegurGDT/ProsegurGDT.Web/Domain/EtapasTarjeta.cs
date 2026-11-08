namespace ProsegurGDT.Web.Domain;

public static class EtapasTarjeta
{
    public static readonly string[] Nombres = { "Ingreso", "Contacto", "Cita", "Ruta", "Resultado" };

    public static int Indice(EstadoTarjeta estado) => estado switch
    {
        EstadoTarjeta.IngresadaEnCustodia or EstadoTarjeta.NoClasificable => 0,
        EstadoTarjeta.PendienteDeContacto or EstadoTarjeta.IntentosAgotados or EstadoTarjeta.PendienteDeCita => 1,
        EstadoTarjeta.CitaProgramada => 2,
        EstadoTarjeta.AsignadaARuta or EstadoTarjeta.PendienteDeAsignacion => 3,
        _ => 4
    };
}