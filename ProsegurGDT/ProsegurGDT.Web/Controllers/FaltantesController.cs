using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU21 — Localizar tarjetas faltantes
[Authorize(Roles = Roles.Buscador)]
public class FaltantesController : Controller
{
    private readonly IFaltantesService _faltantes;
    private readonly IAuditoriaService _audit;

    public FaltantesController(IFaltantesService faltantes, IAuditoriaService audit)
    { _faltantes = faltantes; _audit = audit; }

    public async Task<IActionResult> Index() => View(await _faltantes.ListarAsync());

    // id = incidenciaId
    [HttpGet]
    public async Task<IActionResult> Localizar(int id)
    {
        var vm = await _faltantes.DetalleAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Localizar(int id, string? ubicacion)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var r = await _faltantes.LocalizarAsync(id, ubicacion, userId);
        if (!r.Ok) { TempData["Error"] = r.Error; return RedirectToAction(nameof(Localizar), new { id }); }

        await _audit.RegistrarAsync("LocalizarTarjetaFaltante", "IncidenciaConciliacion", id.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData["Ok"] = "Tarjeta localizada y devuelta a custodia. El Clasificador puede volver a verificar el lote.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NoLocalizada(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var r = await _faltantes.NoLocalizarAsync(id, userId);
        if (!r.Ok) { TempData["Error"] = r.Error; return RedirectToAction(nameof(Localizar), new { id }); }

        await _audit.RegistrarAsync("TarjetaFaltanteNoLocalizada", "IncidenciaConciliacion", id.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData["Ok"] = "Tarjeta marcada como no localizada (extraviada con indicios de fraude).";
        return RedirectToAction(nameof(Index));
    }
}
