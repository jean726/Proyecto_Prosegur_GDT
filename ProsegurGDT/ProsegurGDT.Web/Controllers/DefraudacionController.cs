using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU12 — Mantener apartado de defraudación. El Descargador registra el acta; el Encargado de Bóveda ingresa las tarjetas.
[Authorize(Roles = Roles.Descargador + "," + Roles.EncargadoDeBoveda)]
public class DefraudacionController : Controller
{
    private readonly IDefraudacionService _fraude;
    private readonly IAuditoriaService _audit;

    public DefraudacionController(IDefraudacionService fraude, IAuditoriaService audit)
    { _fraude = fraude; _audit = audit; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var vm = await _fraude.ListarAsync();
        vm.PuedeRegistrar = User.IsInRole(Roles.Descargador);
        vm.PuedeIngresar = User.IsInRole(Roles.EncargadoDeBoveda);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Descargador)]
    public async Task<IActionResult> Registrar(RegistrarActaVM m)
    {
        var r = await _fraude.RegistrarAsync(m, UserId);
        if (r.Ok) await _audit.RegistrarAsync("RegistrarActaFraude", "Tarjeta", m.TarjetaId.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.EncargadoDeBoveda)]
    public async Task<IActionResult> Ingresar(IngresoBovedaVM m)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Index));
        }
        var r = await _fraude.IngresarABovedaAsync(m.Id, m.Ubicacion, UserId);
        if (r.Ok) await _audit.RegistrarAsync("IngresarFraudeABoveda", "ActaFraude", m.Id.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = Roles.EncargadoDeBoveda)]
    public async Task<IActionResult> Inventario() => View(await _fraude.InventarioAsync());
}
