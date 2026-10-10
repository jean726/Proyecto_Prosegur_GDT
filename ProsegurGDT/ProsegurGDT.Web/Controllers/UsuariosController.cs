using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

[Authorize(Roles = Roles.Administrador)]
public class UsuariosController : Controller
{
    private readonly UserManager<ApplicationUser> _um;
    private readonly IAuditoriaService _audit;
    public UsuariosController(UserManager<ApplicationUser> um, IAuditoriaService audit) { _um = um; _audit = audit; }

    public async Task<IActionResult> Index()
    {
        var users = await _um.Users.OrderBy(u => u.NombreCompleto).ToListAsync();
        var filas = new List<UsuarioFilaVM>();
        foreach (var u in users)
            filas.Add(new UsuarioFilaVM
            {
                Id = u.Id,
                NombreCompleto = u.NombreCompleto,
                Email = u.Email ?? "",
                Rol = string.Join(", ", await _um.GetRolesAsync(u)),
                Activo = u.Activo,
                Bloqueado = await _um.IsLockedOutAsync(u)
            });
        return View(filas);
    }

    // ---------- Registrar (subflujo 1; incluye CU27 Asignar rol) ----------
    [HttpGet] public IActionResult Crear() => View(new UsuarioCrearVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(UsuarioCrearVM vm)
    {
        if (!Roles.Todos.Contains(vm.Rol)) ModelState.AddModelError(nameof(vm.Rol), "Seleccione un rol válido.");
        if (!ModelState.IsValid) return View(vm);

        if (await _um.FindByEmailAsync(vm.Email) is not null)
        {
            ModelState.AddModelError(nameof(vm.Email), "El nombre de usuario ingresado ya se encuentra registrado. Ingrese uno diferente.");
            return View(vm);
        }

        var u = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            EmailConfirmed = true,
            Activo = true,
            NombreCompleto = vm.NombreCompleto.Trim(),
            DocumentoIdentidad = vm.DocumentoIdentidad.Trim()
        };
        var r = await _um.CreateAsync(u, vm.Password);
        if (r.Succeeded)
        {
            r = await AsignarRolAsync(u, vm.Rol);
            if (!r.Succeeded) await _um.DeleteAsync(u);   // no dejar usuarios sin rol
        }
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        await _audit.RegistrarAsync("CrearUsuario", "Usuario", u.Id, $"{u.Email}; rol {vm.Rol}");
        TempData["Ok"] = "Usuario registrado exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Modificar (subflujo 2) ----------
    [HttpGet]
    public async Task<IActionResult> Editar(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u is null) return NotFound();
        return View(new UsuarioEditarVM
        {
            Id = u.Id,
            NombreCompleto = u.NombreCompleto,
            DocumentoIdentidad = u.DocumentoIdentidad ?? "",
            Email = u.Email ?? "",
            Rol = (await _um.GetRolesAsync(u)).FirstOrDefault() ?? ""
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(UsuarioEditarVM vm)
    {
        if (!Roles.Todos.Contains(vm.Rol)) ModelState.AddModelError(nameof(vm.Rol), "Seleccione un rol válido.");
        if (!ModelState.IsValid) return View(vm);

        var u = await _um.FindByIdAsync(vm.Id);
        if (u is null) return NotFound();

        var rolActual = (await _um.GetRolesAsync(u)).FirstOrDefault();
        if (rolActual == Roles.Administrador && vm.Rol != Roles.Administrador && await EsUltimoAdminActivoAsync(u))
        {
            ModelState.AddModelError(nameof(vm.Rol), "No se puede quitar el rol al último administrador activo.");
            return View(vm);
        }

        var otro = await _um.FindByEmailAsync(vm.Email);
        if (otro is not null && otro.Id != u.Id)
        {
            ModelState.AddModelError(nameof(vm.Email), "El nombre de usuario ingresado ya se encuentra registrado.");
            return View(vm);
        }

        u.NombreCompleto = vm.NombreCompleto.Trim();
        u.DocumentoIdentidad = vm.DocumentoIdentidad.Trim();
        if (!string.Equals(u.Email, vm.Email, StringComparison.OrdinalIgnoreCase)) { u.Email = vm.Email; u.UserName = vm.Email; }

        var r = await _um.UpdateAsync(u);
        var cambioPass = false;
        if (r.Succeeded && !string.IsNullOrWhiteSpace(vm.NuevaPassword))
        {
            var token = await _um.GeneratePasswordResetTokenAsync(u);
            r = await _um.ResetPasswordAsync(u, token, vm.NuevaPassword);
            cambioPass = r.Succeeded;
        }
        if (r.Succeeded && rolActual != vm.Rol)
        {
            r = await AsignarRolAsync(u, vm.Rol);
            if (r.Succeeded) await _um.UpdateSecurityStampAsync(u);   // refresca la sesión con el rol nuevo
        }
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        await _audit.RegistrarAsync("ModificarUsuario", "Usuario", u.Id,
            $"{u.Email}; rol {rolActual}→{vm.Rol}; contraseña cambiada: {(cambioPass ? "sí" : "no")}");
        TempData["Ok"] = "Datos del usuario actualizados exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Eliminar = desactivar (subflujo 3) ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u is null) return NotFound();
        if (u.Id == _um.GetUserId(User)) { TempData["Error"] = "No puede desactivar su propia cuenta."; return RedirectToAction(nameof(Index)); }
        if (await EsUltimoAdminActivoAsync(u)) { TempData["Error"] = "No se puede desactivar al último administrador activo."; return RedirectToAction(nameof(Index)); }

        u.Activo = false;
        await _um.UpdateAsync(u);
        await _um.UpdateSecurityStampAsync(u);   // invalida sus sesiones abiertas
        await _audit.RegistrarAsync("DesactivarUsuario", "Usuario", u.Id, u.Email);
        TempData["Ok"] = "Usuario desactivado exitosamente. Sus credenciales de acceso han sido revocadas.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivar(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u is null) return NotFound();
        u.Activo = true;
        await _um.UpdateAsync(u);
        await _audit.RegistrarAsync("ReactivarUsuario", "Usuario", u.Id, u.Email);
        TempData["Ok"] = "Usuario reactivado.";
        return RedirectToAction(nameof(Index));
    }

    // Cuenta bloqueada por intentos fallidos (CU22): el login pide contactar al Administrador
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Desbloquear(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u is null) return NotFound();
        await _um.SetLockoutEndDateAsync(u, null);
        await _um.ResetAccessFailedCountAsync(u);
        await _audit.RegistrarAsync("DesbloquearUsuario", "Usuario", u.Id, u.Email);
        TempData["Ok"] = "Cuenta desbloqueada.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- CU27 Asignar Rol a Usuario (reutilizado por Crear y Editar) ----------
    private async Task<IdentityResult> AsignarRolAsync(ApplicationUser u, string rol)
    {
        var actuales = await _um.GetRolesAsync(u);
        if (actuales.Count == 1 && actuales[0] == rol) return IdentityResult.Success;
        if (actuales.Count > 0)
        {
            var quitar = await _um.RemoveFromRolesAsync(u, actuales);
            if (!quitar.Succeeded) return quitar;
        }
        return await _um.AddToRoleAsync(u, rol);
    }

    private async Task<bool> EsUltimoAdminActivoAsync(ApplicationUser u)
    {
        if (!await _um.IsInRoleAsync(u, Roles.Administrador)) return false;
        var admins = await _um.GetUsersInRoleAsync(Roles.Administrador);
        return !admins.Any(a => a.Activo && a.Id != u.Id);
    }
}