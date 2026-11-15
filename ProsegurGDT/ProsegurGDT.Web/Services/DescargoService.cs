using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public interface IDescargoService
{
    Task<DescargoIndexVM> ListarAsync();
    Task<ProcesarRutaVM?> ProcesarAsync(int hojaRutaId);
    Task<ResultadoDescargoOp> RegistrarCargosAsync(int hojaRutaId, int cantidad, string usuarioId);
    Task<ResultadoDescargoOp> MarcarSinDocumentacionAsync(int hojaRutaId, string texto, string usuarioId);
    Task<ResultadoDescargoOp> RegistrarResultadoAsync(RegistrarResultadoVM entrada, string usuarioId);
    Task<ResultadoDescargoOp> CerrarAsync(int hojaRutaId, string usuarioId);
    Task<LegajoVM?> LegajoAsync(int hojaRutaId);
    Task<ResultadoDescargoOp> MarcarOrganizadaAsync(int hojaRutaId);
    Task<List<VerificacionFilaVM>> ListarVerificacionesAsync();
    Task<ResultadoDescargoOp> ResolverVerificacionAsync(int descargoTarjetaId, bool resuelta, string observacion, string buscadorId);
}

// CU09 — El Descargador procesa las hojas de ruta finalizadas: verifica los cargos de entrega contra el sistema y
// actualiza el estado de cada tarjeta (RN-09, RN-10, RN-11). Si la identidad del receptor no coincide con la del titular,
// la tarjeta se deriva al Buscador (RN-13, RN-14). CU11 — organiza la documentación por mensajero.
public class DescargoService : IDescargoService
{
    private readonly ApplicationDbContext _db;
    private readonly ITarjetaEstadoService _estados;

    public DescargoService(ApplicationDbContext db, ITarjetaEstadoService estados)
    { _db = db; _estados = estados; }

    // ---------- Consulta ----------

    public async Task<DescargoIndexVM> ListarAsync()
    {
        var hojas = await CargarHojasAsync();
        var rutaIds = hojas.Select(h => h.AsignacionRutaId).ToList();
        var hojaIds = hojas.Select(h => h.Id).ToList();

        var estados = await _db.AsignacionesTarjeta.AsNoTracking()
            .Where(a => rutaIds.Contains(a.AsignacionRutaId))
            .Select(a => new { a.AsignacionRutaId, a.Tarjeta!.Estado }).ToListAsync();
        var porRuta = estados.GroupBy(e => e.AsignacionRutaId).ToDictionary(g => g.Key, g => g.Select(x => x.Estado).ToList());
        var descargos = await _db.DescargosRuta.AsNoTracking().Where(d => hojaIds.Contains(d.HojaRutaId)).ToDictionaryAsync(d => d.HojaRutaId);

        var vm = new DescargoIndexVM();
        foreach (var h in hojas.OrderByDescending(h => DescargoReglas.Jornada(h)).ThenBy(h => h.AsignacionRuta!.OrdenVisita))
        {
            var lista = porRuta.GetValueOrDefault(h.AsignacionRutaId) ?? new();
            descargos.TryGetValue(h.Id, out var d);
            vm.Rutas.Add(new DescargoRutaFilaVM
            {
                HojaRutaId = h.Id,
                Fecha = DescargoReglas.Jornada(h),
                CodigoHojaControl = h.AsignacionRuta!.HojaAsignacion!.Lote?.CodigoHojaControl,
                Repartidor = h.AsignacionRuta.Repartidor?.Nombre ?? "—",
                Sector = h.AsignacionRuta.SectorCobertura ?? "—",
                Tarjetas = lista.Count,
                CargosRecibidos = d?.CargosRecibidos ?? 0,
                Procesadas = lista.Count(e => e != EstadoTarjeta.AsignadaARuta),
                Estado = d?.Estado ?? EstadoDescargoRuta.Pendiente,
                Organizada = d?.DocumentacionOrganizada ?? false
            });
        }
        return vm;
    }

