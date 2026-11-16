using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Unidad;

/// <summary>Normalización de direcciones antes de compararlas con el motor de IA (CU15, CU16).</summary>
public class NormalizadorDireccionesTests
{
    [Theory]
    [InlineData("Av. Larco 123, Miraflores", "AVENIDA LARCO 123 MIRAFLORES")]
    [InlineData("AVDA LARCO 123", "AVENIDA LARCO 123")]
    [InlineData("Jr. de la Unión 450", "JIRON DE LA UNION 450")]
    [InlineData("Cal. Berlín Nro. 119 Dpto. 301", "CALLE BERLIN 119 DEPARTAMENTO 301")]
    [InlineData("Psje. Los Pinos Mz. B Lt. 4 Urb. San Felipe", "PASAJE LOS PINOS MANZANA B LOTE 4 URBANIZACION SAN FELIPE")]
    [InlineData("Av Pardo N° 749 Int. 2", "AVENIDA PARDO 749 INTERIOR 2")]
    public void Expande_abreviaturas_quita_tildes_y_signos(string entrada, string esperado) =>
        Assert.Equal(esperado, NormalizadorDirecciones.Normalizar(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".,;-#")]
    public void Un_texto_vacio_o_solo_con_signos_da_cadena_vacia(string? entrada) =>
        Assert.Equal(string.Empty, NormalizadorDirecciones.Normalizar(entrada));

    [Fact]
    public void Es_idempotente()
    {
        var una = NormalizadorDirecciones.Normalizar("Av. José Larco Nro. 105 - Miraflores");
        Assert.Equal(una, NormalizadorDirecciones.Normalizar(una));
    }

    [Fact]
    public void Dos_formas_de_escribir_la_misma_direccion_quedan_iguales()
    {
        Assert.Equal(
            NormalizadorDirecciones.Normalizar("AV. LARCO  N° 105,  MIRAFLORES"),
            NormalizadorDirecciones.Normalizar("avenida larco 105 miraflores"));
    }

    [Fact]
    public void Conserva_la_enie_como_n_y_los_numeros()
    {
        Assert.Equal("CALLE LAS BEGONIAS 475 NANA", NormalizadorDirecciones.Normalizar("Calle Las Begonias 475 Ñaña"));
    }
}
