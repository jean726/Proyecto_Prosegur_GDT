using System.Net;
using Microsoft.Extensions.Options;

namespace ProsegurGDT.Web.Services;

// B4 — Intranet: la plataforma web solo responde desde las redes de la empresa.
// La API móvil (/api) queda fuera del filtro: el Repartidor trabaja con datos móviles y la protege el JWT.
public sealed class OpcionesIntranet
{
    public const string Seccion = "Intranet";

    public bool Habilitada { get; set; }

    // IP sueltas ("190.12.34.56") o rangos CIDR ("190.12.34.0/24"). Localhost siempre se permite.
    public List<string> RedesPermitidas { get; set; } = new();

    // Tomar la IP del cliente de X-Forwarded-For (último salto). Si no se indica, solo dentro de Azure App Service:
    // sin un proxy delante, esa cabecera la puede inventar el cliente.
    public bool? ConfiarEnProxy { get; set; }
}

public sealed class FiltroIntranetMiddleware
{
    public const string RutaAviso = "/Home/FueraDeRed";
    public const string ClaveBloqueo = "Intranet.Bloqueado";

    // La API móvil, la comprobación de salud (monitoreo) y los recursos estáticos públicos
    // (estilos, scripts, imágenes y fuentes de la página de aviso).
    private static readonly string[] RutasExentas = { "/api", "/health", "/css", "/js", "/lib", "/img", "/fonts" };
    private static readonly string[] ArchivosExentos = { "/favicon.ico", "/ProsegurGDT.Web.styles.css" };

    private readonly RequestDelegate _next;
    private readonly ILogger<FiltroIntranetMiddleware> _log;
    private readonly bool _habilitada;
    private readonly List<System.Net.IPNetwork> _redes = new();

    public FiltroIntranetMiddleware(RequestDelegate next, IOptions<OpcionesIntranet> opciones, ILogger<FiltroIntranetMiddleware> log)
    {
        _next = next;
        _log = log;
        _habilitada = opciones.Value.Habilitada;
        foreach (var texto in opciones.Value.RedesPermitidas)
        {
            if (IntentarLeerRed(texto, out var red)) _redes.Add(red);
            else log.LogWarning("Intranet: la red permitida «{Red}» no es una IP ni un rango CIDR válido; se ignora.", texto);
        }
        if (_habilitada)
        {
            if (_redes.Count == 0)
                log.LogWarning("Intranet: el filtro está activo sin redes permitidas; la web solo responde desde localhost.");
            else
                log.LogWarning("Intranet: filtro activo; la web solo responde desde {Redes}.", string.Join(", ", _redes));
        }
    }

    public Task InvokeAsync(HttpContext ctx)
    {
        if (!_habilitada || Exenta(ctx.Request.Path)) return _next(ctx);

        var ip = ctx.Connection.RemoteIpAddress;
        if (Permitida(ip)) return _next(ctx);

        _log.LogWarning("Intranet: acceso web bloqueado desde {Ip} a {Ruta}.", ip?.ToString() ?? "desconocida", ctx.Request.Path.Value);
        // Se atiende como la página de aviso (403), sea cual sea la ruta pedida.
        ctx.Items[ClaveBloqueo] = true;
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Path = RutaAviso;
        ctx.Request.QueryString = QueryString.Empty;
        return _next(ctx);
    }

    private static bool Exenta(PathString ruta) =>
        RutasExentas.Any(r => ruta.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase)) ||
        ArchivosExentos.Any(a => ruta.Equals(a, StringComparison.OrdinalIgnoreCase));

    private bool Permitida(IPAddress? ip)
    {
        if (ip is null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        return _redes.Any(r => r.Contains(ip));
    }

    private static bool IntentarLeerRed(string? texto, out System.Net.IPNetwork red)
    {
        red = default;
        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto)) return false;
        if (texto.Contains('/')) return System.Net.IPNetwork.TryParse(texto, out red);
        if (!IPAddress.TryParse(texto, out var ip)) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        red = new System.Net.IPNetwork(ip, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
        return true;
    }
}