    public async Task<ProcesarRutaVM?> ProcesarAsync(int hojaRutaId)
    {
        var h = await CargarHojaAsync(hojaRutaId);
        if (h is null) return null;
        var d = await _db.DescargosRuta.AsNoTracking().FirstOrDefaultAsync(x => x.HojaRutaId == hojaRutaId);
        return new ProcesarRutaVM
        {
            HojaRutaId = h.Id,
            CodigoHojaControl = h.AsignacionRuta!.HojaAsignacion!.Lote?.CodigoHojaControl,
            Repartidor = h.AsignacionRuta.Repartidor?.Nombre ?? "—",
            Sector = h.AsignacionRuta.SectorCobertura ?? "—",
            Fecha = DescargoReglas.Jornada(h),
            Estado = d?.Estado ?? EstadoDescargoRuta.Pendiente,
            CargosRecibidos = d?.CargosRecibidos ?? 0,
            Observacion = d?.Observacion,
            Organizada = d?.DocumentacionOrganizada ?? false,
            Tarjetas = await TarjetasAsync(h.AsignacionRutaId)
        };
    }

    public async Task<LegajoVM?> LegajoAsync(int hojaRutaId)
    {
        var h = await CargarHojaAsync(hojaRutaId);
        if (h is null) return null;
        var d = await _db.DescargosRuta.AsNoTracking().Include(x => x.Descargador).FirstOrDefaultAsync(x => x.HojaRutaId == hojaRutaId);
        if (d is null || d.Estado != EstadoDescargoRuta.Cerrada) return null;
        return new LegajoVM
        {
            HojaRutaId = h.Id,
            CodigoHojaControl = h.AsignacionRuta!.HojaAsignacion!.Lote?.CodigoHojaControl,
            Repartidor = h.AsignacionRuta.Repartidor?.Nombre ?? "—",
            Sector = h.AsignacionRuta.SectorCobertura ?? "—",
            Fecha = DescargoReglas.Jornada(h),
            CargosRecibidos = d.CargosRecibidos,
            FechaCierre = d.FechaCierre,
            Descargador = d.Descargador?.NombreCompleto,
            Organizada = d.DocumentacionOrganizada,
            FechaOrganizacion = d.FechaOrganizacion,
            Tarjetas = await TarjetasAsync(h.AsignacionRutaId)
        };
    }

    public async Task<ResultadoDescargoOp> MarcarOrganizadaAsync(int hojaRutaId)
    {
        var d = await _db.DescargosRuta.FirstOrDefaultAsync(x => x.HojaRutaId == hojaRutaId);
        if (d is null || d.Estado != EstadoDescargoRuta.Cerrada)
            return new(false, "Solo se puede organizar la documentación de una ruta con el descargo cerrado.");
        if (d.DocumentacionOrganizada) return new(false, "La documentación de esta ruta ya está organizada.");
        d.DocumentacionOrganizada = true;
        d.FechaOrganizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new(true, "Documentación de la ruta organizada por mensajero.", $"Hoja de ruta {hojaRutaId}: documentación organizada");
    }

    // ---------- Operaciones del Descargador ----------

    public async Task<ResultadoDescargoOp> RegistrarCargosAsync(int hojaRutaId, int cantidad, string usuarioId)
    {
        var h = await CargarHojaAsync(hojaRutaId);
        if (h is null) return new(false, "La hoja de ruta no está disponible para el descargo.");
        var total = await _db.AsignacionesTarjeta.CountAsync(a => a.AsignacionRutaId == h.AsignacionRutaId);
        if (cantidad < 0 || cantidad > total) return new(false, $"La cantidad de cargos debe estar entre 0 y {total}.");

        var d = await ObtenerOCrearAsync(h, usuarioId);
        if (d.Estado == EstadoDescargoRuta.Cerrada) return new(false, "El descargo de esta ruta ya está cerrado.");
        d.CargosRecibidos = cantidad;
        if (cantidad > 0)
        {
            d.Estado = EstadoDescargoRuta.EnProceso;
            d.Observacion = null;
        }
        await _db.SaveChangesAsync();
        return cantidad > 0
            ? new(true, $"{cantidad} cargos de entrega registrados. Ya puede verificar las tarjetas.", $"Hoja de ruta {hojaRutaId}: {cantidad} cargos recibidos de {total}")
            : new(false, $"La hoja de ruta del repartidor {h.AsignacionRuta!.Repartidor?.Nombre} no cuenta con cargos de entrega registrados. No es posible procesar el descargo sin documentación física.");
    }

