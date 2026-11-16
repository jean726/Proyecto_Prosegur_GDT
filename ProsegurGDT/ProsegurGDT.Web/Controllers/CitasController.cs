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

[Authorize(Roles = Roles.AuxiliarTelefonista)]
public class CitasController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _audit;
    private readonly IEnumerable<IManejadorResultadoLlamada> _manejadores;
    private readonly IConfiguracionOperativa _cfg;

    public CitasController(ApplicationDbContext db, IAuditoriaService audit,
        IEnumerable<IManejadorResultadoLlamada> manejadores, IConfiguracionOperativa cfg)
    { _db = db; _audit = audit; _manejadores = manejadores; _cfg = cfg; }

    private static bool Gestionable(EstadoTarjeta e) =>
        e == EstadoTarjeta.IngresadaEnCustodia || e == EstadoTarjeta.PendienteDeContacto || e == EstadoTarjeta.PendienteDeCita;

    public async Task<IActionResult> Index()
    {
        var lista = await _db.Tarjetas.Include(t => t.Cliente)
            .Where(t => t.Estado == EstadoTarjeta.IngresadaEnCustodia || t.Estado == EstadoTarjeta.PendienteDeContacto || t.Estado == EstadoTarjeta.PendienteDeCita)
            .OrderBy(t => t.Id).Take(200).ToListAsync();
        return View(lista);
    }

    [HttpGet]
    public async Task<IActionResult> Gestionar(int id)
    {
        var t = await _db.Tarjetas.FindAsync(id);
        if (t is null || !Gestionable(t.Estado))
        {
            TempData["Error"] = "La tarjeta no está disponible para coordinar una cita.";
            return RedirectToAction(nameof(Index));
        }
        var vm = new GestionarCitaVM { TarjetaId = id };
        await Rellenar(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Gestionar(GestionarCitaVM vm)
    {
        var t = await _db.Tarjetas.Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == vm.TarjetaId);
        if (t is null || !Gestionable(t.Estado))
        {
            TempData["Error"] = "La tarjeta no está disponible para coordinar una cita.";
            return RedirectToAction(nameof(Index));
        }

        if (vm.Resultado is null) return await Rechazar(vm, "Seleccione el resultado de la llamada.");
        var resultado = vm.Resultado.Value;

        if (!FichaCliente.DatosCompletos(t.Cliente!) && resultado != ResultadoLlamada.DatosInsuficientes)
            return await Rechazar(vm, "Los datos de contacto del cliente no son suficientes para proceder. Registre 'Datos insuficientes' y comunique la situación al Supervisor.");

        var manejador = _manejadores.FirstOrDefault(m => m.Soporta(resultado));
        if (manejador is null) return await Rechazar(vm, "Resultado de llamada no soportado.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var previos = (await _db.Llamadas.Where(l => l.TarjetaId == t.Id).Select(l => l.Resultado).ToListAsync())
            .Count(r => r.EsNoContacto());
        var ctx = new ContextoLlamada(t, resultado, vm.Observaciones, vm.Fecha, vm.Hora, vm.Lugar,
            userId, DateTime.UtcNow, previos);

        var error = manejador.Validar(ctx);
        if (error is not null) return await Rechazar(vm, error);

        _db.Llamadas.Add(new RegistroLlamada
        {
            Tarjeta = t,
            Resultado = resultado,
            Observaciones = vm.Observaciones?.Trim(),
            UsuarioId = userId,
            FechaHora = ctx.AhoraUtc
        });
        var mensaje = manejador.Aplicar(ctx);

        await _db.SaveChangesAsync();
        await _audit.RegistrarAsync("RegistrarLlamada", "Tarjeta", t.Id.ToString(CultureInfo.InvariantCulture), resultado.Texto());
        TempData["Ok"] = mensaje;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Rechazar(GestionarCitaVM vm, string error)
    {
        await Rellenar(vm);
        vm.Error = error;
        return View(vm);
    }

    private async Task Rellenar(GestionarCitaVM vm)
    {
        vm.Tarjeta = await _db.Tarjetas.Include(x => x.Cliente).FirstAsync(x => x.Id == vm.TarjetaId);
        vm.Historial = await _db.Llamadas.Include(l => l.Usuario).Where(l => l.TarjetaId == vm.TarjetaId)
            .OrderByDescending(l => l.FechaHora).Take(20).ToListAsync();
        var todos = await _db.Llamadas.Where(l => l.TarjetaId == vm.TarjetaId).Select(l => l.Resultado).ToListAsync();
        vm.IntentosNoContacto = todos.Count(r => r.EsNoContacto());
        vm.MaxIntentos = _cfg.MaxIntentosContacto;
        vm.DatosCompletos = FichaCliente.DatosCompletos(vm.Tarjeta.Cliente!);
    }
}