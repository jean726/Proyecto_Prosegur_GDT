using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
namespace ProsegurGDT.Web.Services;

public interface ITarjetaEstadoService
{
    void Cambiar(Tarjeta tarjeta, EstadoTarjeta nuevo, string usuarioId, string? observacion = null);
}

public class TarjetaEstadoService : ITarjetaEstadoService
{
    private readonly ApplicationDbContext _db;
    public TarjetaEstadoService(ApplicationDbContext db) => _db = db;

    private static readonly Dictionary<EstadoTarjeta, EstadoTarjeta[]> Permitidas = new()
    {
        [EstadoTarjeta.IngresadaEnCustodia] = new[] { EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.PendienteDeCita, EstadoTarjeta.CitaProgramada, EstadoTarjeta.IntentosAgotados, EstadoTarjeta.NoClasificable },
        [EstadoTarjeta.PendienteDeContacto] = new[] { EstadoTarjeta.PendienteDeCita, EstadoTarjeta.CitaProgramada, EstadoTarjeta.IntentosAgotados, EstadoTarjeta.NoClasificable },
        [EstadoTarjeta.PendienteDeCita] = new[] { EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.CitaProgramada, EstadoTarjeta.IntentosAgotados, EstadoTarjeta.NoClasificable },
        [EstadoTarjeta.IntentosAgotados] = new[] { EstadoTarjeta.PendienteDeContacto },
        [EstadoTarjeta.CitaProgramada] = new[] { EstadoTarjeta.AsignadaARuta, EstadoTarjeta.PendienteDeAsignacion, EstadoTarjeta.NoClasificable },
        [EstadoTarjeta.PendienteDeAsignacion] = new[] { EstadoTarjeta.AsignadaARuta },
        [EstadoTarjeta.NoClasificable] = new[] { EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.PendienteDeAsignacion },
        [EstadoTarjeta.AsignadaARuta] = new[] { EstadoTarjeta.Entregada, EstadoTarjeta.Devuelta, EstadoTarjeta.EnCustodia, EstadoTarjeta.ConIncidencia, EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.ExtraviadaConIndiciosDeFraude, EstadoTarjeta.PendienteDeAsignacion },
        [EstadoTarjeta.ConIncidencia] = new[] { EstadoTarjeta.Devuelta, EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.ExtraviadaConIndiciosDeFraude, EstadoTarjeta.PendienteDeAsignacion },
        [EstadoTarjeta.Devuelta] = new[] { EstadoTarjeta.EnCustodia, EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.ApartadaPorFraude },
        [EstadoTarjeta.EnCustodia] = new[] { EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.PendienteDeAsignacion },
        [EstadoTarjeta.ApartadaPorFraude] = new[] { EstadoTarjeta.Devuelta, EstadoTarjeta.EnCustodia },
        // Fase 7: una tarjeta extraviada puede reaparecer y volver a custodia
        [EstadoTarjeta.ExtraviadaConIndiciosDeFraude] = new[] { EstadoTarjeta.EnCustodia },
        // Entregada es estado final
    };

    public void Cambiar(Tarjeta tarjeta, EstadoTarjeta nuevo, string usuarioId, string? observacion = null)
    {
        if (tarjeta.Estado == nuevo) return;
        if (!Permitidas.TryGetValue(tarjeta.Estado, out var ok) || !ok.Contains(nuevo))
            throw new InvalidOperationException($"Transición de estado no permitida: {tarjeta.Estado} → {nuevo}.");

        var anterior = tarjeta.Estado;
        tarjeta.Estado = nuevo;
        tarjeta.FechaActualizacion = DateTime.UtcNow;
        _db.HistorialEstados.Add(new HistorialEstadoTarjeta
        {
            Tarjeta = tarjeta,
            EstadoAnterior = anterior,
            EstadoNuevo = nuevo,
            UsuarioId = usuarioId,
            FechaHora = DateTime.UtcNow,
            Observacion = observacion
        });
    }
}