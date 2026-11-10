using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU20 — Conciliar tarjetas
[Authorize(Roles = Roles.Clasificador)]
public class ConciliacionController : Controller
{
    private readonly IConciliacionService _conciliacion;
    private readonly IAuditoriaService _audit;

    public ConciliacionController(IConciliacionService conciliacion, IAuditoriaService audit)
    { _conciliacion = conciliacion; _audit = audit; }

    public async Task<IActionResult> Index() => View(await _conciliacion.ListarAsync());

    // id = loteId
    [HttpGet]
    public async Task<IActionResult> Verificar(int id)
    {
        var vm = await _conciliacion.CargarAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Verificar(VerificarConciliacionVM entrada)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var r = await _conciliacion.VerificarAsync(entrada.LoteId, entrada.CodigosFisicos, userId);

        if (!r.Ok)
        {
            var vm = await _conciliacion.CargarAsync(entrada.LoteId, entrada.CodigosFisicos);
            if (vm is null) return NotFound();
            ModelState.Clear();
            vm.Errores = r.Errores;
            return View(vm);
        }

        await _audit.RegistrarAsync("ConciliarTarjetas", "ConciliacionLote", r.ConciliacionId.ToString(), r.Detalle);
        TempData["Ok"] = "Conciliación registrada.";
        return RedirectToAction(nameof(Detalle), new { id = r.ConciliacionId });
    }

    // id = conciliacionId
    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var vm = await _conciliacion.DetalleAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }
}
