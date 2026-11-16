using System.Threading.RateLimiting;

namespace ProsegurGDT.Web.Services.Seguridad;

// C2 — Límites de intentos de inicio de sesión por minuto y dirección IP (sección «Limites» de la configuración).
public sealed class OpcionesLimites
{
    public const string Seccion = "Limites";
    public int LoginWebPorMinuto { get; set; } = 10;
    public int TokenMovilPorMinuto { get; set; } = 10;
}

public interface ILimitadorLoginWeb
{
    /// <summary>Cuenta un intento de inicio de sesión desde la IP de la petición; false si superó el límite del minuto.</summary>
    bool Permitir(HttpContext ctx);
}

// El bloqueo de Identity protege cada cuenta (5 fallos); este limitador protege contra quien prueba muchas cuentas
// desde un mismo origen. Vive en la página de login y no en el middleware para poder responder con la propia
// página y un mensaje en español en lugar de un 429 vacío.
public sealed class LimitadorLoginWeb : ILimitadorLoginWeb, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limitador;

    public LimitadorLoginWeb(OpcionesLimites opciones)
    {
        var limite = Math.Max(1, opciones.LoginWebPorMinuto);
        _limitador = PartitionedRateLimiter.Create<string, string>(ip => RateLimitPartition.GetFixedWindowLimiter(ip,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    }

    public bool Permitir(HttpContext ctx)
    {
        using var permiso = _limitador.AttemptAcquire(ctx.Connection.RemoteIpAddress?.ToString() ?? "?");
        return permiso.IsAcquired;
    }

    public void Dispose() => _limitador.Dispose();
}
