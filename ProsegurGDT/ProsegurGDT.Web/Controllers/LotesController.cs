using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Controllers;

[Authorize(Roles = Roles.Supervisor)]
public class LotesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public LotesController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var lotes = await _db.Lotes
            .Include(l => l.Remito).ThenInclude(r => r!.Banco)
            .Include(l => l.RegistradoPor)
            .OrderByDescending(l => l.FechaHoraRegistro)
            .Take(50)
            .ToListAsync();
        return View(lotes);
    }

    [HttpGet]
    public IActionResult Registrar() => View(new RegistrarLoteVM());

    // Pasos 2-3 del flujo básico: ingresar el remito y cargar sus datos
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Buscar(RegistrarLoteVM vm)
    {
        ModelState.Clear();
        var numero = vm.NumeroRemito?.Trim();
        vm.RemitoEncontrado = false;
        vm.CantidadContada = null;

        if (string.IsNullOrEmpty(numero))
        {
            vm.Error = "Ingrese el número de remito.";
            return View("Registrar", vm);
        }

        var remito = await _db.Remitos.Include(r => r.Banco)
            .FirstOrDefaultAsync(r => r.Numero == numero);

        if (remito is null)
        {
            vm.Error = "El número de remito ingresado no se encuentra registrado. Verifique los datos e intente nuevamente.";
            return View("Registrar", vm);
        }

        if (await YaIngresado(remito.Id))
        {
            vm.Error = "Este remito ya fue ingresado en custodia.";
            return View("Registrar", vm);
        }

        Rellenar(vm, remito);
        return View("Registrar", vm);
    }

    // Pasos 4-11: contar, comparar y registrar
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(RegistrarLoteVM vm)
    {
        var remito = await _db.Remitos.Include(r => r.Banco)
            .FirstOrDefaultAsync(r => r.Id == vm.RemitoId);
        if (remito is null) return RedirectToAction(nameof(Registrar));

        // Los datos del remito se releen de la base; no se confía en lo que envía el navegador
        Rellenar(vm, remito);

        if (await YaIngresado(remito.Id))
        {
            vm.RemitoEncontrado = false;
            vm.Error = "Este remito ya fue ingresado en custodia.";
            return View("Registrar", vm);
        }

        if (vm.CantidadContada is null || vm.CantidadContada < 0)
        {
            vm.Error = "Ingrese la cantidad de tarjetas contadas físicamente.";
            return View("Registrar", vm);
        }

        var coincide = vm.CantidadContada == remito.CantidadDeclarada;
        var ahora = DateTime.UtcNow;

        var lote = new Lote
        {
            RemitoId = remito.Id,
            FechaRecepcion = DateOnly.FromDateTime(HoraLima.Ahora),
            CantidadDeclarada = remito.CantidadDeclarada,
            CantidadContada = vm.CantidadContada.Value,
            Estado = coincide ? EstadoLote.IngresadoEnCustodia : EstadoLote.DiscrepanciaDetectada,
            RegistradoPorId = _users.GetUserId(User)!,
            FechaHoraRegistro = ahora
        };
        _db.Lotes.Add(lote);
        await _db.SaveChangesAsync();

        if (coincide)
        {
            lote.CodigoHojaControl = $"HC-{HoraLima.Convertir(ahora):yyyyMMdd}-{lote.Id:D5}";
            await _db.SaveChangesAsync();
            TempData["Ok"] = $"Lote registrado satisfactoriamente. Hoja de control de custodia generada con código Nro. {lote.CodigoHojaControl}.";
        }
        else
        {
            var dif = vm.CantidadContada.Value - remito.CantidadDeclarada;
            TempData["Error"] = $"Existe una discrepancia entre el remito y el conteo físico. El lote quedó registrado como discrepancia y no ingresó en custodia. " +
                                $"Declaradas: {remito.CantidadDeclarada}, contadas: {vm.CantidadContada}, diferencia: {dif}.";
        }
        return RedirectToAction(nameof(Index));
    }

    private Task<bool> YaIngresado(int remitoId) =>
        _db.Lotes.AnyAsync(l => l.RemitoId == remitoId && l.Estado == EstadoLote.IngresadoEnCustodia);

    private static void Rellenar(RegistrarLoteVM vm, Remito remito)
    {
        vm.RemitoEncontrado = true;
        vm.RemitoId = remito.Id;
        vm.NumeroRemito = remito.Numero;
        vm.Entidad = remito.Banco?.Nombre;
        vm.CantidadDeclarada = remito.CantidadDeclarada;
        vm.FechaRecepcion = DateOnly.FromDateTime(HoraLima.Ahora);
    }
}