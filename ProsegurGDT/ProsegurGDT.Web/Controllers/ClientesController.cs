using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

[Authorize(Roles = Roles.Supervisor + "," + Roles.AuxiliarTelefonista)]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _audit;
    private readonly IImportadorFichaService _importador;

    public ClientesController(ApplicationDbContext db, IAuditoriaService audit, IImportadorFichaService importador)
    { _db = db; _audit = audit; _importador = importador; }

    public async Task<IActionResult> Index(string? q, EstadoTarjeta? estado)
    {
        var query = _db.Tarjetas.Include(t => t.Cliente).Include(t => t.Lote).ThenInclude(l => l!.Remito).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(t => t.Codigo.Contains(q) || t.Cliente!.NombreCompleto.Contains(q) || t.Cliente!.DocumentoIdentidad.Contains(q));
        }
        if (estado.HasValue) query = query.Where(t => t.Estado == estado.Value);
        ViewBag.Q = q;
        return View(await query.OrderByDescending(t => t.Id).Take(200).ToListAsync());
    }

    [Authorize(Roles = Roles.Supervisor)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var c = await _db.Clientes.FindAsync(id);
        if (c is null) return NotFound();
        return View(new ClienteEditarVM { Id = c.Id, NombreCompleto = c.NombreCompleto, Telefono = c.Telefono, Direccion = c.Direccion });
    }

    [Authorize(Roles = Roles.Supervisor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(ClienteEditarVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var c = await _db.Clientes.FindAsync(vm.Id);
        if (c is null) return NotFound();
        c.NombreCompleto = vm.NombreCompleto.Trim();
        c.Telefono = string.IsNullOrWhiteSpace(vm.Telefono) ? null : vm.Telefono.Trim();
        c.Direccion = vm.Direccion.Trim();
        c.DireccionNormalizada = NormalizadorDirecciones.Normalizar(c.Direccion);
        await _db.SaveChangesAsync();
        await _audit.RegistrarAsync("ModificarFichaCliente", "Cliente", c.Id.ToString(), c.DocumentoIdentidad);
        TempData["Ok"] = "Ficha del cliente actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Supervisor)]
    [HttpGet]
    public async Task<IActionResult> Importar()
    {
        var vm = new ImportarFichaVM();
        await CargarLotes(vm);
        return View(vm);
    }

    [Authorize(Roles = Roles.Supervisor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Importar(ImportarFichaVM vm)
    {
        ModelState.Clear();
        await CargarLotes(vm);

        if (vm.Archivo is null || vm.Archivo.Length == 0)
        { vm.Errores = new() { "Seleccione el archivo CSV remitido por UNIBANCA." }; return View(vm); }
        if (vm.Archivo.Length > 2_000_000)
        { vm.Errores = new() { "El archivo debe pesar menos de 2 MB." }; return View(vm); }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var stream = vm.Archivo.OpenReadStream();
        var r = await _importador.ImportarAsync(vm.LoteId, stream, vm.Archivo.FileName, userId);

        if (!r.Ok) { vm.Errores = r.Errores; return View(vm); }

        await _audit.RegistrarAsync("GenerarFichaClientes", "Lote", vm.LoteId.ToString(), $"{r.Cantidad} tarjetas");
        TempData["Ok"] = $"Ficha de clientes generada: {r.Cantidad} tarjetas registradas para el lote {r.CodigoHoja}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarLotes(ImportarFichaVM vm)
    {
        var lotes = await _db.Lotes.Include(l => l.Remito)
            .Where(l => l.Estado == EstadoLote.IngresadoEnCustodia && !_db.Tarjetas.Any(t => t.LoteId == l.Id))
            .OrderByDescending(l => l.Id).ToListAsync();
        vm.Lotes = lotes.Select(l => new SelectListItem($"{l.CodigoHojaControl} — {l.Remito?.Numero} — {l.CantidadContada} tarjetas", l.Id.ToString())).ToList();
    }
}