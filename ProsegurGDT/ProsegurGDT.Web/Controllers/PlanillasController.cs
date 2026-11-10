using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU17 — Gestionar planilla de armado de ruta
[Authorize(Roles = Roles.Clasificador)]
public class PlanillasController : Controller
{
    private readonly IPlanillaArmadoService _planillas;
    private readonly IAuditoriaService _audit;

    public PlanillasController(IPlanillaArmadoService planillas, IAuditoriaService audit)
    { _planillas = planillas; _audit = audit; }

    public async Task<IActionResult> Index() => View(await _planillas.ListarLotesAsync());

    [HttpGet]
    public async Task<IActionResult> Armar(int id)
    {
        var vm = await _planillas.CargarAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Armar(ArmarPlanillaVM entrada)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var resultado = await _planillas.GuardarAsync(entrada, userId);

        if (!resultado.Ok)
        {
            var vm = await _planillas.CargarAsync(entrada.LoteId);
            if (vm is null) return NotFound();

            // Conserva lo que el usuario escribió, tomando zonas y cantidades de la base
            ModelState.Clear();
            vm.Observacion = entrada.Observacion;
            foreach (var z in vm.Zonas)
            {
                var enviada = entrada.Zonas?.FirstOrDefault(x => x.ZonaId == z.ZonaId);
                if (enviada is null) continue;
                z.TipoRepartidorRequerido = enviada.TipoRepartidorRequerido;
                z.OrdenVisita = enviada.OrdenVisita;
                z.Observacion = enviada.Observacion;
            }
            vm.Errores = resultado.Errores;
            return View(vm);
        }

        await _audit.RegistrarAsync(resultado.Creada ? "GenerarPlanillaArmado" : "ModificarPlanillaArmado",
            "PlanillaArmado", resultado.PlanillaId.ToString(), resultado.Detalle);
        TempData["Ok"] = resultado.Creada
            ? "Planilla de armado de ruta guardada exitosamente."
            : "Planilla de armado de ruta actualizada exitosamente.";
        return RedirectToAction(nameof(Detalle), new { id = resultado.PlanillaId });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var vm = await _planillas.DetalleAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }
}
