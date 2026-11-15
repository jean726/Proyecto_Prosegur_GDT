using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly int _maxIntentos;
    private readonly int _minutosBloqueo;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IOptions<IdentityOptions> opciones)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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

        // RN-29: el Repartidor opera solo desde la aplicación móvil
        if (await _userManager.FindByNameAsync(Input.Email) is { } cuenta && await _userManager.IsInRoleAsync(cuenta, ProsegurGDT.Web.Domain.Roles.Repartidor)
            && await _userManager.CheckPasswordAsync(cuenta, Input.Password))
        {
            ModelState.AddModelError(string.Empty, "Las cuentas de repartidor ingresan desde la aplicación móvil, no desde la plataforma web.");
            return Page();
        }

        var r = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (r.Succeeded) return LocalRedirect(returnUrl);

        if (r.IsLockedOut)
            ModelState.AddModelError(string.Empty, $"Su cuenta ha sido bloqueada por {_minutosBloqueo} minutos debido a múltiples intentos fallidos. Comuníquese con el administrador del sistema.");
        else if (r.IsNotAllowed)
            ModelState.AddModelError(string.Empty, "Su cuenta de usuario ha sido desactivada. Comuníquese con el administrador del sistema.");
        else
            ModelState.AddModelError(string.Empty, await MensajeCredencialesAsync());
        return Page();
    }

    private async Task<string> MensajeCredencialesAsync()
    {
        const string generico = "Usuario o contraseña incorrectos. Verifique sus datos e intente nuevamente.";
        var u = await _userManager.FindByNameAsync(Input.Email);
        if (u is null) return generico;
        var restantes = _maxIntentos - await _userManager.GetAccessFailedCountAsync(u);
        if (restantes <= 0) return generico;
        return restantes == 1
            ? "Usuario o contraseña incorrectos. Le queda 1 intento antes del bloqueo temporal."
            : $"Usuario o contraseña incorrectos. Le quedan {restantes} intentos antes del bloqueo temporal.";
    }
}