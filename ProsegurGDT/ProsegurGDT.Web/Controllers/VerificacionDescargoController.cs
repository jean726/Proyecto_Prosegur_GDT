using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

// CU09 (flujo alternativo) — El Buscador verifica físicamente las tarjetas con discrepancia derivadas por el Descargador
[Authorize(Roles = Roles.Buscador)]
public class VerificacionDescargoController : Controller
{
    private readonly IDescargoService _descargo;
    private readonly IAuditoriaService _audit;

    public VerificacionDescargoController(IDescargoService descargo, IAuditoriaService audit)
    { _descargo = descargo; _audit = audit; }

    public async Task<IActionResult> Index() => View(await _descargo.ListarVerificacionesAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolver(ResolverVerificacionVM m)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Index));
        }
        var r = await _descargo.ResolverVerificacionAsync(m.Id, m.Resuelta, m.Observacion, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (r.Ok) await _audit.RegistrarAsync("VerificarDescargo", "DescargoTarjeta", m.Id.ToString(CultureInfo.InvariantCulture), r.Detalle);
        TempData[r.Ok ? "Ok" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
