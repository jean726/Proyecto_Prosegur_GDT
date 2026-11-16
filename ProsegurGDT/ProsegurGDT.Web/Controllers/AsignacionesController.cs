using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU19 — Generar hoja de asignación a mensajeros
[Authorize(Roles = Roles.AnalistaDeRutas)]
public class AsignacionesController : Controller
{
    private readonly IAsignacionService _asignaciones;
    private readonly IAuditoriaService _audit;

    public AsignacionesController(IAsignacionService asignaciones, IAuditoriaService audit)
    { _asignaciones = asignaciones; _audit = audit; }

    public async Task<IActionResult> Index() => View(await _asignaciones.ListarAsync());

    // id = loteId
    [HttpGet]
    public async Task<IActionResult> Generar(int id)
    {
        var vm = await _asignaciones.CargarAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(GenerarHojaVM entrada)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var r = await _asignaciones.GenerarAsync(entrada, userId);

        if (!r.Ok)
        {
            var vm = await _asignaciones.CargarAsync(entrada.LoteId, entrada);
            if (vm is null) return NotFound();
            ModelState.Clear();
            vm.Errores = r.Errores;
            vm.Advertencias = r.Advertencias;
            return View(vm);
        }

        await _audit.RegistrarAsync("GenerarHojaAsignacion", "HojaAsignacion", r.HojaId.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData["Ok"] = "Hoja de asignación generada exitosamente.";
        return RedirectToAction(nameof(Detalle), new { id = r.HojaId });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var vm = await _asignaciones.DetalleAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    // Vista sin menú lateral, lista para imprimir
    [HttpGet]
    public async Task<IActionResult> Imprimir(int id)
    {
        var vm = await _asignaciones.DetalleAsync(id);
        if (vm is null) return NotFound();
        await _audit.RegistrarAsync("ImprimirHojaAsignacion", "HojaAsignacion", id.ToString(CultureInfo.InvariantCulture), $"Lote {vm.CodigoHojaControl}");
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Csv(int id)
    {
        var archivo = await _asignaciones.CsvAsync(id);
        if (archivo is null) return NotFound();
        await _audit.RegistrarAsync("DescargarHojaAsignacion", "HojaAsignacion", id.ToString(CultureInfo.InvariantCulture), archivo.Value.Nombre);
        return File(archivo.Value.Contenido, "text/csv", archivo.Value.Nombre);
    }
}
