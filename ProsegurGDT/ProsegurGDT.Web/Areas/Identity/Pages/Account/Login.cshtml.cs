using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services.Seguridad;

namespace ProsegurGDT.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILimitadorLoginWeb _limitador;
    private readonly int _maxIntentos;
    private readonly int _minutosBloqueo;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> opciones, ILimitadorLoginWeb limitador)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _limitador = limitador;
        _maxIntentos = opciones.Value.Lockout.MaxFailedAccessAttempts;
        _minutosBloqueo = (int)opciones.Value.Lockout.DefaultLockoutTimeSpan.TotalMinutes;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingrese su correo electrónico.")]
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Ingrese su contraseña.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();

        // Límite de intentos por minuto desde una misma dirección IP (además del bloqueo de cada cuenta)
        if (!_limitador.Permitir(HttpContext))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            Response.Headers.RetryAfter = "60";
            ModelState.AddModelError(string.Empty, "Demasiados intentos de inicio de sesión desde este equipo o red. Espere un minuto e intente nuevamente.");
            return Page();
        }

        // RN-29: el Repartidor opera solo desde la aplicación móvil. La contraseña se comprueba con el mismo
        // conteo de intentos fallidos y bloqueo que cualquier cuenta: el aviso solo aparece si es correcta.
        if (await _userManager.FindByNameAsync(Input.Email) is { } cuenta && await _userManager.IsInRoleAsync(cuenta, Roles.Repartidor))
        {
            var comprobacion = await _signInManager.CheckPasswordSignInAsync(cuenta, Input.Password, lockoutOnFailure: true);
            ModelState.AddModelError(string.Empty, comprobacion.Succeeded
                ? "Las cuentas de repartidor ingresan desde la aplicación móvil, no desde la plataforma web."
                : Mensaje(comprobacion));
            return Page();
        }

        var r = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (r.Succeeded) return LocalRedirect(returnUrl);

        ModelState.AddModelError(string.Empty, Mensaje(r));
        return Page();
    }

    // El mensaje de credenciales incorrectas es el mismo exista o no la cuenta: no revela qué correos están registrados.
    private string Mensaje(Microsoft.AspNetCore.Identity.SignInResult r)
    {
        if (r.IsLockedOut)
            return $"Su cuenta ha sido bloqueada por {_minutosBloqueo} minutos debido a múltiples intentos fallidos. Comuníquese con el administrador del sistema.";
        if (r.IsNotAllowed)
            return "Su cuenta de usuario ha sido desactivada. Comuníquese con el administrador del sistema.";
        return $"Usuario o contraseña incorrectos. Verifique sus datos e intente nuevamente. Tras {_maxIntentos} intentos fallidos la cuenta se bloquea {_minutosBloqueo} minutos.";
    }
}