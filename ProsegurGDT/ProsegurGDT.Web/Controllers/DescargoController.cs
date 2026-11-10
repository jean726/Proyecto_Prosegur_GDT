using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU09 — Descargo de tarjetas · CU11 — Organizar la documentación por mensajero (Descargador)
[Authorize(Roles = Roles.Descargador)]
public class DescargoController : Controller
{
    private readonly IDescargoService _descargo;
    private readonly IAuditoriaService _audit;

    public DescargoController(IDescargoService descargo, IAuditoriaService audit)
    { _descargo = descargo; _audit = audit; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index() => View(await _descargo.ListarAsync());

    // id = hojaRutaId
    [HttpGet]
    public async Task<IActionResult> Procesar(int id)
    {
        var vm = await _descargo.ProcesarAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cargos(CargosVM m) =>
        await Responder(m.HojaRutaId, await _descargo.RegistrarCargosAsync(m.HojaRutaId, m.Cantidad, UserId), "RegistrarCargosEntrega");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SinDocumentacion(MotivoVM m)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Procesar), new { id = m.Id });
        }
        return await Responder(m.Id, await _descargo.MarcarSinDocumentacionAsync(m.Id, m.Texto, UserId), "HojaRutaSinCargos");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(RegistrarResultadoVM m)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Procesar), new { id = m.HojaRutaId });
        }
        return await Responder(m.HojaRutaId, await _descargo.RegistrarResultadoAsync(m, UserId), "DescargoTarjeta");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id) =>
        await Responder(id, await _descargo.CerrarAsync(id, UserId), "CerrarDescargoRuta");

    [HttpGet]
    public async Task<IActionResult> Legajo(int id)
    {
        var vm = await _descargo.LegajoAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Organizar(int id)
    {
        var r = await _descargo.MarcarOrganizadaAsync(id);
        if (r.Detalle is not null) await _audit.RegistrarAsync("OrganizarDocumentacionMensajero", "HojaRuta", id.ToString(), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Legajo), new { id });
    }

    private async Task<IActionResult> Responder(int hojaRutaId, ResultadoDescargoOp r, string auditoria)
    {
        if (r.Detalle is not null) await _audit.RegistrarAsync(auditoria, "HojaRuta", hojaRutaId.ToString(), r.Detalle);
        TempData[r.Ok ? (r.Aviso ? "Aviso" : "Ok") : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Procesar), new { id = hojaRutaId });
    }
}
