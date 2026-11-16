using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU07 — Diseñar reporte de entregas · CU08 — Remitir reporte de entregas (Supervisor)
[Authorize(Roles = Roles.Supervisor)]
public class ReportesEntregasController : Controller
{
    private readonly IReporteEntregasService _reportes;
    private readonly IAuditoriaService _audit;

    public ReportesEntregasController(IReporteEntregasService reportes, IAuditoriaService audit)
    { _reportes = reportes; _audit = audit; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index() => View(await _reportes.ListarAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(DateOnly fecha)
    {
        var r = await _reportes.CrearAsync(fecha, UserId);
        if (!r.Ok)
        {
            TempData["Error"] = r.Mensaje;
            return RedirectToAction(nameof(Index));
        }
        await _audit.RegistrarAsync("DisenarReporteEntregas", "ReporteEntregas", r.ReporteId.ToString(CultureInfo.InvariantCulture), r.Detalle);
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
    public async Task<IActionResult> Actualizar(ObservacionReporteVM m)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Detalle), new { id = m.Id });
        }
        var r = await _reportes.ActualizarAsync(m.Id, m.Observacion);
        if (r.Ok) await _audit.RegistrarAsync("ActualizarReporteEntregas", "ReporteEntregas", m.Id.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { id = m.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remitir(int id)
    {
        var r = await _reportes.RemitirAsync(id, UserId);
        if (r.Ok) await _audit.RegistrarAsync("RemitirReporteEntregas", "ReporteEntregas", id.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