    public async Task<ResultadoDescargoOp> MarcarSinDocumentacionAsync(int hojaRutaId, string texto, string usuarioId)
    {
        var h = await CargarHojaAsync(hojaRutaId);
        if (h is null) return new(false, "La hoja de ruta no está disponible para el descargo.");
        var d = await ObtenerOCrearAsync(h, usuarioId);
        if (d.Estado == EstadoDescargoRuta.Cerrada) return new(false, "El descargo de esta ruta ya está cerrado.");
        d.Estado = EstadoDescargoRuta.PendienteDeDocumentacion;
        d.CargosRecibidos = 0;
        d.Observacion = texto.Trim().Length > 300 ? texto.Trim()[..300] : texto.Trim();
        await _db.SaveChangesAsync();
        return new(true, "Incidencia registrada. La hoja queda pendiente de documentación y se escala al Supervisor.",
            $"Hoja de ruta {hojaRutaId} sin cargos: {d.Observacion}");
    }

    public async Task<ResultadoDescargoOp> RegistrarResultadoAsync(RegistrarResultadoVM e, string usuarioId)
    {
        var h = await CargarHojaAsync(e.HojaRutaId);
        if (h is null) return new(false, "La hoja de ruta no está disponible para el descargo.");
        var d = await _db.DescargosRuta.Include(x => x.Tarjetas).FirstOrDefaultAsync(x => x.HojaRutaId == e.HojaRutaId);
        var repartidor = h.AsignacionRuta!.Repartidor?.Nombre;
        if (d is null || d.CargosRecibidos == 0)
            return new(false, $"La hoja de ruta del repartidor {repartidor} no cuenta con cargos de entrega registrados. No es posible procesar el descargo sin documentación física.");
        if (d.Estado == EstadoDescargoRuta.Cerrada) return new(false, "El descargo de esta ruta ya está cerrado.");
        if (e.Resultado is null || !Enum.IsDefined(e.Resultado.Value)) return new(false, "Seleccione el resultado de la visita.");

        var pertenece = await _db.AsignacionesTarjeta.AnyAsync(a => a.AsignacionRutaId == h.AsignacionRutaId && a.TarjetaId == e.TarjetaId);
        if (!pertenece) return new(false, "La tarjeta no pertenece a esta hoja de ruta.");
        var tarjeta = await _db.Tarjetas.Include(t => t.Cliente).FirstAsync(t => t.Id == e.TarjetaId);
        if (tarjeta.Estado != EstadoTarjeta.AsignadaARuta)
            return new(false, $"La tarjeta {tarjeta.Codigo} ya fue procesada ({tarjeta.Estado.TextoEstado()}).");

        var dt = d.Tarjetas.FirstOrDefault(x => x.TarjetaId == e.TarjetaId);
        if (dt?.Verificacion == VerificacionDescargo.DerivadaAlBuscador)
            return new(false, $"La tarjeta {tarjeta.Codigo} está derivada al Buscador. Espere el resultado de su verificación física.");
        if (dt?.Verificacion == VerificacionDescargo.DiscrepanciaNoResuelta)
            return new(false, $"La discrepancia de la tarjeta {tarjeta.Codigo} no se resolvió. Regístrela en el apartado de defraudación.");

        var resultado = e.Resultado.Value;
        var detalle = string.IsNullOrWhiteSpace(e.Detalle) ? null : e.Detalle.Trim();
        var nombre = string.IsNullOrWhiteSpace(e.ReceptorNombre) ? null : e.ReceptorNombre.Trim();
        var doc = string.IsNullOrWhiteSpace(e.ReceptorDocumento) ? null : e.ReceptorDocumento.Trim();

        if (resultado == ResultadoDescargo.Entregada)
        {
            if (nombre is null || doc is null)
                return new(false, "Para una entrega registre el nombre y el documento del receptor consignados en el cargo.");

            var coincide = DescargoReglas.IdentidadCoincide(nombre, doc, tarjeta.Cliente!);
            if (!coincide && dt?.Verificacion != VerificacionDescargo.DiscrepanciaResuelta)
            {
                // Paso 7 de CU09: la identidad no coincide → se detiene el descargo de la tarjeta y se deriva al Buscador
                dt ??= NuevoDescargoTarjeta(d, tarjeta.Id, usuarioId);
                dt.Verificacion = VerificacionDescargo.DerivadaAlBuscador;
                dt.ReceptorNombre = nombre; dt.ReceptorDocumento = doc;
                dt.Resultado = null; dt.Motivo = null; dt.Detalle = detalle;
                dt.DescargadorId = usuarioId; dt.FechaHora = DateTime.UtcNow;
                d.Estado = EstadoDescargoRuta.EnProceso;
                await _db.SaveChangesAsync();
                return new(true, $"El receptor del cargo no coincide con el titular de la tarjeta {tarjeta.Codigo}. Se derivó al Buscador para su verificación física.",
                    $"Tarjeta {tarjeta.Codigo} derivada al Buscador (receptor {nombre} / {doc})", Aviso: true);
            }
        }
        else
        {
            if (e.Motivo is null || !Enum.IsDefined(e.Motivo.Value))
                return new(false, "Registre el motivo de no entrega antes de continuar.");
            if ((resultado == ResultadoDescargo.ConIncidencia || e.Motivo == MotivoNoEntrega.Otro) && (detalle?.Length ?? 0) < 5)
                return new(false, "Describa el evento o el motivo (mínimo 5 caracteres).");
        }

        dt ??= NuevoDescargoTarjeta(d, tarjeta.Id, usuarioId);
        dt.Resultado = resultado;
        dt.Motivo = resultado == ResultadoDescargo.Entregada ? null : e.Motivo;
        dt.ReceptorNombre = nombre; dt.ReceptorDocumento = doc;
        dt.Detalle = detalle;
        if (dt.Verificacion != VerificacionDescargo.DiscrepanciaResuelta) dt.Verificacion = VerificacionDescargo.Conforme;
        dt.DescargadorId = usuarioId; dt.FechaHora = DateTime.UtcNow;

        _estados.Cambiar(tarjeta, DescargoReglas.Destino(resultado), usuarioId,
            $"Descargo ruta de {repartidor}: {resultado.Texto()}" + (dt.Motivo is null ? "" : $" ({dt.Motivo.Value.Texto()})"));
        d.Estado = EstadoDescargoRuta.EnProceso;
        await _db.SaveChangesAsync();
        return new(true, $"Tarjeta {tarjeta.Codigo} registrada como {resultado.Texto().ToLowerInvariant()}.",
            $"Tarjeta {tarjeta.Codigo}: {resultado.Texto()} (hoja de ruta {e.HojaRutaId})");
    }

