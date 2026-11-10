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

[Authorize(Roles = Roles.Clasificador)]
public class ClasificacionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAddressRiskService _riesgo;
    private readonly IAuditoriaService _audit;

    public ClasificacionController(ApplicationDbContext db, IAddressRiskService riesgo, IAuditoriaService audit)
    { _db = db; _riesgo = riesgo; _audit = audit; }

    public async Task<IActionResult> Index()
    {
        var elegibles = ReglasClasificacion.Elegibles;
        var lotes = await _db.Lotes.Include(l => l.Remito)
            .Where(l => l.Estado == EstadoLote.IngresadoEnCustodia && _db.Tarjetas.Any(t => t.LoteId == l.Id))
            .OrderByDescending(l => l.Id).ToListAsync();

        var filas = new List<LoteClasifFilaVM>();
        foreach (var l in lotes)
        {
            var tarjetas = _db.Tarjetas.Where(t => t.LoteId == l.Id);
            filas.Add(new LoteClasifFilaVM
            {
                Lote = l,
                Total = await tarjetas.CountAsync(),
                SinZona = await tarjetas.CountAsync(t => t.ZonaId == null && elegibles.Contains(t.Estado)),
                NoClasificables = await tarjetas.CountAsync(t => t.Estado == EstadoTarjeta.NoClasificable),
                ConAlerta = await _db.Alertas
                    .Where(a => a.Decision == DecisionAlerta.Pendiente && a.Tarjeta!.LoteId == l.Id && elegibles.Contains(a.Tarjeta.Estado))
                    .Select(a => a.TarjetaId).Distinct().CountAsync()
            });
        }
        return View(filas);
    }

    // Paso 1 de CU15: el sistema consulta el registro (CU16) y carga las alertas del lote
    [HttpGet]
    public async Task<IActionResult> Clasificar(int id)
    {
        var lote = await _db.Lotes.Include(l => l.Remito).FirstOrDefaultAsync(l => l.Id == id);
        if (lote is null) return NotFound();
        await _riesgo.EvaluarLoteAsync(id);
        return View(await ConstruirAsync(lote, null, false));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int loteId, Dictionary<int, int?> zonas)
    {
        var lote = await _db.Lotes.Include(l => l.Remito).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote is null) return NotFound();
        zonas ??= new();

        var elegibles = ReglasClasificacion.Elegibles;
        var tarjetas = await _db.Tarjetas.Where(t => t.LoteId == loteId && elegibles.Contains(t.Estado)).ToListAsync();
        var ids = tarjetas.Select(t => t.Id).ToList();
        var bloqueadas = (await _db.Alertas.Where(a => ids.Contains(a.TarjetaId) && a.Decision == DecisionAlerta.Pendiente)
            .Select(a => a.TarjetaId).Distinct().ToListAsync()).ToHashSet();
        var validas = (await _db.Zonas.Where(z => z.Activo).Select(z => z.Id).ToListAsync()).ToHashSet();
        var libres = tarjetas.Where(t => !bloqueadas.Contains(t.Id)).ToList();

        bool TieneZona(Tarjeta t) => zonas.TryGetValue(t.Id, out var z) && z.HasValue && validas.Contains(z.Value);

        if (libres.Count == 0)
        {
            TempData["Error"] = "No hay tarjetas disponibles para clasificar: todas están bloqueadas por alerta o ya fueron procesadas.";
            return RedirectToAction(nameof(Index));
        }
        if (libres.Any(t => !TieneZona(t)))
        {
            var vm = await ConstruirAsync(lote, zonas, true);
            vm.Error = "Existen tarjetas sin zona asignada. Verifique el listado y complete la asignación antes de continuar.";
            return View("Clasificar", vm);
        }

        foreach (var t in libres) t.ZonaId = zonas[t.Id]!.Value;
        await _db.SaveChangesAsync();
        await _audit.RegistrarAsync("ClasificarLote", "Lote", loteId.ToString(),
            $"{libres.Count} clasificadas; {bloqueadas.Count} bloqueadas por alerta");
        TempData["Ok"] = $"Clasificación de tarjetas registrada exitosamente. {libres.Count} clasificadas; {bloqueadas.Count} pendientes de revisión del Buscador.";
        return RedirectToAction(nameof(Index));
    }

    // RF23: el Clasificador propone, el Buscador valida (RN-21)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportarSospechosa(int loteId, int tarjetaId, string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > 300)
        {
            TempData["Error"] = "Ingrese el motivo (máximo 300 caracteres).";
            return RedirectToAction(nameof(Clasificar), new { id = loteId });
        }
        var t = await _db.Tarjetas.Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == tarjetaId && x.LoteId == loteId);
        if (t is null) return NotFound();

        var dir = t.Cliente!.Direccion;
        var norm = NormalizadorDirecciones.Normalizar(dir);
        if (await _db.DireccionesFraudulentas.AnyAsync(d => d.DireccionNormalizada == norm && d.Estado != EstadoDireccion.Descartada))
        {
            TempData["Error"] = "Esa dirección ya está registrada o propuesta.";
            return RedirectToAction(nameof(Clasificar), new { id = loteId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var d2 = await _riesgo.AgregarAsync(dir, motivo, userId, false, $"Propuesta por Clasificador (lote {loteId})");
        await _audit.RegistrarAsync("ProponerDireccionSospechosa", "DireccionFraudulenta", d2.Id.ToString(), dir);
        TempData["Ok"] = "Dirección propuesta. Queda pendiente de validación por el Buscador.";
        return RedirectToAction(nameof(Clasificar), new { id = loteId });
    }

    private async Task<ClasificarLoteVM> ConstruirAsync(Lote lote, Dictionary<int, int?>? seleccion, bool resaltar)
    {
        var elegibles = ReglasClasificacion.Elegibles;
        var tarjetas = await _db.Tarjetas.Include(t => t.Cliente)
            .Where(t => t.LoteId == lote.Id && elegibles.Contains(t.Estado)).OrderBy(t => t.Id).ToListAsync();
        var ids = tarjetas.Select(t => t.Id).ToList();
        var alertas = await _db.Alertas.Include(a => a.DireccionFraudulenta)
            .Where(a => ids.Contains(a.TarjetaId) && a.Decision == DecisionAlerta.Pendiente).ToListAsync();
        var porTarjeta = alertas.GroupBy(a => a.TarjetaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Puntaje).First());
        var zonasDb = await _db.Zonas.Where(z => z.Activo).OrderBy(z => z.Nombre).ToListAsync();

        var filas = tarjetas.Select(t =>
        {
            porTarjeta.TryGetValue(t.Id, out var al);
            int? zona = t.ZonaId;
            if (seleccion is not null && seleccion.TryGetValue(t.Id, out var z)) zona = z;
            return new FilaClasifVM { Tarjeta = t, ZonaId = zona, Alerta = al, Faltante = resaltar && al is null && zona is null };
        }).ToList();

        return new ClasificarLoteVM
        {
            Lote = lote,
            Filas = filas,
            Zonas = zonasDb.Select(z => new SelectListItem(z.Nombre, z.Id.ToString())).ToList()
        };
    }
}