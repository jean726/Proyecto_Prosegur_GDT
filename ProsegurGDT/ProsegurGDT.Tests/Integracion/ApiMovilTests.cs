using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Tests.Integracion;

/// <summary>
/// Contrato de la API móvil /api/v1 (CU22, CU23, CU28–CU31; RN-06, RN-07, RN-29, RN-30).
/// El flujo completo es una sola prueba porque cada paso depende del anterior, como en la jornada real.
/// </summary>
[Collection(ColeccionIntegracion.Nombre)]
public class ApiMovilTests(FabricaWeb fabrica)
{
    private const string Repartidor = "repartidor@gdt.local";

    private HttpClient Cliente(string? token = null)
    {
        var c = fabrica.NuevaSesion().Http;
        if (token is not null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<string> Detalle(HttpResponseMessage r) =>
        (await Json(r)).TryGetProperty("detail", out var d) ? d.GetString() ?? "" : "";

    private Task<HttpResponseMessage> PedirToken(string correo, string clave) =>
        Cliente().PostAsJsonAsync("/api/v1/auth/token", new { correo, contrasena = clave });

    // ---------- autenticación ----------
    [Fact]
    public async Task Sin_token_responde_401_con_ProblemDetails_en_espanol_y_no_redirige_al_login()
    {
        var r = await Cliente().GetAsync("/api/v1/hojas-ruta");

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Inicie sesión", await Detalle(r));
        Assert.Contains("Bearer", r.Headers.WwwAuthenticate.ToString());
    }

    [Theory]
    [InlineData("no-es-un-token")]
    [InlineData("eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJ4Iiwicm9sZSI6IlJlcGFydGlkb3IifQ.")]   // algoritmo «none»
    public async Task Un_token_invalido_responde_401(string token)
    {
        var r = await Cliente(token).GetAsync("/api/v1/hojas-ruta");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task La_cookie_de_la_web_no_vale_en_la_api()
    {
        var supervisor = await fabrica.SesionDeAsync(Roles.Supervisor);

        var r = await supervisor.GetAsync("/api/v1/hojas-ruta");

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Las_credenciales_incorrectas_o_incompletas_no_dan_token()
    {
        var mala = await PedirToken("nadie@gdt.local", "malaclave1");
        Assert.Equal(HttpStatusCode.Unauthorized, mala.StatusCode);
        Assert.Contains("incorrectos", await Detalle(mala));

        var vacia = await PedirToken("", "");
        Assert.Equal(HttpStatusCode.BadRequest, vacia.StatusCode);
    }

    [Fact]
    public async Task Un_rol_de_la_web_no_obtiene_token_movil()
    {
        var r = await PedirToken("supervisor@gdt.local", FabricaWeb.ClaveDemo);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("no está habilitada", await Detalle(r));
    }

    [Fact]
    public async Task El_documento_OpenAPI_solo_publica_las_rutas_de_la_api_movil()
    {
        var r = await Cliente().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var rutas = (await Json(r)).GetProperty("paths").EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(
        [
            "/api/v1/auth/logout", "/api/v1/auth/token", "/api/v1/auth/yo", "/api/v1/hojas-ruta", "/api/v1/hojas-ruta/{id}",
            "/api/v1/hojas-ruta/{id}/confirmacion", "/api/v1/hojas-ruta/{id}/visitas/{tarjetaId}/resultado",
            "/api/v1/hojas-ruta/{id}/visitas/{tarjetaId}/suplantacion"
        ], rutas);
    }

    // ---------- jornada completa del repartidor ----------
    [Fact]
    public async Task Jornada_completa_del_repartidor_desde_el_token_hasta_el_descargo()
    {
        // CU22: token
        var rToken = await PedirToken(Repartidor, FabricaWeb.ClaveDemo);
        Assert.Equal(HttpStatusCode.OK, rToken.StatusCode);
        var jToken = await Json(rToken);
        var token = jToken.GetProperty("token").GetString()!;
        Assert.Equal("Carlos Mendoza", jToken.GetProperty("repartidor").GetProperty("nombre").GetString());
        Assert.EndsWith("Z", jToken.GetProperty("expiraUtc").GetString());                  // fechas en UTC
        var api = Cliente(token);

        var yo = await Json(await api.GetAsync("/api/v1/auth/yo"));
        Assert.Equal("Motorizado", yo.GetProperty("tipo").GetString());                     // enums como texto

        // CU28: antes de aprobar no ve nada
        Assert.Equal(0, (await Json(await api.GetAsync("/api/v1/hojas-ruta"))).GetArrayLength());

        // El Supervisor aprueba las 6 hojas demo (CU05)
        var supervisor = await fabrica.SesionDeAsync(Roles.Supervisor);
        var ids = Regex.Matches(await supervisor.HtmlAsync("/HojasRuta"), @"/HojasRuta/Detalle/(\d+)").Select(m => int.Parse(m.Groups[1].Value)).Distinct().Order().ToList();
        Assert.Equal(6, ids.Count);
        foreach (var i in ids)
            Assert.Equal(HttpStatusCode.Redirect, (await supervisor.PostAsync($"/HojasRuta/Detalle/{i}", $"/HojasRuta/Aprobar/{i}")).StatusCode);

        // CU28: ve solo su hoja, aprobada
        var lista = await Json(await api.GetAsync("/api/v1/hojas-ruta"));
        Assert.Equal(1, lista.GetArrayLength());
        Assert.Equal("Aprobada", lista[0].GetProperty("estado").GetString());
        var hoja = lista[0].GetProperty("id").GetInt32();
        var ajena = ids.First(i => i != hoja);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync($"/api/v1/hojas-ruta/{ajena}")).StatusCode);

        var detalle = await Json(await api.GetAsync($"/api/v1/hojas-ruta/{hoja}"));
        var visitas = detalle.GetProperty("visitas").EnumerateArray().ToList();
        Assert.Equal(6, visitas.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6], visitas.Select(v => v.GetProperty("orden").GetInt32()));
        Assert.All(visitas, v =>
        {
            Assert.False(string.IsNullOrEmpty(v.GetProperty("titular").GetString()));
            Assert.False(string.IsNullOrEmpty(v.GetProperty("documentoTitular").GetString()));
            Assert.Equal(JsonValueKind.Object, v.GetProperty("cita").ValueKind);
            Assert.False(v.GetProperty("editable").GetBoolean());                             // aún sin confirmar recepción
        });
        var (v1, v2, v3) = (visitas[0], visitas[1], visitas[2]);
        string Url(JsonElement v) => $"/api/v1/hojas-ruta/{hoja}/visitas/{v.GetProperty("tarjetaId").GetInt32()}/resultado";
        object Entrega(JsonElement v) => new { resultado = "Entregada", receptorNombre = v.GetProperty("titular").GetString(), receptorDocumento = v.GetProperty("documentoTitular").GetString() };

        // RN-06: sin confirmar la recepción no se registran visitas
        var antes = await api.PutAsJsonAsync(Url(v1), Entrega(v1));
        Assert.Equal(HttpStatusCode.Conflict, antes.StatusCode);
        Assert.Contains("Confirme la recepción", await Detalle(antes));

        // CU31
        Assert.Equal(HttpStatusCode.OK, (await api.PostAsync($"/api/v1/hojas-ruta/{hoja}/confirmacion", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsync($"/api/v1/hojas-ruta/{hoja}/confirmacion", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync($"/api/v1/hojas-ruta/{ajena}/confirmacion", null)).StatusCode);

        // CU29 (RN-07): la entrega exige nombre y documento; la no entrega, motivo
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsJsonAsync(Url(v1), new { resultado = "Entregada", receptorNombre = "Alguien" })).StatusCode);
        var entrega = await api.PutAsJsonAsync(Url(v1), Entrega(v1));
        Assert.Equal(HttpStatusCode.Created, entrega.StatusCode);
        Assert.DoesNotContain("Aviso", (await Json(entrega)).GetProperty("mensaje").GetString());

        var otroReceptor = await api.PutAsJsonAsync(Url(v1), new { resultado = "Entregada", receptorNombre = "Pedro Pérez", receptorDocumento = "99999999" });
        Assert.Equal(HttpStatusCode.OK, otroReceptor.StatusCode);                             // corrección, no alta
        Assert.Contains("Aviso", (await Json(otroReceptor)).GetProperty("mensaje").GetString());
        Assert.Equal(HttpStatusCode.OK, (await api.PutAsJsonAsync(Url(v1), Entrega(v1))).StatusCode);

        var sinMotivo = await api.PutAsJsonAsync(Url(v2), new { resultado = "NoEntregada" });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        Assert.Contains("motivo", await Detalle(sinMotivo));
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsJsonAsync(Url(v2), new { resultado = "NoEntregada", motivo = "Otro" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.PutAsJsonAsync(Url(v2), new { resultado = "NoEntregada", motivo = "ClienteAusente", detalle = "Nadie atendió" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsJsonAsync(Url(v2), new { resultado = "Perdida" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PutAsJsonAsync($"/api/v1/hojas-ruta/{hoja}/visitas/999999/resultado", new { resultado = "NoEntregada", motivo = "ClienteAusente" })).StatusCode);

        // CU30
        var urlSup = $"/api/v1/hojas-ruta/{hoja}/visitas/{v3.GetProperty("tarjetaId").GetInt32()}/suplantacion";
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonAsync(urlSup, new { descripcion = "corto" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.PostAsJsonAsync(urlSup, new { descripcion = "La persona presentó un DNI con otra foto y se negó a firmar." })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonAsync(urlSup, new { descripcion = "Segundo reporte de la misma tarjeta para prueba." })).StatusCode);

        // CU28: el detalle refleja lo registrado y (RN-30) el estado de las tarjetas no cambió
        detalle = await Json(await api.GetAsync($"/api/v1/hojas-ruta/{hoja}"));
        Assert.True(detalle.GetProperty("recepcionConfirmada").GetBoolean());
        var porTarjeta = detalle.GetProperty("visitas").EnumerateArray().ToDictionary(v => v.GetProperty("tarjetaId").GetInt32());
        Assert.Equal("Entregada", porTarjeta[v1.GetProperty("tarjetaId").GetInt32()].GetProperty("resultado").GetProperty("resultado").GetString());
        Assert.Equal("ClienteAusente", porTarjeta[v2.GetProperty("tarjetaId").GetInt32()].GetProperty("resultado").GetProperty("motivo").GetString());
        Assert.Equal("Pendiente", porTarjeta[v3.GetProperty("tarjetaId").GetInt32()].GetProperty("suplantacion").GetProperty("estado").GetString());
        Assert.All(porTarjeta.Values, v => Assert.Equal("AsignadaARuta", v.GetProperty("estadoTarjeta").GetString()));
        var estados = await fabrica.ConBaseAsync((db, _) => db.Tarjetas.Where(t => porTarjeta.Keys.Contains(t.Id)).Select(t => t.Estado).Distinct().ToListAsync());
        Assert.Equal([EstadoTarjeta.AsignadaARuta], estados);

        // El Verificador ve la suplantación y el reporte de fraude sale precargado
        var verificador = await fabrica.SesionDeAsync(Roles.Verificador);
        var fraude = await verificador.HtmlAsync("/Fraude");
        Assert.Contains("Suplantaciones reportadas desde campo", fraude);
        Assert.Contains(v3.GetProperty("codigo").GetString()!, fraude);
        var suplantacionId = porTarjeta[v3.GetProperty("tarjetaId").GetInt32()].GetProperty("suplantacion").GetProperty("id").GetInt32();
        var nuevo = await verificador.HtmlAsync($"/Fraude/Nuevo?hojaRutaId={hoja}&tarjetaId={v3.GetProperty("tarjetaId").GetInt32()}&suplantacionId={suplantacionId}");
        Assert.Contains("Reportado por el repartidor", nuevo);

        // Descargo: el Supervisor firma el resto, crea y remite el reporte de entregas (CU07, CU08)
        foreach (var i in ids.Where(i => i != hoja))
            await supervisor.PostAsync($"/HojasRuta/Detalle/{i}", $"/HojasRuta/Firmar/{i}");
        var fecha = Regex.Match(await supervisor.HtmlAsync("/ReportesEntregas"), "name=\"fecha\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEqual("", fecha);
        var creado = await supervisor.PostAsync("/ReportesEntregas", "/ReportesEntregas/Crear", ("fecha", fecha));
        var reporte = Regex.Match(creado.RutaDestino(), @"/ReportesEntregas/Detalle/(\d+)").Groups[1].Value;
        Assert.NotEqual("", reporte);
        await supervisor.PostAsync($"/ReportesEntregas/Detalle/{reporte}", $"/ReportesEntregas/Remitir/{reporte}");

        // CU09: el Descargador ve el pre-registro de campo y el formulario precargado
        var descargador = await fabrica.SesionDeAsync(Roles.Descargador);
        var procesar = await descargador.HtmlAsync($"/Descargo/Procesar/{hoja}");
        Assert.Contains("Reportado en campo", procesar);
        Assert.Contains("Suplantación reportada", procesar);
        await descargador.PostAsync($"/Descargo/Procesar/{hoja}", "/Descargo/Cargos", ("HojaRutaId", hoja.ToString()), ("Cantidad", "6"));
        procesar = await descargador.HtmlAsync($"/Descargo/Procesar/{hoja}");
        Assert.Contains("Confirmar resultado", procesar);
        Assert.Contains($"value=\"{v1.GetProperty("documentoTitular").GetString()}\"", procesar);

        // RN-30: con el descargo iniciado, la app ya no puede modificar el resultado
        Assert.Equal(HttpStatusCode.Conflict, (await api.PutAsJsonAsync(Url(v1), new { resultado = "NoEntregada", motivo = "ClienteAusente" })).StatusCode);

        // Auditoría de las acciones móviles
        var acciones = await fabrica.ConBaseAsync((db, _) => db.AuditLogs.Where(a => a.UsuarioNombre == Repartidor).Select(a => a.Accion).Distinct().ToListAsync());
        Assert.Superset(new HashSet<string> { "IniciarSesionMovil", "ConfirmarHojaRutaMovil", "RegistrarResultadoCampo", "ReportarSuplantacion" }, acciones.ToHashSet());

        // CU23: cerrar sesión revoca el token
        Assert.Equal(HttpStatusCode.OK, (await api.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/v1/hojas-ruta")).StatusCode);
    }
}