    public async Task<ResultadoDescargoOp> CerrarAsync(int hojaRutaId, string usuarioId)
    {
        var h = await CargarHojaAsync(hojaRutaId);
        if (h is null) return new(false, "La hoja de ruta no está disponible para el descargo.");
        var d = await _db.DescargosRuta.FirstOrDefaultAsync(x => x.HojaRutaId == hojaRutaId);
        if (d is null || d.CargosRecibidos == 0)
            return new(false, "No se puede cerrar una hoja de ruta sin cargos de entrega registrados.");
        if (d.Estado == EstadoDescargoRuta.Cerrada) return new(false, "El descargo de esta ruta ya está cerrado.");

        var tarjetas = await _db.Tarjetas.AsNoTracking()
            .Where(t => _db.AsignacionesTarjeta.Any(a => a.AsignacionRutaId == h.AsignacionRutaId && a.TarjetaId == t.Id)).ToListAsync();
        var pendientes = tarjetas.Count(t => t.Estado == EstadoTarjeta.AsignadaARuta);
        if (pendientes > 0) return new(false, $"Aún hay {pendientes} tarjeta(s) sin verificar o derivadas al Buscador. Complete la verificación antes de cerrar.");

        d.Estado = EstadoDescargoRuta.Cerrada;
        d.DescargadorId = usuarioId;
        d.FechaCierre = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var ent = tarjetas.Count(t => t.Estado == EstadoTarjeta.Entregada);
        var dev = tarjetas.Count(t => t.Estado == EstadoTarjeta.Devuelta || t.Estado == EstadoTarjeta.EnCustodia);
        var inc = tarjetas.Count(t => t.Estado != EstadoTarjeta.Entregada && t.Estado != EstadoTarjeta.Devuelta && t.Estado != EstadoTarjeta.EnCustodia);
        var msg = $"Descargo de la ruta cerrado. {ent} tarjetas entregadas, {dev} devueltas o en custodia, {inc} con incidencia.";

        // Paso 12–13: si todas las rutas de la jornada están cerradas, se completa el descargo de la jornada
        var fecha = DescargoReglas.Jornada(h);
        var hojas = (await CargarHojasAsync()).Where(x => DescargoReglas.Jornada(x) == fecha).ToList();
        var ids = hojas.Select(x => x.Id).ToList();
        var cerradas = await _db.DescargosRuta.CountAsync(x => ids.Contains(x.HojaRutaId) && x.Estado == EstadoDescargoRuta.Cerrada);
        if (cerradas == hojas.Count) msg += $" Descargo de la jornada {fecha:dd/MM/yyyy} completado: ya puede generar los reportes.";

        return new(true, msg, $"Hoja de ruta {hojaRutaId} cerrada: {ent} entregadas, {dev} devueltas/custodia, {inc} incidencias");
    }

