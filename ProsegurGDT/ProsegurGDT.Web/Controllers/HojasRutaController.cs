using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU05 — Administrar hoja de ruta a mensajeros. El Supervisor gestiona; el Auxiliar Telefonista solo consulta.
[Authorize(Roles = Roles.Supervisor + "," + Roles.AuxiliarTelefonista)]
public class HojasRutaController : Controller
{
    private readonly IHojaRutaService _hojas;
    private readonly IAuditoriaService _audit;

    public HojasRutaController(IHojaRutaService hojas, IAuditoriaService audit)
    { _hojas = hojas; _audit = audit; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var vm = await _hojas.ListarAsync();
        vm.PuedeGestionar = User.IsInRole(Roles.Supervisor);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Supervisor)]
    public async Task<IActionResult> Generar(int id)
    {
        var r = await _hojas.GenerarAsync(id, UserId);
        if (r.Ok) await _audit.RegistrarAsync("GenerarHojasRuta", "HojaAsignacion", id.ToString(), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var vm = await _hojas.DetalleAsync(id);
        if (vm is null) return NotFound();
        vm.PuedeGestionar = User.IsInRole(Roles.Supervisor);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Supervisor)]
    public Task<IActionResult> Aprobar(int id) =>
        Ejecutar(id, _ => _hojas.AprobarAsync(id, UserId), "AprobarHojaRuta");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Supervisor)]
    public async Task<IActionResult> Discrepancia(MotivoVM m)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Detalle), new { id = m.Id });
        }
        return await Ejecutar(m.Id, _ => _hojas.ReportarDiscrepanciaAsync(m.Id, m.Texto), "ReportarDiscrepanciaHojaRuta");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Supervisor)]
    public Task<IActionResult> Resolver(int id) =>
        Ejecutar(id, _ => _hojas.ResolverAsync(id), "ResolverDiscrepanciaHojaRuta");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Supervisor)]
    public Task<IActionResult> Firmar(int id) =>
        Ejecutar(id, _ => _hojas.RegistrarFirmaAsync(id, UserId), "FirmarHojaRuta");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Supervisor)]
    public async Task<IActionResult> SinFirma(MotivoVM m)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Detalle), new { id = m.Id });
        }
        return await Ejecutar(m.Id, _ => _hojas.RegistrarSinFirmaAsync(m.Id, m.Texto), "HojaRutaSinFirma");
    }

    private async Task<IActionResult> Ejecutar(int id, Func<int, Task<ResultadoHojaRuta>> accion, string auditoria)
    {
        var r = await accion(id);
        if (r.Detalle is not null) await _audit.RegistrarAsync(auditoria, "HojaRuta", id.ToString(), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
