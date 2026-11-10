using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU18 — Gestionar reporte de densidad de entregas
[Authorize(Roles = Roles.Clasificador)]
public class DensidadController : Controller
{
    private readonly IDensidadService _densidad;
    private readonly IAuditoriaService _audit;

    public DensidadController(IDensidadService densidad, IAuditoriaService audit)
    { _densidad = densidad; _audit = audit; }

    public async Task<IActionResult> Index() => View(await _densidad.ListarAsync());

    // id = loteId: muestra el reporte si existe; si no, la vista previa con el botón de generar
    [HttpGet]
    public async Task<IActionResult> Ver(int id)
    {
        var vm = await _densidad.VerAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(int loteId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var r = await _densidad.GenerarAsync(loteId, userId);
        if (!r.Ok)
        {
            TempData["Error"] = r.Error;
            return RedirectToAction(nameof(Ver), new { id = loteId });
        }

        await _audit.RegistrarAsync("GenerarReporteDensidad", "ReporteDensidad", r.ReporteId.ToString(), r.Detalle);
        TempData["Ok"] = "Reporte de densidad generado exitosamente. Queda disponible para el Analista de Rutas.";
        return RedirectToAction(nameof(Ver), new { id = loteId });
    }

    // id = reporteId: vista sin menú lateral, lista para imprimir
    [HttpGet]
    public async Task<IActionResult> Imprimir(int id)
    {
        var vm = await _densidad.ObtenerAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }
}
