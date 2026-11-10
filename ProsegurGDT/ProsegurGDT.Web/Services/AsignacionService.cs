using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;

namespace ProsegurGDT.Web.Services;

public record ResultadoAsignacion(bool Ok, List<string> Errores, List<string> Advertencias, int HojaId = 0, string? Detalle = null);

public interface IAsignacionService
{
    Task<List<LoteAsignacionFilaVM>> ListarAsync();
    Task<GenerarHojaVM?> CargarAsync(int loteId, GenerarHojaVM? entrada = null);
    Task<ResultadoAsignacion> GenerarAsync(GenerarHojaVM entrada, string usuarioId);
    Task<DetalleHojaVM?> DetalleAsync(int hojaId);
    Task<(string Nombre, byte[] Contenido)?> CsvAsync(int hojaId);
}

// CU19 — El Analista de Rutas asigna un repartidor a cada zona del reporte de densidad (CU18),
// reagrupa o posterga las zonas sin cobertura y genera la hoja de asignación.
// Solo las tarjetas con cita programada entran a una ruta: el estado CitaProgramada es el único
// desde el que ITarjetaEstadoService permite pasar a AsignadaARuta.
public class AsignacionService : IAsignacionService
{
    private readonly ApplicationDbContext _db;
    private readonly ITarjetaEstadoService _estados;

    public AsignacionService(ApplicationDbContext db, ITarjetaEstadoService estados)
    { _db = db; _estados = estados; }

    private sealed class Contexto
    {
        public Lote Lote { get; set; } = null!;
        public ReporteDensidad? Reporte { get; set; }
        public bool TieneHoja { get; set; }
        public Dictionary<int, List<Tarjeta>> Asignables { get; } = new();   // zona → tarjetas con cita y sin alerta
        public Dictionary<int, int> Vivas { get; } = new();                  // zona → tarjetas aún clasificables
        public Dictionary<int, int> ConAlertaActiva { get; } = new();        // zona → tarjetas con alerta pendiente
        public Dictionary<int, int> OrdenPlanilla { get; } = new();
        public List<Repartidor> Repartidores { get; set; } = new();
        public Dictionary<int, int> AsignadasHoy { get; } = new();
    }

    public async Task<List<LoteAsignacionFilaVM>> ListarAsync()
    {
        var reportes = await _db.ReportesDensidad
            .Include(r => r.Lote).ThenInclude(l => l!.Remito).ThenInclude(x => x!.Banco)
            .Include(r => r.Zonas)
            .OrderByDescending(r => r.LoteId).ToListAsync();
        var ids = reportes.Select(r => r.LoteId).ToList();
        var hojas = await _db.HojasAsignacion.Where(h => ids.Contains(h.LoteId))
            .ToDictionaryAsync(h => h.LoteId, h => new { h.Id, h.FechaHoraEmision });

        return reportes.Select(r =>
        {
            hojas.TryGetValue(r.LoteId, out var h);
            return new LoteAsignacionFilaVM
            {
                LoteId = r.LoteId,
                CodigoHojaControl = r.Lote?.CodigoHojaControl,
                Remito = r.Lote?.Remito?.Numero,
                Banco = r.Lote?.Remito?.Banco?.Nombre,
                ReporteId = r.Id,
                EstadoReporte = r.Estado,
                ZonasReporte = r.Zonas.Count,
                HojaId = h?.Id,
                FechaHoja = h?.FechaHoraEmision
            };
        }).ToList();
    }

    // Con «entrada» conserva lo que el usuario eligió (para volver a mostrar el formulario tras un error)
    public async Task<GenerarHojaVM?> CargarAsync(int loteId, GenerarHojaVM? entrada = null)
    {
        var ctx = await CargarContextoAsync(loteId);
        if (ctx is null) return null;
        var vm = ConstruirVm(ctx, entrada);
        if (entrada is not null) vm.ConfirmarAdvertencias = entrada.ConfirmarAdvertencias;
        return vm;
    }