    // ---------- Buscador ----------

    public async Task<List<VerificacionFilaVM>> ListarVerificacionesAsync()
    {
        var lista = await _db.DescargosTarjeta.AsNoTracking()
            .Where(x => x.Verificacion != VerificacionDescargo.Conforme)
            .Include(x => x.Tarjeta).ThenInclude(t => t!.Cliente)
            .Include(x => x.DescargoRuta).ThenInclude(r => r!.HojaRuta).ThenInclude(h => h!.AsignacionRuta).ThenInclude(a => a!.Repartidor)
            .AsSplitQuery().OrderBy(x => x.Verificacion).ThenByDescending(x => x.Id).ToListAsync();
        return lista.Select(x => new VerificacionFilaVM
        {
            DescargoTarjetaId = x.Id,
            Codigo = x.Tarjeta?.Codigo ?? "—",
            Cliente = x.Tarjeta?.Cliente?.NombreCompleto ?? "—",
            Documento = x.Tarjeta?.Cliente?.DocumentoIdentidad ?? "—",
            Direccion = x.Tarjeta?.Cliente?.Direccion ?? "—",
            ReceptorNombre = x.ReceptorNombre,
            ReceptorDocumento = x.ReceptorDocumento,
            Repartidor = x.DescargoRuta?.HojaRuta?.AsignacionRuta?.Repartidor?.Nombre ?? "—",
            Estado = x.Verificacion,
            Observacion = x.ObservacionBuscador,
            FechaHora = x.FechaHora
        }).ToList();
    }

    public async Task<ResultadoDescargoOp> ResolverVerificacionAsync(int id, bool resuelta, string observacion, string buscadorId)
    {
        var x = await _db.DescargosTarjeta.Include(t => t.Tarjeta).FirstOrDefaultAsync(t => t.Id == id);
        if (x is null) return new(false, "La verificación no existe.");
        if (x.Verificacion != VerificacionDescargo.DerivadaAlBuscador) return new(false, "Esta verificación ya fue resuelta.");
        x.Verificacion = resuelta ? VerificacionDescargo.DiscrepanciaResuelta : VerificacionDescargo.DiscrepanciaNoResuelta;
        x.ObservacionBuscador = observacion.Trim();
        x.BuscadorId = buscadorId;
        x.FechaVerificacionBuscador = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new(true, resuelta
                ? "Discrepancia resuelta. El Descargador puede registrar la entrega con la observación."
                : "Discrepancia no resuelta. El Descargador la registrará en el apartado de defraudación.",
            $"Tarjeta {x.Tarjeta?.Codigo}: discrepancia {(resuelta ? "resuelta" : "no resuelta")} - {x.ObservacionBuscador}");
    }

