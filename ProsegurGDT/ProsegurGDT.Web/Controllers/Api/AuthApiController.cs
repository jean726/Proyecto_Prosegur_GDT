using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models.Api;
using ProsegurGDT.Web.Services;
using ProsegurGDT.Web.Services.Api;

namespace ProsegurGDT.Web.Controllers.Api;

// CU22 / CU23 por la API móvil: el Repartidor obtiene y revoca su token JWT (RF29, RF30, RN-29).
[ApiController]
[IgnoreAntiforgeryToken]   // la API se autentica con JWT en la cabecera, no con cookies
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthApiController : ControllerBase
{
    private const string NoHabilitada = "La cuenta no está habilitada para la aplicación móvil. Comuníquese con el administrador del sistema.";

    private readonly UserManager<ApplicationUser> _um;
    private readonly SignInManager<ApplicationUser> _sm;
    private readonly ApplicationDbContext _db;
    private readonly ITokenMovilService _tokens;
    private readonly IAuditoriaService _audit;
    private readonly int _minutosBloqueo;

    public AuthApiController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, ApplicationDbContext db,
        ITokenMovilService tokens, IAuditoriaService audit, Microsoft.Extensions.Options.IOptions<IdentityOptions> id)
    {
        _um = um; _sm = sm; _db = db; _tokens = tokens; _audit = audit;
        _minutosBloqueo = (int)id.Value.Lockout.DefaultLockoutTimeSpan.TotalMinutes;
    }

    /// <summary>CU22 — Inicia sesión en la app móvil y devuelve un token JWT.</summary>
    [HttpPost("token"), AllowAnonymous, EnableRateLimiting("token-movil")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Token(SolicitudToken s)
    {
        var u = await _um.FindByEmailAsync(s.Correo.Trim());
        if (u is null) return Problema(401, "Usuario o contraseña incorrectos. Verifique sus datos e intente nuevamente.");

        var r = await _sm.CheckPasswordSignInAsync(u, s.Contrasena, lockoutOnFailure: true);
        if (r.IsLockedOut)
            return Problema(403, $"Su cuenta ha sido bloqueada por {_minutosBloqueo} minutos debido a múltiples intentos fallidos. Comuníquese con el administrador del sistema.");
        if (r.IsNotAllowed) return Problema(403, "Su cuenta de usuario ha sido desactivada. Comuníquese con el administrador del sistema.");
        if (!r.Succeeded) return Problema(401, "Usuario o contraseña incorrectos. Verifique sus datos e intente nuevamente.");

        // RN-29: solo cuentas con rol Repartidor vinculadas a un repartidor activo
        var rep = await _db.Repartidores.AsNoTracking().Include(x => x.ZonaHabitual).FirstOrDefaultAsync(x => x.UsuarioId == u.Id);
        if (!await _um.IsInRoleAsync(u, Roles.Repartidor) || rep is null || !rep.Activo) return Problema(403, NoHabilitada);

        var t = _tokens.Emitir(u, rep, await _um.GetSecurityStampAsync(u));
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.Id), new Claim(ClaimTypes.Name, u.UserName ?? u.Id)
        }, "jwt"));
        await _audit.RegistrarAsync("IniciarSesionMovil", "Repartidor", rep.Id.ToString(), $"{rep.Nombre}; token válido hasta {t.ExpiraUtc:u}");
        return Ok(new TokenDto(t.Token, t.ExpiraUtc, new RepartidorDto(rep.Id, rep.Nombre, rep.Tipo, rep.ZonaHabitual?.Nombre)));
    }

    /// <summary>CU23 — Cierra la sesión: revoca todos los tokens emitidos a la cuenta.</summary>
    [HttpPost("logout"), Authorize(Roles = Roles.Repartidor)]
    [ProducesResponseType<MensajeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var u = await _um.FindByIdAsync(User.UsuarioId());
        if (u is null) return Unauthorized();
        await _um.UpdateSecurityStampAsync(u);
        await _audit.RegistrarAsync("CerrarSesionMovil", "Repartidor", User.RepartidorId().ToString());
        return Ok(new MensajeDto("Sesión cerrada. Vuelva a iniciar sesión para continuar."));
    }

    /// <summary>Datos del repartidor del token.</summary>
    [HttpGet("yo"), Authorize(Roles = Roles.Repartidor)]
    [ProducesResponseType<RepartidorDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Yo()
    {
        var id = User.RepartidorId();
        var rep = await _db.Repartidores.AsNoTracking().Include(x => x.ZonaHabitual).FirstAsync(x => x.Id == id);
        return Ok(new RepartidorDto(rep.Id, rep.Nombre, rep.Tipo, rep.ZonaHabitual?.Nombre));
    }

    private ObjectResult Problema(int estado, string detalle) => Problem(detail: detalle, statusCode: estado);
}