    public async Task<ResultadoAsignacion> GenerarAsync(GenerarHojaVM entrada, string usuarioId)
    {
        var ctx = await CargarContextoAsync(entrada.LoteId);
        if (ctx is null) return Fallo("El lote no existe.");
        var bloqueo = Bloqueo(ctx);
        if (bloqueo is not null) return Fallo(bloqueo);

        var reporte = ctx.Reporte!;
        var zonasReporte = reporte.Zonas.ToDictionary(z => z.ZonaId);
        var enviadas = entrada.Zonas ?? new();
        if (enviadas.Select(z => z.ZonaId).Distinct().Count() != enviadas.Count
            || !enviadas.Select(z => z.ZonaId).OrderBy(x => x).SequenceEqual(zonasReporte.Keys.OrderBy(x => x)))
            return Fallo("Las zonas enviadas no coinciden con las del reporte. Vuelva a abrir el formulario e intente de nuevo.");

        var errores = new List<string>();
        var advertencias = new List<string>();
        var repartidores = ctx.Repartidores.ToDictionary(r => r.Id);
        string Nombre(int zonaId) => zonasReporte[zonaId].Zona?.Nombre ?? $"Zona {zonaId}";
        int Cant(int zonaId) => ctx.Asignables.TryGetValue(zonaId, out var l) ? l.Count : 0;

        // Solo participan las zonas con tarjetas que tengan cita programada
        var activas = enviadas.Where(z => Cant(z.ZonaId) > 0).ToList();
        foreach (var z in activas)
            if (!Enum.IsDefined(z.Accion)) errores.Add($"Acción no válida para la zona {Nombre(z.ZonaId)}.");
        if (errores.Count > 0) return new(false, errores, advertencias);

        var asignar = activas.Where(z => z.Accion == AccionZonaAsignacion.Asignar).ToList();
        var sinCobertura = asignar.Where(z => z.RepartidorId is null || !repartidores.ContainsKey(z.RepartidorId.Value)).ToList();
        if (sinCobertura.Count > 0)
            errores.Add("Existen zonas sin cobertura de repartidor asignado: " + string.Join(", ", sinCobertura.Select(z => Nombre(z.ZonaId))) +
                        ". Asigne un repartidor, reagrupe la zona con una ruta adyacente o posterguela.");

        var destinosValidos = asignar.Except(sinCobertura).Select(z => z.ZonaId).ToHashSet();
        foreach (var z in activas.Where(z => z.Accion == AccionZonaAsignacion.Reagrupar))
        {
            if (z.DestinoZonaId is null || z.DestinoZonaId == z.ZonaId || !destinosValidos.Contains(z.DestinoZonaId.Value))
                errores.Add($"La zona {Nombre(z.ZonaId)} debe reagruparse con otra zona que tenga repartidor asignado.");
        }
        if (asignar.Count - sinCobertura.Count == 0 && errores.Count == 0)
            errores.Add("Debe haber al menos una ruta con repartidor asignado.");
        if (errores.Count > 0) return new(false, errores, advertencias);

        // Rutas: una por zona asignada; las zonas reagrupadas se suman a la ruta de destino
        var rutas = asignar.Select(z => new
        {
            Zona = z,
            Repartidor = repartidores[z.RepartidorId!.Value],
            Reagrupadas = activas.Where(r => r.Accion == AccionZonaAsignacion.Reagrupar && r.DestinoZonaId == z.ZonaId).ToList()
        }).ToList();
        var postergadas = activas.Where(z => z.Accion == AccionZonaAsignacion.Postergar).ToList();

        var tarjetasRuta = rutas.ToDictionary(
            r => r.Zona.ZonaId,
            r => ctx.Asignables[r.Zona.ZonaId].Concat(r.Reagrupadas.SelectMany(g => ctx.Asignables[g.ZonaId])).ToList());

        // Tarjeta duplicada: ya asignada a una ruta de otra hoja
        var idsAsignar = tarjetasRuta.Values.SelectMany(x => x).Select(t => t.Id).ToList();
        var yaAsignadas = await _db.AsignacionesTarjeta.CountAsync(a => idsAsignar.Contains(a.TarjetaId));
        if (yaAsignadas > 0)
            return new(false, new() { $"{yaAsignadas} tarjeta(s) ya están asignadas a otra ruta. No se puede generar la hoja con tarjetas duplicadas." }, advertencias);

        // Advertencias: sobrecarga y tipo de repartidor incompatible (el Analista decide)
        foreach (var grupo in rutas.GroupBy(r => r.Repartidor.Id))
        {
            var rep = grupo.First().Repartidor;
            var carga = ctx.AsignadasHoy.GetValueOrDefault(rep.Id) + grupo.Sum(r => tarjetasRuta[r.Zona.ZonaId].Count);
            if (carga > rep.CapacidadTarjetas)
                advertencias.Add($"Sobrecarga: {rep.Nombre} tendría {carga} tarjetas y su capacidad es {rep.CapacidadTarjetas}.");
        }
        foreach (var r in rutas)
        {
            var zonasRuta = new[] { r.Zona.ZonaId }.Concat(r.Reagrupadas.Select(g => g.ZonaId));
            foreach (var zid in zonasRuta)
            {
                var req = zonasReporte[zid].TipoRepartidorRequerido;
                if (!Compatible(req, r.Repartidor.Tipo))
                    advertencias.Add($"Tipo incompatible: la zona {Nombre(zid)} requiere {req.Texto().ToLowerInvariant()} y {r.Repartidor.Nombre} es {r.Repartidor.Tipo.Texto().ToLowerInvariant()}.");
            }
        }
        if (advertencias.Count > 0 && !entrada.ConfirmarAdvertencias)
            return new(false, new(), advertencias);

        // ----- Generación (una sola transacción) -----
        var ahora = DateTime.UtcNow;
        var hoja = new HojaAsignacion
        {
            LoteId = entrada.LoteId,
            ReporteDensidadId = reporte.Id,
            GeneradaPorId = usuarioId,
            FechaHoraEmision = ahora,
            Estado = EstadoHojaAsignacion.Emitida,
            TotalTarjetas = idsAsignar.Count,
            TotalRutas = rutas.Count
        };
        _db.HojasAsignacion.Add(hoja);

        foreach (var r in rutas.OrderBy(r => ctx.OrdenPlanilla.GetValueOrDefault(r.Zona.ZonaId, int.MaxValue)))
        {
            var cartas = tarjetasRuta[r.Zona.ZonaId];
            var ruta = new AsignacionRuta
            {
                HojaAsignacion = hoja,
                RepartidorId = r.Repartidor.Id,
                ZonaId = r.Zona.ZonaId,
                SectorCobertura = Recortar(string.Join(" + ", new[] { Nombre(r.Zona.ZonaId) }.Concat(r.Reagrupadas.Select(g => Nombre(g.ZonaId)))), 150),
                CantidadTarjetas = cartas.Count,
                OrdenVisita = ctx.OrdenPlanilla.GetValueOrDefault(r.Zona.ZonaId, 0),
                Estado = r.Reagrupadas.Count > 0 ? EstadoAsignacionRuta.Reagrupada : EstadoAsignacionRuta.Asignada
            };
            _db.AsignacionesRuta.Add(ruta);
            foreach (var t in cartas)
            {
                ruta.Tarjetas.Add(new AsignacionTarjeta { Tarjeta = t, FechaAsignacion = ahora });
                _estados.Cambiar(t, EstadoTarjeta.AsignadaARuta, usuarioId, $"Asignada a ruta de {r.Repartidor.Nombre} ({ruta.SectorCobertura})");
            }
        }

        var nPostergadas = 0;
        foreach (var z in postergadas)
            foreach (var t in ctx.Asignables[z.ZonaId])
            {
                _estados.Cambiar(t, EstadoTarjeta.PendienteDeAsignacion, usuarioId, $"Postergada a la siguiente jornada (zona {Nombre(z.ZonaId)} sin cobertura)");
                nPostergadas++;
            }

        // RN-22: el Analista revisa el reporte de densidad al generar la hoja
        reporte.Estado = EstadoReporteDensidad.Revisado;
        reporte.RevisadoPorId = usuarioId;
        reporte.FechaRevision = ahora;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Fallo("No se pudo generar la hoja porque otro usuario la generó al mismo tiempo. Vuelva a abrir el lote.");
        }

