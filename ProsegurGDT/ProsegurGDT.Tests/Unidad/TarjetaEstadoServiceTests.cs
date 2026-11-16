using ProsegurGDT.Tests.Apoyo;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Unidad;

/// <summary>
/// Máquina de estados de la tarjeta (RN-03): el estado solo cambia por <see cref="ITarjetaEstadoService"/>.
/// La tabla de abajo es la especificación; si alguien cambia las transiciones del servicio, esta prueba avisa.
/// </summary>
public class TarjetaEstadoServiceTests
{
    private static readonly Dictionary<EstadoTarjeta, EstadoTarjeta[]> Especificacion = new()
    {
        [EstadoTarjeta.IngresadaEnCustodia] = [EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.PendienteDeCita, EstadoTarjeta.CitaProgramada, EstadoTarjeta.IntentosAgotados, EstadoTarjeta.NoClasificable],
        [EstadoTarjeta.PendienteDeContacto] = [EstadoTarjeta.PendienteDeCita, EstadoTarjeta.CitaProgramada, EstadoTarjeta.IntentosAgotados, EstadoTarjeta.NoClasificable],
        [EstadoTarjeta.PendienteDeCita] = [EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.CitaProgramada, EstadoTarjeta.IntentosAgotados, EstadoTarjeta.NoClasificable],
        [EstadoTarjeta.IntentosAgotados] = [EstadoTarjeta.PendienteDeContacto],
        [EstadoTarjeta.CitaProgramada] = [EstadoTarjeta.AsignadaARuta, EstadoTarjeta.PendienteDeAsignacion, EstadoTarjeta.NoClasificable],
        [EstadoTarjeta.PendienteDeAsignacion] = [EstadoTarjeta.AsignadaARuta],
        [EstadoTarjeta.NoClasificable] = [EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.PendienteDeAsignacion],
        [EstadoTarjeta.AsignadaARuta] = [EstadoTarjeta.Entregada, EstadoTarjeta.Devuelta, EstadoTarjeta.EnCustodia, EstadoTarjeta.ConIncidencia, EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.ExtraviadaConIndiciosDeFraude, EstadoTarjeta.PendienteDeAsignacion],
        [EstadoTarjeta.ConIncidencia] = [EstadoTarjeta.Devuelta, EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.ExtraviadaConIndiciosDeFraude, EstadoTarjeta.PendienteDeAsignacion],
        [EstadoTarjeta.Devuelta] = [EstadoTarjeta.EnCustodia, EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.ApartadaPorFraude],
        [EstadoTarjeta.EnCustodia] = [EstadoTarjeta.PendienteDeContacto, EstadoTarjeta.ApartadaPorFraude, EstadoTarjeta.PendienteDeAsignacion],
        [EstadoTarjeta.ApartadaPorFraude] = [EstadoTarjeta.Devuelta, EstadoTarjeta.EnCustodia],
        [EstadoTarjeta.ExtraviadaConIndiciosDeFraude] = [EstadoTarjeta.EnCustodia],
        [EstadoTarjeta.Entregada] = [],
    };

    public static IEnumerable<object[]> TodosLosPares() =>
        from origen in Enum.GetValues<EstadoTarjeta>()
        from destino in Enum.GetValues<EstadoTarjeta>()
        where origen != destino
        select new object[] { origen, destino, Especificacion[origen].Contains(destino) };

    [Fact]
    public void La_especificacion_cubre_los_14_estados()
    {
        Assert.Equal(14, Enum.GetValues<EstadoTarjeta>().Length);
        Assert.Equal(Enum.GetValues<EstadoTarjeta>().Order(), Especificacion.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(TodosLosPares))]
    public void Cada_par_de_estados_se_permite_o_se_rechaza_segun_la_especificacion(EstadoTarjeta origen, EstadoTarjeta destino, bool permitida)
    {
        using var db = BaseEnMemoria.Crear();
        var servicio = new TarjetaEstadoService(db);
        var tarjeta = new Tarjeta { Codigo = "T-1", Estado = origen };

        if (permitida)
        {
            servicio.Cambiar(tarjeta, destino, BaseEnMemoria.UsuarioPrueba, "prueba");
            Assert.Equal(destino, tarjeta.Estado);
        }
        else
        {
            var ex = Assert.Throws<InvalidOperationException>(() => servicio.Cambiar(tarjeta, destino, BaseEnMemoria.UsuarioPrueba));
            Assert.Contains("no permitida", ex.Message);
            Assert.Equal(origen, tarjeta.Estado);
            Assert.Empty(db.ChangeTracker.Entries<HistorialEstadoTarjeta>());
        }
    }

    [Fact]
    public void Un_cambio_valido_deja_historial_con_usuario_estados_y_observacion()
    {
        using var db = BaseEnMemoria.Crear();
        var tarjeta = db.AgregarTarjeta(db.AgregarLote(), "T-HIST", "Av. Larco 123", EstadoTarjeta.CitaProgramada);
        var antes = tarjeta.FechaActualizacion;

        new TarjetaEstadoService(db).Cambiar(tarjeta, EstadoTarjeta.AsignadaARuta, BaseEnMemoria.UsuarioPrueba, "Asignada a la ruta 3");
        db.SaveChanges();

        var h = Assert.Single(db.HistorialEstados);
        Assert.Equal(EstadoTarjeta.CitaProgramada, h.EstadoAnterior);
        Assert.Equal(EstadoTarjeta.AsignadaARuta, h.EstadoNuevo);
        Assert.Equal(BaseEnMemoria.UsuarioPrueba, h.UsuarioId);
        Assert.Equal("Asignada a la ruta 3", h.Observacion);
        Assert.Equal(tarjeta.Id, h.TarjetaId);
        Assert.True(tarjeta.FechaActualizacion >= antes);
    }

    [Fact]
    public void Cambiar_al_mismo_estado_no_hace_nada()
    {
        using var db = BaseEnMemoria.Crear();
        var tarjeta = new Tarjeta { Codigo = "T-2", Estado = EstadoTarjeta.Entregada };

        new TarjetaEstadoService(db).Cambiar(tarjeta, EstadoTarjeta.Entregada, BaseEnMemoria.UsuarioPrueba);

        Assert.Empty(db.ChangeTracker.Entries<HistorialEstadoTarjeta>());
    }

    [Fact]
    public void Entregada_es_un_estado_final()
    {
        using var db = BaseEnMemoria.Crear();
        var servicio = new TarjetaEstadoService(db);
        foreach (var destino in Enum.GetValues<EstadoTarjeta>().Where(e => e != EstadoTarjeta.Entregada))
            Assert.Throws<InvalidOperationException>(() =>
                servicio.Cambiar(new Tarjeta { Codigo = "T-3", Estado = EstadoTarjeta.Entregada }, destino, BaseEnMemoria.UsuarioPrueba));
    }

    [Fact]
    public void Todos_los_estados_son_alcanzables_desde_el_ingreso_en_custodia()
    {
        var alcanzados = new HashSet<EstadoTarjeta> { EstadoTarjeta.IngresadaEnCustodia };
        var cola = new Queue<EstadoTarjeta>(alcanzados);
        while (cola.Count > 0)
            foreach (var d in Especificacion[cola.Dequeue()])
                if (alcanzados.Add(d)) cola.Enqueue(d);

        Assert.Equal(Enum.GetValues<EstadoTarjeta>().Length, alcanzados.Count);
    }
}
