using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU10 — Reporte de entregas · CU13 — Reporte de descargo · CU14 — Derivar al banco (Descargador)
[Authorize(Roles = Roles.Descargador)]
public class ReportesDescargoController : Controller
{
    private readonly IReportesDescargoService _reportes;
    private readonly IAuditoriaService _audit;

    public ReportesDescargoController(IReportesDescargoService reportes, IAuditoriaService audit)
    { _reportes = reportes; _audit = audit; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index() => View(await _reportes.ListarAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(DateOnly fecha, TipoReporteDescargo tipo)
    {
        if (!Enum.IsDefined(tipo)) return BadRequest();
        var r = await _reportes.GenerarAsync(fecha, tipo, UserId);
        if (!r.Ok)
        {
            TempData["Error"] = r.Mensaje;
            return RedirectToAction(nameof(Index));
        }
        await _audit.RegistrarAsync(tipo == TipoReporteDescargo.Entregas ? "GenerarReporteEntregasDescargo" : "GenerarReporteDescargo",
            "ReporteDescargo", r.ReporteId.ToString(), r.Detalle);
        TempData["Ok"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { id = r.ReporteId });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var vm = await _reportes.DetalleAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Derivar(int id)
    {
        var r = await _reportes.DerivarAlBancoAsync(id, UserId);
        if (r.Ok) await _audit.RegistrarAsync("DerivarReporteDescargoAlBanco", "ReporteDescargo", id.ToString(), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
