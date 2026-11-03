using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager) => _signInManager = signInManager;

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

        var r = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (r.Succeeded) return LocalRedirect(returnUrl);

        if (r.IsLockedOut)
            ModelState.AddModelError(string.Empty, "Su cuenta ha sido bloqueada temporalmente por múltiples intentos fallidos. Comuníquese con el administrador del sistema.");
        else if (r.IsNotAllowed)
            ModelState.AddModelError(string.Empty, "Su cuenta de usuario ha sido desactivada. Comuníquese con el administrador del sistema.");
        else
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos. Verifique sus datos e intente nuevamente.");
        return Page();
    }
}