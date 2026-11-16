using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

[Authorize(Roles = Roles.Buscador + "," + Roles.Clasificador + "," + Roles.AnalistaDeRutas)]
public class DireccionesFraudulentasController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAddressRiskService _riesgo;
    private readonly IAuditoriaService _audit;
    private readonly ITarjetaEstadoService _estados;

    public DireccionesFraudulentasController(ApplicationDbContext db, IAddressRiskService riesgo,
        IAuditoriaService audit, ITarjetaEstadoService estados)
    { _db = db; _riesgo = riesgo; _audit = audit; _estados = estados; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Consulta (RF21) y prueba del motor ----------
    public async Task<IActionResult> Index(string? probar, EstadoDireccion? estado)
    {
        var q = _db.DireccionesFraudulentas.AsQueryable();
        if (estado.HasValue) q = q.Where(d => d.Estado == estado.Value);
        var vm = new DireccionesIndexVM
        {
            Items = await q.OrderByDescending(d => d.Id).Take(200).ToListAsync(),
            Probar = probar,
            UmbralMedio = _riesgo.UmbralMedio,
            UmbralAlto = _riesgo.UmbralAlto,
            AlertasPendientes = await _db.Alertas.CountAsync(a => a.Decision == DecisionAlerta.Pendiente)
        };
        if (!string.IsNullOrWhiteSpace(probar)) vm.Coincidencias = await _riesgo.BuscarAsync(probar, 5);
        return View(vm);
    }

    // ---------- Registrar / validar / dar de baja (solo Buscador) ----------
    [Authorize(Roles = Roles.Buscador)]
    [HttpGet]
    public IActionResult Crear() => View(new DireccionCrearVM());

    [Authorize(Roles = Roles.Buscador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(DireccionCrearVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var norm = NormalizadorDirecciones.Normalizar(vm.Direccion);
        if (norm.Length < 5)
        {
            ModelState.AddModelError(nameof(vm.Direccion), "La dirección es demasiado corta.");
            return View(vm);
        }
        if (await _db.DireccionesFraudulentas.AnyAsync(d => d.DireccionNormalizada == norm && d.Estado != EstadoDireccion.Descartada))
        {
            ModelState.AddModelError(nameof(vm.Direccion), "Esa dirección ya está registrada.");
            return View(vm);
        }
        var d = await _riesgo.AgregarAsync(vm.Direccion, vm.Motivo, UserId, true, "Registro manual del Buscador");
        await _audit.RegistrarAsync("RegistrarDireccionFraudulenta", "DireccionFraudulenta", d.Id.ToString(CultureInfo.InvariantCulture), d.DireccionOriginal);
        TempData["Ok"] = "Dirección registrada y validada.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Buscador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Validar(int id)
    {
        var d = await _db.DireccionesFraudulentas.FindAsync(id);
        if (d is null || d.Estado != EstadoDireccion.Pendiente)
        {
            TempData["Error"] = "La dirección no está pendiente de validación.";
            return RedirectToAction(nameof(Index));
        }
        await _riesgo.ValidarAsync(d, UserId);
        await _audit.RegistrarAsync("ValidarDireccionFraudulenta", "DireccionFraudulenta", d.Id.ToString(CultureInfo.InvariantCulture), d.DireccionOriginal);
        TempData["Ok"] = "Dirección validada e incorporada al registro.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Buscador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Descartar(int id)
    {
        var d = await _db.DireccionesFraudulentas.FindAsync(id);
        if (d is null || d.Estado == EstadoDireccion.Descartada) return RedirectToAction(nameof(Index));
        d.Estado = EstadoDireccion.Descartada;
        await _db.SaveChangesAsync();
        await _audit.RegistrarAsync("DarDeBajaDireccionFraudulenta", "DireccionFraudulenta", d.Id.ToString(CultureInfo.InvariantCulture), d.DireccionOriginal);
        TempData["Ok"] = "Dirección dada de baja del registro.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Buscador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reindexar()
    {
        await _riesgo.ReindexarAsync();
        await _audit.RegistrarAsync("ReindexarDirecciones");
        TempData["Ok"] = "Vectores del registro recalculados.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Bandeja de alertas del motor ----------
    [Authorize(Roles = Roles.Buscador)]
    public async Task<IActionResult> Alertas()
    {
        var elegibles = ReglasClasificacion.Elegibles;
        var lista = await _db.Alertas
            .Include(a => a.DireccionFraudulenta)
            .Include(a => a.Tarjeta).ThenInclude(t => t!.Cliente)
            .Where(a => a.Decision == DecisionAlerta.Pendiente && elegibles.Contains(a.Tarjeta!.Estado))
            .OrderByDescending(a => a.Puntaje).ToListAsync();
        return View(lista);
    }

    [Authorize(Roles = Roles.Buscador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolverAlerta(int id, bool confirmar)
    {
        var a = await _db.Alertas
            .Include(x => x.Tarjeta).ThenInclude(t => t!.Cliente)
            .Include(x => x.DireccionFraudulenta)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a is null || a.Decision != DecisionAlerta.Pendiente)
        {
            TempData["Error"] = "La alerta ya fue resuelta.";
            return RedirectToAction(nameof(Alertas));
        }

        a.DecididoPorId = UserId;
        a.FechaDecision = DateTime.UtcNow;

        if (!confirmar)
        {
            a.Decision = DecisionAlerta.Descartada;
            await _db.SaveChangesAsync();
            await _audit.RegistrarAsync("DescartarAlertaDireccion", "Tarjeta", a.TarjetaId.ToString(CultureInfo.InvariantCulture), $"alerta {a.Id}");
            TempData["Ok"] = "Alerta descartada. La tarjeta queda libre para clasificarse.";
            return RedirectToAction(nameof(Alertas));
        }

        var t = a.Tarjeta!;
        try
        {
            _estados.Cambiar(t, EstadoTarjeta.NoClasificable, UserId,
                $"Dirección fraudulenta confirmada (alerta {a.Id}, puntaje {a.Puntaje:P0})");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Alertas));
        }

        a.Decision = DecisionAlerta.Confirmada;
        foreach (var c in await _db.Citas.Where(c => c.TarjetaId == t.Id && c.Vigente).ToListAsync())
            c.Vigente = false;   // la cita queda anulada

        // Aprendizaje: la dirección de la tarjeta se incorpora al registro
        var norm = string.IsNullOrWhiteSpace(t.Cliente!.DireccionNormalizada)
            ? NormalizadorDirecciones.Normalizar(t.Cliente.Direccion) : t.Cliente.DireccionNormalizada!;
        var aprendida = false;
        if (!await _db.DireccionesFraudulentas.AnyAsync(d => d.DireccionNormalizada == norm && d.Estado != EstadoDireccion.Descartada))
        {
            await _riesgo.AgregarAsync(t.Cliente.Direccion, $"Confirmada desde alerta {a.Id}", UserId, true, "Aprendizaje");
            aprendida = true;
        }

        await _db.SaveChangesAsync();
        await _audit.RegistrarAsync("ConfirmarAlertaDireccion", "Tarjeta", t.Id.ToString(CultureInfo.InvariantCulture),
            $"alerta {a.Id}; tarjeta {t.Codigo} no clasificable; notificar a Descargo; dirección aprendida: {(aprendida ? "sí" : "no")}");
        TempData["Ok"] = $"Fraude confirmado. La tarjeta {t.Codigo} queda como no clasificable.";
        return RedirectToAction(nameof(Alertas));
    }
}