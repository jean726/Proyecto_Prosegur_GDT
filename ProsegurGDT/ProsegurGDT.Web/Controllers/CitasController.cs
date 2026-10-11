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
    private readonly ITarjetaEstadoService _estados;
    private readonly int _max;

    private static readonly ResultadoLlamada[] NoContacto =
    {
        ResultadoLlamada.NoContesta, ResultadoLlamada.BuzonDeVoz, ResultadoLlamada.NumeroIncorrecto,
        ResultadoLlamada.NumeroFueraDeServicio, ResultadoLlamada.Otro
    };

    public CitasController(ApplicationDbContext db, IAuditoriaService audit, ITarjetaEstadoService estados, IConfiguration cfg)
    {
        _db = db; _audit = audit; _estados = estados;
        _max = cfg.GetValue<int?>("Contacto:MaxIntentos") ?? 3;
    }

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

        var error = Validar(vm, t);
        if (error is not null) { await Rellenar(vm); vm.Error = error; return View(vm); }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ahora = DateTime.UtcNow;
        var resultado = vm.Resultado!.Value;
        _db.Llamadas.Add(new RegistroLlamada
        {
            Tarjeta = t,
            Resultado = resultado,
            Observaciones = vm.Observaciones?.Trim(),
            UsuarioId = userId,
            FechaHora = ahora
        });

        string msg;
        switch (resultado)
        {
            case ResultadoLlamada.Exitoso:
                {
                    var fecha = DateOnly.FromDateTime(vm.Fecha!.Value);
                    var hora = TimeOnly.Parse(vm.Hora!);
                    _db.Citas.Add(new Cita
                    {
                        Tarjeta = t,
                        FechaEntrega = fecha,
                        HoraEntrega = hora,
                        Lugar = vm.Lugar!.Trim(),
                        Observaciones = vm.Observaciones?.Trim(),
                        RegistradaPorId = userId,
                        FechaHoraRegistro = ahora
                    });
                    _estados.Cambiar(t, EstadoTarjeta.CitaProgramada, userId, "Cita registrada");
                    msg = $"Cita registrada satisfactoriamente para el cliente {t.Cliente!.NombreCompleto} el {fecha:dd/MM/yyyy} a las {hora:HH\\:mm} en {vm.Lugar!.Trim()}.";
                    break;
                }
            case ResultadoLlamada.ContactadoSinAcuerdo:
                _estados.Cambiar(t, EstadoTarjeta.PendienteDeCita, userId, "Contactado sin acuerdo");
                msg = "Contacto registrado sin acuerdo. La tarjeta queda pendiente de cita.";
                break;
            case ResultadoLlamada.DatosInsuficientes:
                _estados.Cambiar(t, EstadoTarjeta.PendienteDeContacto, userId, "Datos de contacto insuficientes");
                msg = "Registrado. Comunique la situación al Supervisor para corregir la ficha.";
                break;
            default:   // CU03: no se logró contacto
                {
                    var previos = (await _db.Llamadas.Where(l => l.TarjetaId == t.Id).Select(l => l.Resultado).ToListAsync())
                        .Count(r => NoContacto.Contains(r));
                    if (previos + 1 >= _max)
                    {
                        _estados.Cambiar(t, EstadoTarjeta.IntentosAgotados, userId, "Intentos de contacto agotados");
                        msg = "Motivo de no contacto registrado. Intentos agotados: el caso queda pendiente de escalamiento al Supervisor.";
                    }
                    else
                    {
                        _estados.Cambiar(t, EstadoTarjeta.PendienteDeContacto, userId, resultado.Texto());
                        msg = $"Motivo de no contacto registrado (intento {previos + 1} de {_max}).";
                    }
                    break;
                }
        }

        await _db.SaveChangesAsync();
        await _audit.RegistrarAsync("RegistrarLlamada", "Tarjeta", t.Id.ToString(), resultado.Texto());
        TempData["Ok"] = msg;
        return RedirectToAction(nameof(Index));
    }

    private string? Validar(GestionarCitaVM vm, Tarjeta t)
    {
        if (vm.Resultado is null) return "Seleccione el resultado de la llamada.";
        if (!FichaCliente.DatosCompletos(t.Cliente!) && vm.Resultado != ResultadoLlamada.DatosInsuficientes)
            return "Los datos de contacto del cliente no son suficientes para proceder. Registre 'Datos insuficientes' y comunique la situación al Supervisor.";
        if (vm.Resultado is ResultadoLlamada.ContactadoSinAcuerdo or ResultadoLlamada.DatosInsuficientes or ResultadoLlamada.Otro
            && string.IsNullOrWhiteSpace(vm.Observaciones))
            return "Las observaciones son obligatorias para este resultado.";
        if (vm.Resultado == ResultadoLlamada.Exitoso)
        {
            if (vm.Fecha is null) return "Ingrese la fecha de entrega.";
            if (DateOnly.FromDateTime(vm.Fecha.Value) < DateOnly.FromDateTime(HoraLima.Ahora)) return "La fecha de entrega no puede ser anterior a hoy.";
            if (!TimeOnly.TryParse(vm.Hora, out _)) return "Ingrese una hora válida.";
            if (string.IsNullOrWhiteSpace(vm.Lugar)) return "Ingrese el lugar de entrega.";
        }
        return null;
    }

    private async Task Rellenar(GestionarCitaVM vm)
    {
        vm.Tarjeta = await _db.Tarjetas.Include(x => x.Cliente).FirstAsync(x => x.Id == vm.TarjetaId);
        vm.Historial = await _db.Llamadas.Include(l => l.Usuario).Where(l => l.TarjetaId == vm.TarjetaId)
            .OrderByDescending(l => l.FechaHora).Take(20).ToListAsync();
        var todos = await _db.Llamadas.Where(l => l.TarjetaId == vm.TarjetaId).Select(l => l.Resultado).ToListAsync();
        vm.IntentosNoContacto = todos.Count(r => NoContacto.Contains(r));
        vm.MaxIntentos = _max;
        vm.DatosCompletos = FichaCliente.DatosCompletos(vm.Tarjeta.Cliente!);
    }
}