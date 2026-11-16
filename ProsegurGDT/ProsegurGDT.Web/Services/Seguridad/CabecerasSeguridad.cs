using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ProsegurGDT.Web.Services.Seguridad;

// C2 — Cabeceras de seguridad de todas las respuestas (RNF de seguridad).
// La política de contenido (CSP) solo admite scripts y estilos propios o marcados con el «nonce» de la petición:
// un script inyectado en un dato (XSS) no se ejecuta aunque llegara a la página.
public sealed class CabecerasSeguridadMiddleware
{
    public const string ClaveNonce = "Seguridad.Nonce";

    private readonly RequestDelegate _next;
    private readonly bool _desarrollo;

    public CabecerasSeguridadMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _desarrollo = env.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext ctx)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));   // solo [0-9A-F]: no necesita codificarse en HTML
        ctx.Items[ClaveNonce] = nonce;

        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h.ContentSecurityPolicy = Politica(nonce);
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "SAMEORIGIN";
            h["Referrer-Policy"] = "same-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";

            // Las páginas con datos de clientes no se guardan en la caché del navegador (botón «atrás» tras cerrar sesión)
            if (ctx.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true
                && !h.ContainsKey("Cache-Control"))
                h.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        return _next(ctx);
    }

    private string Politica(string nonce)
    {
        // En desarrollo, Visual Studio y «dotnet watch» inyectan su script de recarga desde otro puerto de localhost.
        var local = _desarrollo ? " http://localhost:* https://localhost:*" : "";
        var conexion = _desarrollo ? " http://localhost:* https://localhost:* ws://localhost:* wss://localhost:*" : "";
        return string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}'{local}",
            $"style-src 'self' 'nonce-{nonce}'",
            "style-src-attr 'unsafe-inline'",          // anchos de barras y columnas calculados en las vistas
            "img-src 'self' data:",
            "font-src 'self'",
            $"connect-src 'self'{conexion}",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'self'");
    }
}

// Añade el «nonce» de la petición a cada <script> y <style> de las vistas, para que la CSP los admita.
[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
public sealed class NonceTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    // Después de los tag helpers del marco (asp-append-version, etc.)
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.HttpContext.Items[CabecerasSeguridadMiddleware.ClaveNonce] is string nonce)
            output.Attributes.SetAttribute("nonce", nonce);
    }
}
