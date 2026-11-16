using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Unidad;

/// <summary>Reglas del descargo (CU09): destino del resultado y verificación de identidad del receptor.</summary>
public class DescargoReglasTests
{
    private static Cliente Titular(string nombre = "María Fernanda Quispe Huamán", string doc = "45871236") =>
        new() { NombreCompleto = nombre, DocumentoIdentidad = doc, Direccion = "Av. Larco 123" };

    [Theory]
    [InlineData(ResultadoDescargo.Entregada, EstadoTarjeta.Entregada)]
    [InlineData(ResultadoDescargo.Devuelta, EstadoTarjeta.Devuelta)]
    [InlineData(ResultadoDescargo.ConIncidencia, EstadoTarjeta.ConIncidencia)]
    [InlineData(ResultadoDescargo.EnCustodia, EstadoTarjeta.EnCustodia)]
    public void Cada_resultado_del_descargo_lleva_a_su_estado_de_tarjeta(ResultadoDescargo resultado, EstadoTarjeta esperado) =>
        Assert.Equal(esperado, DescargoReglas.Destino(resultado));

    [Fact]
    public void Todos_los_destinos_del_descargo_son_transiciones_validas_desde_AsignadaARuta()
    {
        using var db = Apoyo.BaseEnMemoria.Crear();
        var servicio = new TarjetaEstadoService(db);
        foreach (var r in Enum.GetValues<ResultadoDescargo>())
        {
            var t = new Tarjeta { Codigo = "T", Estado = EstadoTarjeta.AsignadaARuta };
            servicio.Cambiar(t, DescargoReglas.Destino(r), "u");
            Assert.Equal(DescargoReglas.Destino(r), t.Estado);
        }
    }

    [Theory]
    [InlineData("María Fernanda Quispe Huamán", "45871236")]      // idéntico
    [InlineData("MARIA QUISPE", "45871236")]                      // sin tildes, mayúsculas, dos palabras
    [InlineData("quispe huaman maria", "45871236")]               // otro orden
    [InlineData("María Quispe", "45.871.236")]                    // documento con puntos
    [InlineData("María Quispe", " 45871236 ")]                    // documento con espacios
    public void La_identidad_coincide_con_el_mismo_documento_y_dos_palabras_del_nombre(string nombre, string doc) =>
        Assert.True(DescargoReglas.IdentidadCoincide(nombre, doc, Titular()));

    [Theory]
    [InlineData("María Fernanda Quispe Huamán", "45871237")]      // documento distinto
    [InlineData("Pedro Pérez Rojas", "45871236")]                 // mismo documento, otro nombre
    [InlineData("María Rojas", "45871236")]                       // solo una palabra en común
    [InlineData("María Fernanda Quispe Huamán", "")]              // sin documento
    [InlineData("María Fernanda Quispe Huamán", null)]
    [InlineData("", "45871236")]                                  // sin nombre
    [InlineData(null, "45871236")]
    public void La_identidad_no_coincide_si_cambia_el_documento_o_el_nombre(string? nombre, string? doc) =>
        Assert.False(DescargoReglas.IdentidadCoincide(nombre, doc, Titular()));

    [Fact]
    public void Con_un_titular_de_un_solo_nombre_basta_esa_palabra()
    {
        Assert.True(DescargoReglas.IdentidadCoincide("Madonna", "X1", Titular("Madonna", "X1")));
        Assert.False(DescargoReglas.IdentidadCoincide("Shakira", "X1", Titular("Madonna", "X1")));
    }

    [Fact]
    public void El_documento_se_compara_sin_distinguir_mayusculas()
    {
        Assert.True(DescargoReglas.IdentidadCoincide("Ana Torres", "ce-00123a", Titular("Ana Torres", "CE00123A")));
    }
}
