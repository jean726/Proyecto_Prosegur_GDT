using Microsoft.Extensions.Configuration;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Unidad;

/// <summary>Motor de respaldo por n-gramas (hash), motor híbrido y umbrales de la IA (CU15, CU16, RNF de exactitud).</summary>
public class MotorDeIaTests
{
    private static readonly HashingNgramEmbeddingService Hash = new();

    private static double Similitud(string a, string b) =>
        VectorUtil.Dot(Hash.Embed(NormalizadorDirecciones.Normalizar(a)), Hash.Embed(NormalizadorDirecciones.Normalizar(b)));

    [Fact]
    public void El_vector_tiene_512_dimensiones_y_norma_uno()
    {
        var v = Hash.Embed("AVENIDA LARCO 105 MIRAFLORES");
        Assert.Equal(512, v.Length);
        Assert.Equal(1.0, Math.Sqrt(v.Sum(x => (double)x * x)), 5);
    }

    [Fact]
    public void Un_texto_vacio_da_el_vector_cero()
    {
        Assert.All(Hash.Embed(""), x => Assert.Equal(0f, x));
        Assert.All(Hash.Embed("   "), x => Assert.Equal(0f, x));
    }

    [Fact]
    public void Es_determinista_entre_instancias()
    {
        Assert.Equal(Hash.Embed("JIRON DE LA UNION 450"), new HashingNgramEmbeddingService().Embed("JIRON DE LA UNION 450"));
    }

    [Fact]
    public void La_misma_direccion_puntua_uno()
    {
        Assert.Equal(1.0, Similitud("Av. Larco 105, Miraflores", "AVENIDA LARCO 105 MIRAFLORES"), 5);
    }

    [Fact]
    public void Un_error_de_tipeo_puntua_mas_que_una_calle_distinta_del_mismo_distrito()
    {
        var tipeo = Similitud("Av. Larco 105 Miraflores", "Av. Larko 105 Miraflores");
        var otraCalle = Similitud("Av. Larco 105 Miraflores", "Av. Pardo 749 Miraflores");
        var otraZona = Similitud("Av. Larco 105 Miraflores", "Jr. Huallaga 300 Lima Cercado");

        Assert.True(tipeo > otraCalle, $"tipeo {tipeo:F3} debe superar a otra calle {otraCalle:F3}");
        Assert.True(otraCalle > otraZona, $"otra calle {otraCalle:F3} debe superar a otra zona {otraZona:F3}");
        Assert.True(tipeo >= 0.60, $"un tipeo leve debe alcanzar el umbral medio del motor hash (0.60): {tipeo:F3}");
        Assert.True(otraZona < 0.60, $"una dirección de otra zona no debe alertar: {otraZona:F3}");
    }

    [Fact]
    public void La_version_del_motor_cabe_en_los_40_caracteres_de_la_base()
    {
        Assert.True(Hash.Version.Length <= 40);
        Assert.False(Hash.ModeloIaActivo);
    }

    [Fact]
    public void Los_vectores_se_guardan_y_se_leen_sin_perdida()
    {
        var v = Hash.Embed("CALLE BERLIN 119");
        Assert.Equal(v, VectorUtil.FromBytes(VectorUtil.ToBytes(v)));
        Assert.Equal(512 * sizeof(float), VectorUtil.ToBytes(v).Length);
    }

    // ---------- motor híbrido ----------
    private sealed class MotorFijo(string version, params float[] vector) : IEmbeddingService
    {
        public string Version => version;
        public int Dimension => vector.Length;
        public bool ModeloIaActivo => true;
        public float[] Embed(string textoNormalizado) => vector;
    }

    [Fact]
    public void El_hibrido_concatena_ambos_vectores_y_sigue_siendo_unitario()
    {
        var hibrido = new HybridEmbeddingService(new MotorFijo("m", 1f, 0f), Hash, 0.5);
        var v = hibrido.Embed("AVENIDA LARCO 105");

        Assert.Equal(2 + 512, hibrido.Dimension);
        Assert.Equal(hibrido.Dimension, v.Length);
        Assert.Equal(1.0, Math.Sqrt(v.Sum(x => (double)x * x)), 5);
        Assert.True(hibrido.ModeloIaActivo);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void El_producto_del_hibrido_es_la_media_ponderada_de_los_dos_motores(double peso)
    {
        // El motor semántico fijo da siempre el mismo vector: su coseno es 1. El del hash se calcula aparte.
        var hibrido = new HybridEmbeddingService(new MotorFijo("m", 0.6f, 0.8f), Hash, peso);
        const string a = "AVENIDA LARCO 105 MIRAFLORES", b = "AVENIDA PARDO 749 MIRAFLORES";
        var cosHash = VectorUtil.Dot(Hash.Embed(a), Hash.Embed(b));

        var real = VectorUtil.Dot(hibrido.Embed(a), hibrido.Embed(b));

        Assert.Equal(peso * 1.0 + (1 - peso) * cosHash, real, 4);
    }

    [Fact]
    public void El_hibrido_rechaza_una_version_de_mas_de_40_caracteres()
    {
        var largo = new MotorFijo(new string('x', 40), 1f);
        Assert.Throws<InvalidOperationException>(() => new HybridEmbeddingService(largo, Hash, 0.5));
    }

    [Fact]
    public void La_version_del_hibrido_real_cabe_en_40_caracteres()
    {
        var hibrido = new HybridEmbeddingService(new MotorFijo("minilm-l12-int8", 1f), Hash, 0.5);
        Assert.True(hibrido.Version.Length <= 40, hibrido.Version);
        Assert.EndsWith("-w50", hibrido.Version);
    }

    // ---------- umbrales ----------
    private static IConfiguration Config(params (string, string?)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.ToDictionary(v => v.Item1, v => v.Item2)).Build();

    [Fact]
    public void Sin_configuracion_usa_los_umbrales_del_motor_hash_y_tres_intentos()
    {
        var cfg = new ConfiguracionOperativa(Config());
        Assert.Equal(0.60, cfg.UmbralMedio);
        Assert.Equal(0.90, cfg.UmbralAlto);
        Assert.Equal(3, cfg.MaxIntentosContacto);
    }

    [Fact]
    public void Lee_los_umbrales_del_motor_hibrido_de_la_configuracion()
    {
        var cfg = new ConfiguracionOperativa(Config(("IA:UmbralMedio", "0.68"), ("IA:UmbralAlto", "0.90"), ("Contacto:MaxIntentos", "5")));
        Assert.Equal(0.68, cfg.UmbralMedio);
        Assert.Equal(0.90, cfg.UmbralAlto);
        Assert.Equal(5, cfg.MaxIntentosContacto);
    }

    [Theory]
    [InlineData("0", "0.9")]      // medio debe ser mayor que 0
    [InlineData("1", "1")]        // medio debe ser menor que 1
    [InlineData("0.9", "0.9")]    // alto debe superar al medio
    [InlineData("0.9", "0.6")]
    [InlineData("0.6", "1.1")]    // alto no puede pasar de 1
    public void Rechaza_umbrales_incoherentes(string medio, string alto) =>
        Assert.Throws<InvalidOperationException>(() => new ConfiguracionOperativa(Config(("IA:UmbralMedio", medio), ("IA:UmbralAlto", alto))));

    [Fact]
    public void El_maximo_de_intentos_nunca_baja_de_uno()
    {
        Assert.Equal(1, new ConfiguracionOperativa(Config(("Contacto:MaxIntentos", "0"))).MaxIntentosContacto);
    }
}
