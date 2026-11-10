using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU06 — Generar reporte de fraude (Verificador)
[Authorize(Roles = Roles.Verificador)]
public class FraudeController : Controller
{
    private readonly IFraudeService _fraude;
    private readonly IAuditoriaService _audit;

    public FraudeController(IFraudeService fraude, IAuditoriaService audit)
    { _fraude = fraude; _audit = audit; }

    public async Task<IActionResult> Index() => View(await _fraude.ListarAsync());

    // id = hojaRutaId
    [HttpGet]
    public async Task<IActionResult> Revisar(int id)
    {
        var vm = await _fraude.RevisarAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nuevo(int hojaRutaId, int tarjetaId)
    {
        var vm = await _fraude.NuevoAsync(hojaRutaId, tarjetaId);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(NuevoReporteFraudeVM entrada)
    {
        if (ModelState.IsValid)
        {
            var r = await _fraude.CrearAsync(entrada, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (r.Ok)
            {
                await _audit.RegistrarAsync("GenerarReporteFraude", "ReporteFraude", r.ReporteId.ToString(), r.Detalle);
                TempData["Ok"] = r.Mensaje;
                return RedirectToAction(nameof(Detalle), new { id = r.ReporteId });
            }
            ModelState.AddModelError(string.Empty, r.Mensaje);
        }

        var vm = await _fraude.NuevoAsync(entrada.HojaRutaId, entrada.TarjetaId);
        if (vm is null) return NotFound();
        vm.Tipo = entrada.Tipo;
        vm.Descripcion = entrada.Descripcion;
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var vm = await _fraude.DetalleAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }
}
