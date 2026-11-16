using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Tests.Integracion;

/// <summary>Endurecimiento de la web (C2): cabeceras, política de contenido, cookies, antiforgery y límites.</summary>
[Collection(ColeccionIntegracion.Nombre)]
public partial class SeguridadTests(FabricaWeb fabrica)
{
    [GeneratedRegex(@"\son[a-z]+\s*=\s*""", RegexOptions.IgnoreCase)]
    private static partial Regex ManejadorEnLinea();

    [GeneratedRegex(@"<(script|style)\b([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOEstilo();

    private static string Cabecera(HttpResponseMessage r, string nombre) =>
        r.Headers.TryGetValues(nombre, out var v) ? string.Join(",", v)
        : r.Content.Headers.TryGetValues(nombre, out var c) ? string.Join(",", c) : "";

    [Theory]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Home/AccesoDenegado")]
    [InlineData("/css/site.css")]
    [InlineData("/api/v1/hojas-ruta")]
    [InlineData("/no-existe")]
    public async Task Toda_respuesta_lleva_las_cabeceras_de_seguridad(string ruta)
    {
        var r = await fabrica.NuevaSesion().GetAsync(ruta);

        Assert.Equal("nosniff", Cabecera(r, "X-Content-Type-Options"));
        Assert.Equal("SAMEORIGIN", Cabecera(r, "X-Frame-Options"));
        Assert.Equal("same-origin", Cabecera(r, "Referrer-Policy"));
        Assert.Contains("camera=()", Cabecera(r, "Permissions-Policy"));
        Assert.Equal("", Cabecera(r, "Server"));
        var csp = Cabecera(r, "Content-Security-Policy");
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'self'", csp);
        Assert.Contains("form-action 'self'", csp);
        Assert.DoesNotContain("'unsafe-inline'", csp.Split(';').First(d => d.TrimStart().StartsWith("script-src")));
        Assert.DoesNotContain("unsafe-eval", csp);
    }

    [Fact]
    public async Task Cada_respuesta_usa_un_nonce_distinto()
    {
        var sesion = fabrica.NuevaSesion();
        static string Nonce(HttpResponseMessage r) => Regex.Match(Cabecera(r, "Content-Security-Policy"), "'nonce-([^']+)'").Groups[1].Value;

        var a = Nonce(await sesion.GetAsync("/Identity/Account/Login"));
        var b = Nonce(await sesion.GetAsync("/Identity/Account/Login"));

        Assert.True(a.Length >= 20, a);
        Assert.NotEqual(a, b);
    }

    /// <summary>Recorre todas las pantallas de cada rol: ningún manejador en línea y todo script o estilo con el nonce de la respuesta.</summary>
    [Fact]
    public async Task Ninguna_pantalla_incumple_la_politica_de_contenido()
    {
        var problemas = new List<string>();
        var revisadas = 0;
        foreach (var rol in Roles.Todos.Where(r => r != Roles.Repartidor))
        {
            var sesion = await fabrica.SesionDeAsync(rol);
            var vistas = new HashSet<string>();
            var patrones = new HashSet<string>();
            var cola = new Queue<(string Ruta, int Nivel)>();
            cola.Enqueue(("/", 0));
            while (cola.Count > 0)
            {
                var (ruta, nivel) = cola.Dequeue();
                if (!vistas.Add(ruta)) continue;
                // Una sola muestra por tipo de pantalla (/Clientes/Editar/1, /Clientes/Editar/2… son la misma vista)
                if (!patrones.Add(Regex.Replace(ruta, @"\d+|[0-9a-f]{8}-[0-9a-f-]{27}", "N"))) continue;
                var r = await sesion.GetAsync(ruta);
                if (r.StatusCode != HttpStatusCode.OK || r.Content.Headers.ContentType?.MediaType != "text/html") continue;
                var html = await r.Content.ReadAsStringAsync();
                revisadas++;

                var nonce = Regex.Match(Cabecera(r, "Content-Security-Policy"), "'nonce-([^']+)'").Groups[1].Value;
                if (ManejadorEnLinea().Match(html) is { Success: true } m)
                    problemas.Add($"{rol} {ruta}: manejador en línea «{html.Substring(m.Index, Math.Min(40, html.Length - m.Index)).Trim()}»");
                foreach (Match etiqueta in ScriptOEstilo().Matches(html))
                    if (!etiqueta.Groups[2].Value.Contains($"nonce=\"{nonce}\""))
                        problemas.Add($"{rol} {ruta}: <{etiqueta.Groups[1].Value}> sin nonce");
                if (Regex.IsMatch(html, @"href\s*=\s*""javascript:", RegexOptions.IgnoreCase))
                    problemas.Add($"{rol} {ruta}: enlace javascript:");

                if (nivel >= 2) continue;
                foreach (Match enlace in Regex.Matches(html, "href=\"(/[^\"#]*)\""))
                {
                    var destino = WebUtility.HtmlDecode(enlace.Groups[1].Value);
                    if (Regex.IsMatch(destino, @"^/(css|js|lib|img|fonts|favicon|Identity/Account/Logout)|\.(css|js|png|ico)(\?|$)|/Csv/")) continue;
                    cola.Enqueue((destino, nivel + 1));
                }
            }
        }

        Assert.True(revisadas >= 40, $"Solo se revisaron {revisadas} pantallas");
        Assert.True(problemas.Count == 0, string.Join("\n", problemas.Distinct()));
    }

    [Fact]
    public async Task Las_cookies_de_sesion_y_antiforgery_son_seguras_y_no_accesibles_desde_scripts()
    {
        var sesion = fabrica.NuevaSesion();
        var login = await sesion.GetAsync("/Identity/Account/Login");
        var entrada = await sesion.IniciarAsync("supervisor@gdt.local", FabricaWeb.ClaveDemo);

        var cookies = login.Headers.GetValues("Set-Cookie").Concat(entrada.Headers.GetValues("Set-Cookie")).ToList();
        Assert.Contains(cookies, c => c.StartsWith(".AspNetCore.Antiforgery"));
        Assert.Contains(cookies, c => c.StartsWith(".AspNetCore.Identity.Application="));
        Assert.All(cookies, c =>
        {
            Assert.Contains("secure", c, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", c, StringComparison.OrdinalIgnoreCase);
            Assert.Matches("(?i)samesite=(lax|strict)", c);
        });
    }

    [Fact]
    public async Task Las_paginas_con_sesion_no_se_guardan_en_cache()
    {
        var r = await (await fabrica.SesionDeAsync(Roles.Supervisor)).GetAsync("/Clientes");

        Assert.Contains("no-store", Cabecera(r, "Cache-Control"));
    }

    [Fact]
    public async Task La_api_movil_no_exige_antiforgery_pero_la_web_si()
    {
        // API: POST con JSON y sin token antiforgery → llega a la acción (401 por credenciales, no 400 por antiforgery)
        var api = await fabrica.NuevaSesion().Http.PostAsJsonAsync("/api/v1/auth/token", new { correo = "nadie@gdt.local", contrasena = "malaclave1" });
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);

        // Web: el cierre de sesión es una página Razor; sin token se rechaza
        var supervisor = await fabrica.NuevaSesionDeAsync(Roles.Supervisor);
        var web = await supervisor.Http.PostAsync("/Identity/Account/Logout", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, web.StatusCode);
    }

    [Fact]
    public async Task El_limite_de_tokens_moviles_responde_429_con_ProblemDetails()
    {
        var cliente = fabrica.NuevaSesion().Http;       // una sola IP para los 11 intentos
        HttpResponseMessage r = null!;
        for (var i = 0; i < 11; i++)
            r = await cliente.PostAsJsonAsync("/api/v1/auth/token", new { correo = $"nadie{i}@gdt.local", contrasena = "malaclave1" });

        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Demasiados intentos", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_lote_demo_y_sus_documentos_quedaron_en_la_auditoria()
    {
        var acciones = await fabrica.ConBaseAsync((db, _) => db.AuditLogs.Select(a => a.Accion).Distinct().ToListAsync());

        Assert.Superset(new HashSet<string> { "RegistrarLote", "GenerarFichaClientes", "GenerarPlanillaArmado", "GenerarReporteDensidad", "GenerarHojaAsignacion", "GenerarHojasRuta" },
            acciones.ToHashSet());
    }

    [Fact]
    public async Task Un_archivo_binario_renombrado_a_csv_se_rechaza_sin_importar_nada()
    {
        var supervisor = await fabrica.SesionDeAsync(Roles.Supervisor);
        var html = await supervisor.HtmlAsync("/Clientes/Importar");
        var token = SesionWeb.ExtraerToken(html)!;
        var antes = await fabrica.ConBaseAsync((db, _) => db.Tarjetas.CountAsync());

        using var cuerpo = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("1"), "LoteId" },
            { new ByteArrayContent([0x50, 0x4B, 0x03, 0x04, 0x00, 0x01, 0x02, 0xFF, 0xFE, 0x0A, 0x41, 0x3B, 0x42, 0x0A]), "Archivo", "ficha.csv" }
        };
        var r = await supervisor.Http.PostAsync("/Clientes/Importar", cuerpo);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var respuesta = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Assert.True(respuesta.Contains("no es un CSV de texto válido") || respuesta.Contains("Seleccione un lote"), "Debe rechazar el archivo");
        Assert.Equal(antes, await fabrica.ConBaseAsync((db, _) => db.Tarjetas.CountAsync()));
    }
}
