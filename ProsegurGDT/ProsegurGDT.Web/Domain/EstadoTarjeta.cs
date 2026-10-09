namespace ProsegurGDT.Web.Domain;

public enum EstadoTarjeta
{
    IngresadaEnCustodia = 1, PendienteDeContacto, IntentosAgotados, PendienteDeCita,
    CitaProgramada, AsignadaARuta, PendienteDeAsignacion, NoClasificable, Entregada,
    Devuelta, EnCustodia, ConIncidencia, ApartadaPorFraude, ExtraviadaConIndiciosDeFraude
}