    // ---------- Apoyo ----------

    private DescargoTarjeta NuevoDescargoTarjeta(DescargoRuta d, int tarjetaId, string usuarioId)
    {
        var dt = new DescargoTarjeta { DescargoRuta = d, TarjetaId = tarjetaId, Verificacion = VerificacionDescargo.Conforme, DescargadorId = usuarioId, FechaHora = DateTime.UtcNow };
        _db.DescargosTarjeta.Add(dt);
        return dt;
    }

    private async Task<DescargoRuta> ObtenerOCrearAsync(HojaRuta h, string usuarioId)
    {
        var d = await _db.DescargosRuta.FirstOrDefaultAsync(x => x.HojaRutaId == h.Id);
        if (d is not null) return d;
        d = new DescargoRuta { HojaRutaId = h.Id, Estado = EstadoDescargoRuta.Pendiente, DescargadorId = usuarioId, FechaInicio = DateTime.UtcNow };
        _db.DescargosRuta.Add(d);
        return d;
    }

    private async Task<List<HojaRuta>> CargarHojasAsync() =>
        await DescargoReglas.Elegibles(_db).AsNoTracking()
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion).ThenInclude(a => a!.Lote)
            .AsSplitQuery().ToListAsync();

    private Task<HojaRuta?> CargarHojaAsync(int id) =>
        DescargoReglas.Elegibles(_db).AsNoTracking()
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.Repartidor)
            .Include(h => h.AsignacionRuta).ThenInclude(r => r!.HojaAsignacion).ThenInclude(a => a!.Lote)
            .AsSplitQuery().FirstOrDefaultAsync(h => h.Id == id);

    private async Task<List<TarjetaDescargoVM>> TarjetasAsync(int asignacionRutaId)
    {
        var tarjetas = await _db.Tarjetas.AsNoTracking()
            .Where(t => _db.AsignacionesTarjeta.Any(a => a.AsignacionRutaId == asignacionRutaId && a.TarjetaId == t.Id))
            .Include(t => t.Cliente).OrderBy(t => t.Codigo).ToListAsync();
        var ids = tarjetas.Select(t => t.Id).ToList();
        var dts = await _db.DescargosTarjeta.AsNoTracking().Where(x => ids.Contains(x.TarjetaId)).ToDictionaryAsync(x => x.TarjetaId);
        var actas = (await _db.ActasFraude.AsNoTracking().Where(a => ids.Contains(a.TarjetaId)).Select(a => a.TarjetaId).ToListAsync()).ToHashSet();
        var campo = (await _db.ResultadosCampo.AsNoTracking().Where(r => ids.Contains(r.TarjetaId)).ToListAsync())
            .GroupBy(r => r.TarjetaId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
        var suplantadas = (await _db.ReportesSuplantacion.AsNoTracking().Where(r => ids.Contains(r.TarjetaId) && r.Estado != EstadoReporteSuplantacion.Descartado)
            .Select(r => r.TarjetaId).ToListAsync()).ToHashSet();

        return tarjetas.Select(t =>
        {
            dts.TryGetValue(t.Id, out var dt);
            return new TarjetaDescargoVM
            {
                TarjetaId = t.Id,
                Codigo = t.Codigo,
                Cliente = t.Cliente?.NombreCompleto ?? "—",
                Documento = t.Cliente?.DocumentoIdentidad ?? "—",
                Direccion = t.Cliente?.Direccion ?? "—",
                Estado = t.Estado,
                Resultado = dt?.Resultado,
                Verificacion = dt?.Verificacion,
                ReceptorNombre = dt?.ReceptorNombre,
                ReceptorDocumento = dt?.ReceptorDocumento,
                Motivo = dt?.Motivo,
                Detalle = dt?.Detalle,
                ObservacionBuscador = dt?.ObservacionBuscador,
                ConActa = actas.Contains(t.Id),
                Campo = campo.GetValueOrDefault(t.Id),
                SuplantacionReportada = suplantadas.Contains(t.Id)
            };
        }).ToList();
    }
}
