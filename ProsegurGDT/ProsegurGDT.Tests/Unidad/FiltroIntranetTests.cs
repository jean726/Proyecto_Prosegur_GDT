using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Unidad;

/// <summary>Filtro de intranet (B4): la web solo responde desde las redes permitidas; /api y los estáticos quedan fuera.</summary>
public class FiltroIntranetTests
{
    private static async Task<(bool paso, HttpContext ctx)> Pedir(string ruta, string? ip, bool habilitada = true, params string[] redes)
    {
        var paso = false;
        var filtro = new FiltroIntranetMiddleware(
            _ => { paso = true; return Task.CompletedTask; },
            Options.Create(new OpcionesIntranet { Habilitada = habilitada, RedesPermitidas = redes.ToList() }),
            NullLogger<FiltroIntranetMiddleware>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = ruta;
        ctx.Request.QueryString = new QueryString("?x=1");
        ctx.Connection.RemoteIpAddress = ip is null ? null : IPAddress.Parse(ip);
        await filtro.InvokeAsync(ctx);
        return (paso, ctx);
    }

    private static bool Bloqueada(HttpContext ctx) => ctx.Items[FiltroIntranetMiddleware.ClaveBloqueo] is true;

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.0/24")]        // dentro del rango CIDR
    [InlineData("203.0.113.255", "203.0.113.0/24")]      // último del rango
    [InlineData("192.0.2.10", "192.0.2.10")]             // IP suelta
    [InlineData("::ffff:203.0.113.5", "203.0.113.0/24")] // IPv4 dentro de IPv6
    [InlineData("2001:db8::1", "2001:db8::/32")]         // IPv6
    [InlineData("10.1.2.3", " 10.0.0.0/8 ")]             // espacios en la configuración
    public async Task Una_ip_de_la_red_permitida_pasa_sin_cambios(string ip, string red)
    {
        var (paso, ctx) = await Pedir("/Lotes", ip, true, red);

        Assert.True(paso);
        Assert.False(Bloqueada(ctx));
        Assert.Equal("/Lotes", ctx.Request.Path);
        Assert.Equal("POST", ctx.Request.Method);
    }

    [Theory]
    [InlineData("198.51.100.7", "203.0.113.0/24")]       // fuera del rango
    [InlineData("203.0.114.1", "203.0.113.0/24")]        // rango vecino
    [InlineData("192.0.2.11", "192.0.2.10")]             // otra IP
    [InlineData("2001:db9::1", "2001:db8::/32")]
    public async Task Una_ip_de_fuera_se_atiende_como_la_pagina_de_aviso(string ip, string red)
    {
        var (paso, ctx) = await Pedir("/Usuarios/Crear", ip, true, red);

        Assert.True(paso);                                   // sigue la cadena, pero hacia la página de aviso
        Assert.True(Bloqueada(ctx));
        Assert.Equal(FiltroIntranetMiddleware.RutaAviso, ctx.Request.Path);
        Assert.Equal("GET", ctx.Request.Method);             // un POST de fuera no llega a ejecutarse
        Assert.False(ctx.Request.QueryString.HasValue);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task Localhost_siempre_pasa_aunque_no_haya_redes(string ip)
    {
        var (_, ctx) = await Pedir("/", ip);
        Assert.False(Bloqueada(ctx));
    }

    [Fact]
    public async Task Sin_ip_conocida_se_bloquea()
    {
        var (_, ctx) = await Pedir("/", null, true, "203.0.113.0/24");
        Assert.True(Bloqueada(ctx));
    }

    [Theory]
    [InlineData("/api/v1/auth/token")]
    [InlineData("/API/v1/hojas-ruta")]
    [InlineData("/css/site.css")]
    [InlineData("/js/site.js")]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("/img/logo.png")]
    [InlineData("/fonts/plex.woff2")]
    [InlineData("/favicon.ico")]
    [InlineData("/ProsegurGDT.Web.styles.css")]
    public async Task La_api_movil_y_los_estaticos_no_pasan_por_el_filtro(string ruta)
    {
        var (paso, ctx) = await Pedir(ruta, "198.51.100.7", true, "203.0.113.0/24");

        Assert.True(paso);
        Assert.False(Bloqueada(ctx));
        Assert.Equal(ruta, ctx.Request.Path);
    }

    [Theory]
    [InlineData("/apiculos")]            // no es el segmento /api
    [InlineData("/cssx/site.css")]
    [InlineData("/Home/FueraDeRed")]
    [InlineData("/Identity/Account/Login")]
    public async Task Una_ruta_parecida_a_una_exenta_si_se_filtra(string ruta)
    {
        var (_, ctx) = await Pedir(ruta, "198.51.100.7", true, "203.0.113.0/24");
        Assert.True(Bloqueada(ctx));
    }

    [Fact]
    public async Task Con_el_filtro_apagado_pasa_cualquier_ip()
    {
        var (paso, ctx) = await Pedir("/Lotes", "198.51.100.7", habilitada: false);
        Assert.True(paso);
        Assert.False(Bloqueada(ctx));
    }

    [Fact]
    public async Task Una_red_mal_escrita_se_ignora_y_las_validas_siguen_funcionando()
    {
        var (_, dentro) = await Pedir("/", "203.0.113.5", true, "no-es-una-ip", "999.1.1.1/24", "", "203.0.113.0/24");
        var (_, fuera) = await Pedir("/", "198.51.100.7", true, "no-es-una-ip", "999.1.1.1/24", "", "203.0.113.0/24");

        Assert.False(Bloqueada(dentro));
        Assert.True(Bloqueada(fuera));
    }

    [Fact]
    public async Task Activo_y_sin_redes_solo_responde_a_localhost()
    {
        var (_, fuera) = await Pedir("/", "203.0.113.5");
        Assert.True(Bloqueada(fuera));
    }
}
