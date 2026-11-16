using Microsoft.Extensions.Configuration;
using ProsegurGDT.Tests.Apoyo;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Servicios;

/// <summary>
/// Motor de alertas de direcciones (CU15, CU16) con el motor hash y sus umbrales 0.60 / 0.90:
/// qué alerta, con qué nivel, y que no duplica ni deja alertas obsoletas.
/// </summary>
public class AddressRiskServiceTests
{
    private static AddressRiskService Crear(ApplicationDbContext db, double medio = 0.60, double alto = 0.90)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IA:UmbralMedio"] = medio.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["IA:UmbralAlto"] = alto.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        return new AddressRiskService(db, new HashingNgramEmbeddingService(), new ConfiguracionOperativa(cfg));
    }

    [Fact]
    public async Task Una_direccion_validada_queda_indexada_con_la_version_del_motor()
    {
        using var db = BaseEnMemoria.Crear();
        var d = await Crear(db).AgregarAsync("  Av. Larco 105, Miraflores ", " Domicilio ficticio ", "u1", validada: true, origen: "Prueba");

        Assert.Equal(EstadoDireccion.Validada, d.Estado);
        Assert.Equal("Av. Larco 105, Miraflores", d.DireccionOriginal);
        Assert.Equal("AVENIDA LARCO 105 MIRAFLORES", d.DireccionNormalizada);
        Assert.Equal("Domicilio ficticio", d.Motivo);
        Assert.Equal("ngram-hash-v1", d.ModeloVersion);
        Assert.Equal(512 * sizeof(float), d.Vector!.Length);
        Assert.Equal("u1", d.ValidadaPorId);
        Assert.NotNull(d.FechaValidacion);
    }

    [Fact]
    public async Task Una_direccion_propuesta_queda_pendiente_y_no_participa_en_la_busqueda()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        var d = await riesgo.AgregarAsync("Av. Larco 105, Miraflores", "Sospecha del clasificador", "u1", validada: false, origen: null);

        Assert.Equal(EstadoDireccion.Pendiente, d.Estado);
        Assert.Null(d.Vector);
        Assert.Empty(await riesgo.BuscarAsync("Av. Larco 105, Miraflores"));

        await riesgo.ValidarAsync(d, "buscador");

        var c = Assert.Single(await riesgo.BuscarAsync("Av. Larco 105, Miraflores"));
        Assert.True(c.Exacta);
        Assert.Equal(1.0, c.Puntaje);
        Assert.Equal("buscador", d.ValidadaPorId);
    }

    [Fact]
    public async Task La_busqueda_ordena_por_puntaje_y_respeta_el_maximo()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        await riesgo.AgregarAsync("Jr. Huallaga 300, Lima Cercado", "m", "u", true, null);
        await riesgo.AgregarAsync("Av. Larco 105, Miraflores", "m", "u", true, null);
        await riesgo.AgregarAsync("Av. Larko 105, Miraflores", "m", "u", true, null);

        var r = await riesgo.BuscarAsync("avenida larco 105 miraflores", max: 2);

        Assert.Equal(2, r.Count);
        Assert.Equal("Av. Larco 105, Miraflores", r[0].Direccion);
        Assert.True(r[0].Exacta);
        Assert.Equal("Av. Larko 105, Miraflores", r[1].Direccion);
        Assert.False(r[1].Exacta);
        Assert.True(r[0].Puntaje >= r[1].Puntaje);
    }

    [Fact]
    public async Task Una_numeracion_distinta_penaliza_el_puntaje()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        await riesgo.AgregarAsync("Calle Berlin 117, Miraflores", "m", "u", true, null);

        var otraNumeracion = Assert.Single(await riesgo.BuscarAsync("Calle Berlin 119, Miraflores"));
        var mismaNumeracion = Assert.Single(await riesgo.BuscarAsync("Calle Berlim 117, Miraflores"));

        Assert.Contains("distinta (penalizada)", otraNumeracion.Detalle);
        Assert.Contains("numeración igual", mismaNumeracion.Detalle);
        Assert.True(otraNumeracion.Puntaje <= 0.85, $"con otra numeración el puntaje no puede pasar de 0.85: {otraNumeracion.Puntaje}");
    }

    [Fact]
    public async Task Una_busqueda_vacia_no_devuelve_nada()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        await riesgo.AgregarAsync("Av. Larco 105", "m", "u", true, null);
        Assert.Empty(await riesgo.BuscarAsync("  ,. "));
    }

    [Fact]
    public async Task Evaluar_el_lote_alerta_la_exacta_como_alta_y_no_alerta_la_de_otra_zona()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        await riesgo.AgregarAsync("Av. Benavides 108, Miraflores", "m", "u", true, null);
        var lote = db.AgregarLote();
        var exacta = db.AgregarTarjeta(lote, "T-1", "AVENIDA BENAVIDES 108 MIRAFLORES");
        var limpia = db.AgregarTarjeta(lote, "T-2", "Jr. Huallaga 300, Lima Cercado");

        var nuevas = await riesgo.EvaluarLoteAsync(lote.Id);

        Assert.Equal(1, nuevas);
        var alerta = Assert.Single(db.Alertas);
        Assert.Equal(exacta.Id, alerta.TarjetaId);
        Assert.Equal(NivelAlerta.Alta, alerta.Nivel);
        Assert.Equal("Exacta", alerta.Metodo);
        Assert.Equal(1.0, alerta.Puntaje);
        Assert.Equal(DecisionAlerta.Pendiente, alerta.Decision);
        Assert.DoesNotContain(db.Alertas, a => a.TarjetaId == limpia.Id);
    }

    [Fact]
    public async Task Una_coincidencia_parcial_alerta_con_nivel_medio_y_el_metodo_del_motor()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        await riesgo.AgregarAsync("Av. Larco 105, Miraflores", "m", "u", true, null);
        var lote = db.AgregarLote();
        db.AgregarTarjeta(lote, "T-1", "Av. Larko 105, Miraflores");

        await riesgo.EvaluarLoteAsync(lote.Id);

        var alerta = Assert.Single(db.Alertas);
        Assert.InRange(alerta.Puntaje, 0.60, 0.8999);
        Assert.Equal(NivelAlerta.Media, alerta.Nivel);
        Assert.Equal("ngram-hash-v1", alerta.Metodo);
    }

    [Fact]
    public async Task Evaluar_dos_veces_no_duplica_alertas()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        await riesgo.AgregarAsync("Av. Benavides 108, Miraflores", "m", "u", true, null);
        var lote = db.AgregarLote();
        db.AgregarTarjeta(lote, "T-1", "Av. Benavides 108, Miraflores");

        Assert.Equal(1, await riesgo.EvaluarLoteAsync(lote.Id));
        Assert.Equal(0, await riesgo.EvaluarLoteAsync(lote.Id));
        Assert.Single(db.Alertas);
    }

    [Theory]
    [InlineData(EstadoTarjeta.AsignadaARuta)]
    [InlineData(EstadoTarjeta.Entregada)]
    [InlineData(EstadoTarjeta.NoClasificable)]
    [InlineData(EstadoTarjeta.IntentosAgotados)]
    public async Task Solo_se_evaluan_las_tarjetas_que_aun_pueden_clasificarse(EstadoTarjeta estado)
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        await riesgo.AgregarAsync("Av. Benavides 108, Miraflores", "m", "u", true, null);
        var lote = db.AgregarLote();
        db.AgregarTarjeta(lote, "T-1", "Av. Benavides 108, Miraflores", estado);

        Assert.Equal(0, await riesgo.EvaluarLoteAsync(lote.Id));
        Assert.Empty(db.Alertas);
    }

    [Fact]
    public async Task Sin_direcciones_validadas_no_hay_alertas()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote();
        db.AgregarTarjeta(lote, "T-1", "Av. Benavides 108, Miraflores");

        Assert.Equal(0, await Crear(db).EvaluarLoteAsync(lote.Id));
    }

    [Fact]
    public async Task Al_subir_el_umbral_se_retiran_las_alertas_pendientes_que_quedan_por_debajo()
    {
        using var db = BaseEnMemoria.Crear();
        await Crear(db).AgregarAsync("Av. Larco 105, Miraflores", "m", "u", true, null);
        var lote = db.AgregarLote();
        db.AgregarTarjeta(lote, "T-1", "Av. Larko 105, Miraflores");
        await Crear(db).EvaluarLoteAsync(lote.Id);
        var puntaje = Assert.Single(db.Alertas).Puntaje;

        // Con un umbral medio por encima del puntaje, la alerta pendiente deja de tener sentido
        await Crear(db, medio: Math.Min(0.89, puntaje + 0.01), alto: 0.90).EvaluarLoteAsync(lote.Id);

        Assert.Empty(db.Alertas);
    }

    [Fact]
    public async Task Una_alerta_ya_decidida_no_se_retira_aunque_cambie_el_umbral()
    {
        using var db = BaseEnMemoria.Crear();
        await Crear(db).AgregarAsync("Av. Larco 105, Miraflores", "m", "u", true, null);
        var lote = db.AgregarLote();
        db.AgregarTarjeta(lote, "T-1", "Av. Larko 105, Miraflores");
        await Crear(db).EvaluarLoteAsync(lote.Id);
        var alerta = Assert.Single(db.Alertas);
        alerta.Decision = DecisionAlerta.Confirmada;
        db.SaveChanges();

        await Crear(db, medio: Math.Min(0.89, alerta.Puntaje + 0.01), alto: 0.90).EvaluarLoteAsync(lote.Id);

        Assert.Single(db.Alertas);
    }

    [Fact]
    public async Task Reindexar_recalcula_los_vectores_que_quedaron_con_otra_version()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        var d = await riesgo.AgregarAsync("Av. Larco 105, Miraflores", "m", "u", true, null);
        d.ModeloVersion = "motor-viejo";
        d.Vector = new byte[8];
        db.SaveChanges();

        await riesgo.ReindexarAsync();

        Assert.Equal("ngram-hash-v1", d.ModeloVersion);
        Assert.Equal(512 * sizeof(float), d.Vector!.Length);
    }

    [Fact]
    public async Task Buscar_reindexa_solo_si_el_vector_es_de_otra_version()
    {
        using var db = BaseEnMemoria.Crear();
        var riesgo = Crear(db);
        var d = await riesgo.AgregarAsync("Av. Larco 105, Miraflores", "m", "u", true, null);
        d.ModeloVersion = "motor-viejo";
        d.Vector = VectorUtil.ToBytes(new float[512]);
        db.SaveChanges();

        var c = Assert.Single(await riesgo.BuscarAsync("Av. Larko 105, Miraflores"));

        Assert.True(c.Puntaje > 0.5, "tras reindexar, el vector vuelve a comparar");
        Assert.Equal("ngram-hash-v1", d.ModeloVersion);
    }
}
