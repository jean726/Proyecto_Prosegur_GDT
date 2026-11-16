using System.Net;
using System.Text.Json;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Tests.Integracion;

/// <summary>Operación (C4): comprobación de salud y páginas de error propias en español.</summary>
[Collection(ColeccionIntegracion.Nombre)]
public class OperacionTests(FabricaWeb fabrica)
{
    [Fact]
    public async Task Health_informa_la_base_de_datos_y_el_motor_de_IA_sin_pedir_sesion()
    {
        var r = await fabrica.NuevaSesion().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/json", r.Content.Headers.ContentType!.MediaType);
        var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Correcto", json.GetProperty("estado").GetString());
        var comprobaciones = json.GetProperty("comprobaciones").EnumerateArray().ToDictionary(c => c.GetProperty("nombre").GetString()!);
        Assert.Equal("Correcto", comprobaciones["base-de-datos"].GetProperty("estado").GetString());
        Assert.Equal("Correcto", comprobaciones["motor-ia"].GetProperty("estado").GetString());
        Assert.Equal("ngram-hash-v1", comprobaciones["motor-ia"].GetProperty("datos").GetProperty("motor").GetString());
    }

    [Fact]
    public async Task Health_no_revela_la_cadena_de_conexion_ni_rutas_del_servidor()
    {
        var texto = await (await fabrica.NuevaSesion().GetAsync("/health")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Server=", texto);
        Assert.DoesNotContain(fabrica.NombreBase, texto);
        Assert.DoesNotContain(":\\", texto);
    }

    [Fact]
    public async Task Una_direccion_inexistente_muestra_la_pagina_404_propia()
    {
        var anonimo = await fabrica.NuevaSesion().GetAsync("/no-existe");
        var conSesion = await (await fabrica.SesionDeAsync(Roles.Supervisor)).GetAsync("/Clientes/Editar/999999");

        foreach (var r in new[] { anonimo, conSesion })
        {
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
            var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
            Assert.Contains("Página no encontrada", html);
            Assert.Contains("Volver al inicio", html);
            Assert.Contains("pg-torre", html);
        }
    }

    [Fact]
    public async Task Un_formulario_sin_token_muestra_la_pagina_400_propia()
    {
        var sesion = await fabrica.SesionDeAsync(Roles.Supervisor);

        var r = await sesion.Http.PostAsync("/ReportesEntregas/Crear", new FormUrlEncodedContent([KeyValuePair.Create("fecha", "2026-11-16")]));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("Solicitud no válida", WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task La_api_conserva_sus_errores_en_JSON()
    {
        var r = await fabrica.NuevaSesion().GetAsync("/api/v1/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("<html", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_plantilla_de_privacidad_ya_no_existe()
    {
        var r = await (await fabrica.SesionDeAsync(Roles.Supervisor)).GetAsync("/Home/Privacy");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
