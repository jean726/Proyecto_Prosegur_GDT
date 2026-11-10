using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using System.Diagnostics;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) => _db = db;

        private static readonly Dictionary<string, string> EtiquetasRol = new()
        {
            [Roles.Administrador] = "Administrador",
            [Roles.Supervisor] = "Supervisor",
            [Roles.AuxiliarTelefonista] = "Telefonista",
            [Roles.Verificador] = "Verificador",
            [Roles.Descargador] = "Descargador",
            [Roles.EncargadoDeBoveda] = "Bóveda",
            [Roles.Clasificador] = "Clasificador",
            [Roles.Buscador] = "Buscador",
            [Roles.AnalistaDeRutas] = "Analista de rutas"
        };

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole(Roles.Administrador))
                return View("IndexAdmin", await ConstruirDashboardAdminAsync());

            if (User.IsInRole(Roles.Supervisor))
                return View("IndexSupervisor", await ConstruirDashboardSupervisorAsync());

            if (User.IsInRole(Roles.Clasificador))
                return View("IndexClasificador", await ConstruirDashboardClasificadorAsync());

            if (User.IsInRole(Roles.Buscador))
                return View("IndexBuscador", await ConstruirDashboardBuscadorAsync());

            if (User.IsInRole(Roles.AuxiliarTelefonista))
                return View("IndexTelefonista", await ConstruirDashboardTelefonistaAsync());

            if (User.IsInRole(Roles.AnalistaDeRutas))
                return View("IndexAnalista", await ConstruirDashboardAnalistaAsync());

            if (User.IsInRole(Roles.Verificador))
                return View("IndexVerificador", await ConstruirDashboardVerificadorAsync());

            return View();
        }

        private async Task<DashboardVerificadorViewModel> ConstruirDashboardVerificadorAsync()
        {
            var revisables = new[] { EstadoHojaRuta.Aprobada, EstadoHojaRuta.Firmada };
            var hoy = DateTime.UtcNow.AddHours(-36);
            var recientes = await _db.ReportesFraude
                .OrderByDescending(r => r.Id).Take(5)
                .Select(r => new { r.Id, r.Tarjeta!.Codigo, r.Tipo, r.FechaHora })
                .ToListAsync();
            var fechas = await _db.ReportesFraude.Where(r => r.FechaHora >= hoy).Select(r => r.FechaHora).ToListAsync();
            var hojasRevisables = _db.HojasRuta.Where(h => revisables.Contains(h.Estado));

            return new DashboardVerificadorViewModel
            {
                HojasPorRevisar = await hojasRevisables.CountAsync(),
                TarjetasEnRevision = await hojasRevisables.SumAsync(h => (int?)h.AsignacionRuta!.CantidadTarjetas) ?? 0,
                ReportesTotal = await _db.ReportesFraude.CountAsync(),
                ReportesHoy = fechas.Count(f => HoraLima.Convertir(f).Date == HoraLima.Ahora.Date),
                Recientes = recientes.Select(r => new FilaReporteFraudeReciente(r.Id, r.Codigo, r.Tipo.Texto(), r.FechaHora)).ToList()
            };
        }

        private async Task<DashboardSupervisorViewModel> ConstruirDashboardSupervisorAsync()
        {
            var modelo = new DashboardSupervisorViewModel();

            var rutasConHoja = _db.HojasRuta.Select(h => h.AsignacionRutaId);
            modelo.RutasSinHoja = await _db.AsignacionesRuta
                .CountAsync(r => r.HojaAsignacion!.Estado == EstadoHojaAsignacion.Emitida && !rutasConHoja.Contains(r.Id));
            modelo.HojasPorAprobar = await _db.HojasRuta.CountAsync(h => h.Estado == EstadoHojaRuta.PendienteDeAprobacion);
            modelo.HojasConDiscrepancia = await _db.HojasRuta.CountAsync(h => h.Estado == EstadoHojaRuta.PendienteDeCorreccion);
            modelo.HojasPorFirmar = await _db.HojasRuta.CountAsync(h => h.Estado == EstadoHojaRuta.Aprobada);
            var hoyLima = DateOnly.FromDateTime(HoraLima.Ahora);
            var repHoy = await _db.ReportesEntregas.Where(r => r.FechaJornada == hoyLima).Select(r => (EstadoReporteEntregas?)r.Estado).FirstOrDefaultAsync();
            modelo.ReporteHoy = repHoy?.Texto();

            var conteos = await _db.Tarjetas
                .GroupBy(t => t.Estado)
                .Select(g => new { Estado = g.Key, Total = g.Count() })
                .ToListAsync();

            foreach (var c in conteos)
                modelo.PorEtapa[EtapasTarjeta.Indice(c.Estado)] += c.Total;

            modelo.IntentosAgotados = conteos
                .Where(c => c.Estado == EstadoTarjeta.IntentosAgotados)
                .Sum(c => c.Total);

            var lotes = await _db.Lotes
                .OrderByDescending(l => l.Id)
                .Take(5)
                .Select(l => new
                {
                    l.Remito!.Numero,
                    Banco = l.Remito.Banco!.Nombre,
                    l.FechaRecepcion,
                    l.Estado,
                    Tarjetas = l.Tarjetas.Count()
                })
                .ToListAsync();

            modelo.LotesRecientes = lotes
                .Select(l => new FilaLote(l.Numero, l.Banco, l.FechaRecepcion, l.Tarjetas, l.Estado))
                .ToList();

            return modelo;
        }
        private async Task<DashboardAdminViewModel> ConstruirDashboardAdminAsync()
        {
            var ahora = DateTimeOffset.UtcNow;
            var modelo = new DashboardAdminViewModel
            {
                Activas = await _db.Users.CountAsync(u => u.Activo),
                Desactivadas = await _db.Users.CountAsync(u => !u.Activo),
                Bloqueadas = await _db.Users.CountAsync(u => u.Activo && u.LockoutEnd != null && u.LockoutEnd > ahora)
            };

            var porRol = await (from ur in _db.UserRoles
                                join r in _db.Roles on ur.RoleId equals r.Id
                                join u in _db.Users on ur.UserId equals u.Id
                                where u.Activo
                                group r by r.Name into g
                                select new { Rol = g.Key!, Total = g.Count() })
                               .ToListAsync();

            var ordenados = porRol.OrderByDescending(x => x.Total).ToList();
            foreach (var x in ordenados.Take(4))
                modelo.PorRol.Add(new FilaRol(EtiquetasRol.GetValueOrDefault(x.Rol, x.Rol), x.Total));
            var otros = ordenados.Skip(4).Sum(x => x.Total);
            if (otros > 0)
                modelo.PorRol.Add(new FilaRol("Otros roles", otros));

            var acciones = await _db.AuditLogs
                .OrderByDescending(a => a.Id)
                .Take(5)
                .Select(a => new { a.Accion, a.Detalle, a.UsuarioNombre, a.FechaHoraUtc })
                .ToListAsync();
            modelo.UltimasAcciones = acciones
                .Select(a => new FilaAccion(a.Accion, a.Detalle, a.UsuarioNombre,
                    HoraLima.Convertir(DateTime.SpecifyKind(a.FechaHoraUtc, DateTimeKind.Utc))))
                .ToList();

            return modelo;
        }
        private async Task<DashboardClasificadorViewModel> ConstruirDashboardClasificadorAsync()
        {
            var elegibles = ReglasClasificacion.Elegibles;

            return new DashboardClasificadorViewModel
            {
                PorClasificar = await _db.Tarjetas.CountAsync(t => t.ZonaId == null && elegibles.Contains(t.Estado)),
                YaClasificadas = await _db.Tarjetas.CountAsync(t => t.ZonaId != null),
                NoClasificables = await _db.Tarjetas.CountAsync(t => t.Estado == EstadoTarjeta.NoClasificable),
                CodigosNoClasificables = await _db.Tarjetas
                    .Where(t => t.Estado == EstadoTarjeta.NoClasificable)
                    .OrderByDescending(t => t.FechaActualizacion)
                    .Take(5)
                    .Select(t => t.Codigo)
                    .ToListAsync()
            };
        }
        private async Task<DashboardBuscadorViewModel> ConstruirDashboardBuscadorAsync()
        {
            var elegibles = ReglasClasificacion.Elegibles;
            var pendientes = _db.Alertas
                .Where(a => a.Decision == DecisionAlerta.Pendiente && elegibles.Contains(a.Tarjeta!.Estado));

            var criticas = await pendientes
                .OrderByDescending(a => a.Puntaje)
                .Take(3)
                .Select(a => new { a.Tarjeta!.Codigo, Direccion = a.DireccionFraudulenta!.DireccionOriginal, a.Puntaje })
                .ToListAsync();

            return new DashboardBuscadorViewModel
            {
                AlertasPendientes = await pendientes.CountAsync(),
                DireccionesPorValidar = await _db.DireccionesFraudulentas.CountAsync(d => d.Estado == EstadoDireccion.Pendiente),
                DireccionesValidadas = await _db.DireccionesFraudulentas.CountAsync(d => d.Estado == EstadoDireccion.Validada),
                AlertasCriticas = criticas.Select(c => new FilaAlertaCritica(c.Codigo, c.Direccion, c.Puntaje)).ToList()
            };
        }

        private async Task<DashboardAnalistaViewModel> ConstruirDashboardAnalistaAsync()
        {
            var conHoja = _db.HojasAsignacion.Select(h => h.LoteId);
            var pendientes = await _db.ReportesDensidad
                .Where(r => !conHoja.Contains(r.LoteId))
                .OrderBy(r => r.LoteId)
                .Select(r => new { r.LoteId, Hoja = r.Lote!.CodigoHojaControl, Remito = r.Lote.Remito!.Numero, Zonas = r.Zonas.Count, r.Estado })
                .ToListAsync();

            // Jornada de hoy en hora de Lima (las fechas se guardan en UTC)
            var rutasHoy = await _db.AsignacionesRuta
                .Where(r => r.HojaAsignacion!.Estado == EstadoHojaAsignacion.Emitida
                         && r.HojaAsignacion.FechaHoraEmision >= DateTime.UtcNow.AddHours(-36))
                .Select(r => new { r.RepartidorId, r.CantidadTarjetas, r.HojaAsignacionId, r.HojaAsignacion!.FechaHoraEmision })
                .ToListAsync();
            var hoy = HoraLima.Ahora.Date;
            rutasHoy = rutasHoy.Where(r => HoraLima.Convertir(r.FechaHoraEmision).Date == hoy).ToList();

            var repartidores = await _db.Repartidores.Where(r => r.Activo).OrderBy(r => r.Nombre).ToListAsync();
            var asignadas = rutasHoy.GroupBy(r => r.RepartidorId).ToDictionary(g => g.Key, g => g.Sum(x => x.CantidadTarjetas));

            return new DashboardAnalistaViewModel
            {
                ReportesPendientes = pendientes.Count,
                HojasHoy = rutasHoy.Select(r => r.HojaAsignacionId).Distinct().Count(),
                TarjetasEnRutaHoy = rutasHoy.Sum(r => r.CantidadTarjetas),
                RepartidoresActivos = repartidores.Count,
                Pendientes = pendientes.Select(p => new FilaReportePendiente(p.LoteId, p.Hoja, p.Remito, p.Zonas, p.Estado.Texto())).ToList(),
                Cargas = repartidores.Select(r => new FilaRepartidorCarga(r.Nombre, r.Tipo.Texto(), r.CapacidadTarjetas, asignadas.GetValueOrDefault(r.Id))).ToList()
            };
        }

        private async Task<DashboardTelefonistaViewModel> ConstruirDashboardTelefonistaAsync()
        {
            var hoy = DateOnly.FromDateTime(HoraLima.Ahora);
            var vigentes = _db.Citas
                .Where(c => c.Vigente && c.Tarjeta!.Estado == EstadoTarjeta.CitaProgramada);

            var proximas = await vigentes
                .Where(c => c.FechaEntrega >= hoy)
                .OrderBy(c => c.FechaEntrega).ThenBy(c => c.HoraEntrega)
                .Take(5)
                .Select(c => new { c.Tarjeta!.Codigo, Cliente = c.Tarjeta.Cliente!.NombreCompleto, c.FechaEntrega, c.HoraEntrega })
                .ToListAsync();

            return new DashboardTelefonistaViewModel
            {
                PendientesContacto = await _db.Tarjetas.CountAsync(t =>
                    t.Estado == EstadoTarjeta.IngresadaEnCustodia ||
                    t.Estado == EstadoTarjeta.PendienteDeContacto ||
                    t.Estado == EstadoTarjeta.PendienteDeCita),
                IntentosAgotados = await _db.Tarjetas.CountAsync(t => t.Estado == EstadoTarjeta.IntentosAgotados),
                CitasProgramadas = await vigentes.CountAsync(),
                ProximasCitas = proximas.Select(c => new FilaCita(c.Codigo, c.Cliente, c.FechaEntrega, c.HoraEntrega)).ToList()
            };
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [AllowAnonymous]
        public IActionResult AccesoDenegado() => View();
    }
}