        var nReagrupadas = rutas.Sum(r => r.Reagrupadas.Count);
        var detalle = $"Hoja {hoja.Id} lote {ctx.Lote.CodigoHojaControl}: {rutas.Count} rutas, {idsAsignar.Count} tarjetas asignadas, " +
                      $"{nPostergadas} postergadas, {nReagrupadas} zonas reagrupadas";
        return new(true, new(), advertencias, hoja.Id, detalle);
    }

    public async Task<DetalleHojaVM?> DetalleAsync(int hojaId)
    {
        var h = await _db.HojasAsignacion
            .Include(x => x.Lote).ThenInclude(l => l!.Remito).ThenInclude(r => r!.Banco)
            .Include(x => x.GeneradaPor)
            .Include(x => x.Rutas).ThenInclude(r => r.Repartidor)
            .Include(x => x.Rutas).ThenInclude(r => r.Tarjetas).ThenInclude(t => t.Tarjeta).ThenInclude(t => t!.Cliente)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == hojaId);
        if (h is null) return null;

        var postergadas = await _db.Tarjetas
            .Where(t => t.LoteId == h.LoteId && t.Estado == EstadoTarjeta.PendienteDeAsignacion && t.ZonaId != null)
            .GroupBy(t => t.Zona!.Nombre).Select(g => new { Zona = g.Key, N = g.Count() }).ToListAsync();

        return new DetalleHojaVM
        {
            HojaId = h.Id,
            LoteId = h.LoteId,
            CodigoHojaControl = h.Lote?.CodigoHojaControl,
            Remito = h.Lote?.Remito?.Numero,
            Banco = h.Lote?.Remito?.Banco?.Nombre,
            GeneradaPor = h.GeneradaPor?.NombreCompleto ?? "—",
            FechaHoraEmision = h.FechaHoraEmision,
            Estado = h.Estado,
            TotalTarjetas = h.TotalTarjetas,
            TotalRutas = h.TotalRutas,
            Postergadas = postergadas.OrderBy(p => p.Zona).Select(p => (p.Zona, p.N)).ToList(),
            Rutas = h.Rutas.OrderBy(r => r.OrdenVisita).ThenBy(r => r.Id).Select(r => new RutaHojaVM
            {
                Orden = r.OrdenVisita,
                Repartidor = r.Repartidor?.Nombre ?? "—",
                TipoRepartidor = r.Repartidor?.Tipo ?? TipoRepartidor.Motorizado,
                Sector = r.SectorCobertura ?? "—",
                CantidadTarjetas = r.CantidadTarjetas,
                Estado = r.Estado,
                Tarjetas = r.Tarjetas.OrderBy(t => t.Tarjeta!.Codigo).Select(t => new TarjetaRutaVM
                {
                    Codigo = t.Tarjeta!.Codigo,
                    Cliente = t.Tarjeta.Cliente?.NombreCompleto ?? "—",
                    Direccion = t.Tarjeta.Cliente?.Direccion ?? "—"
                }).ToList()
            }).ToList()
        };
    }

    public async Task<(string Nombre, byte[] Contenido)?> CsvAsync(int hojaId)
    {
        var d = await DetalleAsync(hojaId);
        if (d is null) return null;

        var sb = new StringBuilder();
        sb.AppendLine("Ruta;Orden;Repartidor;Tipo;Sector;CodigoTarjeta;Cliente;Direccion");
        var n = 0;
        foreach (var r in d.Rutas)
        {
            n++;
            foreach (var t in r.Tarjetas)
                sb.AppendLine(string.Join(";", new[]
                {
                    n.ToString(CultureInfo.InvariantCulture), r.Orden.ToString(CultureInfo.InvariantCulture),
                    CeldaCsv(r.Repartidor), CeldaCsv(r.TipoRepartidor.Texto()), CeldaCsv(r.Sector),
                    CeldaCsv(t.Codigo), CeldaCsv(t.Cliente), CeldaCsv(t.Direccion)
                }));
        }
        var nombre = $"hoja-asignacion-{d.CodigoHojaControl ?? d.HojaId.ToString()}.csv";
        // BOM UTF-8 para que Excel muestre bien las tildes
        return (nombre, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray());
    }

    // ---------- Apoyo ----------

    private static ResultadoAsignacion Fallo(string mensaje) => new(false, new() { mensaje }, new());

    private static bool Compatible(TipoRepartidorRequerido requerido, TipoRepartidor tipo) =>
        requerido == TipoRepartidorRequerido.Indiferente
        || (requerido == TipoRepartidorRequerido.Motorizado && tipo == TipoRepartidor.Motorizado)
        || (requerido == TipoRepartidorRequerido.Caminante && tipo == TipoRepartidor.Caminante);

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..max];

    // Evita que Excel interprete una celda como fórmula y entrecomilla cuando hace falta
    private static string CeldaCsv(string valor)
    {
        valor ??= string.Empty;
        if (valor.Length > 0 && "=+-@\t\r".Contains(valor[0])) valor = "'" + valor;
        if (valor.Contains(';') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r'))
            valor = "\"" + valor.Replace("\"", "\"\"") + "\"";
        return valor;
    }

    private static string? Bloqueo(Contexto c)
    {
        if (c.Reporte is null)
            return "El lote aún no tiene reporte de densidad. Genérelo en «Reporte de densidad» antes de asignar repartidores.";
        if (c.TieneHoja)
            return "El lote ya tiene una hoja de asignación generada.";

        var zonasConAlerta = c.ConAlertaActiva.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (zonasConAlerta.Count > 0)
        {
            var nombres = c.Reporte.Zonas.Where(z => zonasConAlerta.Contains(z.ZonaId)).Select(z => z.Zona?.Nombre ?? $"Zona {z.ZonaId}");
            return "Hay alertas de dirección activas en: " + string.Join(", ", nombres) + ". El Buscador debe resolverlas antes de asignar.";
        }
        foreach (var z in c.Reporte.Zonas)
            if (c.Vivas.GetValueOrDefault(z.ZonaId) != z.CantidadTarjetas)
                return $"El reporte de densidad ya no coincide con las tarjetas actuales de la zona {z.Zona?.Nombre}. Revise la planilla y el reporte.";
        if (!c.Asignables.Values.Any(l => l.Count > 0))
            return "Ninguna tarjeta del lote tiene cita programada, por lo que no hay nada que asignar a una ruta.";
        return null;
    }

    private GenerarHojaVM ConstruirVm(Contexto c, GenerarHojaVM? entrada)
    {
        var zonas = (c.Reporte?.Zonas ?? new List<ReporteDensidadZona>())
            .Select(z =>
            {
                var e = entrada?.Zonas?.FirstOrDefault(x => x.ZonaId == z.ZonaId);
                return new ZonaAsignacionVM
                {
                    ZonaId = z.ZonaId,
                    ZonaNombre = z.Zona?.Nombre ?? $"Zona {z.ZonaId}",
                    CantidadReporte = z.CantidadTarjetas,
                    Asignables = c.Asignables.TryGetValue(z.ZonaId, out var l) ? l.Count : 0,
                    TieneAlerta = z.TieneAlerta,
                    TipoRequerido = z.TipoRepartidorRequerido,
                    OrdenVisita = c.OrdenPlanilla.GetValueOrDefault(z.ZonaId, 0),
                    Accion = e?.Accion ?? AccionZonaAsignacion.Asignar,
                    RepartidorId = e?.RepartidorId,
                    DestinoZonaId = e?.DestinoZonaId
                };
            })
            .OrderBy(z => z.OrdenVisita == 0 ? int.MaxValue : z.OrdenVisita).ThenBy(z => z.ZonaNombre).ToList();

        return new GenerarHojaVM
        {
            LoteId = c.Lote.Id,
            CodigoHojaControl = c.Lote.CodigoHojaControl,
            Remito = c.Lote.Remito?.Numero,
            Banco = c.Lote.Remito?.Banco?.Nombre,
            FechaJornada = HoraLima.Ahora.Date,
            EstadoReporte = c.Reporte?.Estado,
            Zonas = zonas,
            Repartidores = c.Repartidores.Select(r => new RepartidorDisponibleVM
            {
                Id = r.Id, Nombre = r.Nombre, Tipo = r.Tipo, ZonaHabitual = r.ZonaHabitual?.Nombre,
                Capacidad = r.CapacidadTarjetas, AsignadasHoy = c.AsignadasHoy.GetValueOrDefault(r.Id)
            }).ToList(),
            Bloqueo = Bloqueo(c)
        };
    }

    private async Task<Contexto?> CargarContextoAsync(int loteId)
    {
        var lote = await _db.Lotes.Include(l => l.Remito).ThenInclude(r => r!.Banco).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote is null) return null;

        var c = new Contexto { Lote = lote };
        c.Reporte = await _db.ReportesDensidad.Include(r => r.Zonas).ThenInclude(z => z.Zona).FirstOrDefaultAsync(r => r.LoteId == loteId);
        c.TieneHoja = await _db.HojasAsignacion.AnyAsync(h => h.LoteId == loteId);

        var planilla = await _db.Planillas.Include(p => p.Zonas).FirstOrDefaultAsync(p => p.LoteId == loteId);
        if (planilla is not null)
            foreach (var z in planilla.Zonas) c.OrdenPlanilla[z.ZonaId] = z.OrdenVisita;

        // Con seguimiento: el servicio de estados modifica estas tarjetas al generar la hoja
        var tarjetas = await _db.Tarjetas.Where(t => t.LoteId == loteId && t.ZonaId != null).ToListAsync();
        var ids = tarjetas.Select(t => t.Id).ToList();
        var conAlerta = (await _db.Alertas.Where(a => a.Decision == DecisionAlerta.Pendiente && ids.Contains(a.TarjetaId))
            .Select(a => a.TarjetaId).Distinct().ToListAsync()).ToHashSet();

        foreach (var t in tarjetas.Where(t => ReglasClasificacion.Elegibles.Contains(t.Estado)))
        {
            var z = t.ZonaId!.Value;
            c.Vivas[z] = c.Vivas.GetValueOrDefault(z) + 1;
            if (conAlerta.Contains(t.Id)) { c.ConAlertaActiva[z] = c.ConAlertaActiva.GetValueOrDefault(z) + 1; continue; }
            if (t.Estado != EstadoTarjeta.CitaProgramada) continue;
            if (!c.Asignables.TryGetValue(z, out var lista)) c.Asignables[z] = lista = new();
            lista.Add(t);
        }

        c.Repartidores = await _db.Repartidores.Include(r => r.ZonaHabitual).Where(r => r.Activo).OrderBy(r => r.Nombre).ToListAsync();

        // Tarjetas que cada repartidor ya tiene asignadas en la jornada de hoy (hora de Lima)
        var desde = DateTime.UtcNow.AddHours(-36);
        var hoy = HoraLima.Ahora.Date;
        var rutasHoy = await _db.AsignacionesRuta.Include(r => r.HojaAsignacion)
            .Where(r => r.HojaAsignacion!.Estado == EstadoHojaAsignacion.Emitida && r.HojaAsignacion.FechaHoraEmision >= desde)
            .ToListAsync();
        foreach (var r in rutasHoy.Where(r => HoraLima.Convertir(r.HojaAsignacion!.FechaHoraEmision).Date == hoy))
            c.AsignadasHoy[r.RepartidorId] = c.AsignadasHoy.GetValueOrDefault(r.RepartidorId) + r.CantidadTarjetas;

        return c;
    }
}
