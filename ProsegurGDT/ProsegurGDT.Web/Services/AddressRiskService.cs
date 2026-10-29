using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Services;

public record CoincidenciaDireccion(int DireccionId, string Direccion, double Puntaje, bool Exacta, string Detalle);

public interface IAddressRiskService
{
    double UmbralMedio { get; }
    double UmbralAlto { get; }
    Task<List<CoincidenciaDireccion>> BuscarAsync(string direccion, int max = 5);
    Task<int> EvaluarLoteAsync(int loteId);
    Task<DireccionFraudulenta> AgregarAsync(string direccion, string motivo, string usuarioId, bool validada, string? origen);
    Task ValidarAsync(DireccionFraudulenta d, string usuarioId);
    Task ReindexarAsync();
}

public class AddressRiskService : IAddressRiskService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmbeddingService _emb;
    private const double PenalizacionNumeros = 0.85;

    public double UmbralMedio { get; }
    public double UmbralAlto { get; }

    private sealed record Entrada(DireccionFraudulenta D, float[] V);

    public AddressRiskService(ApplicationDbContext db, IEmbeddingService emb, IConfiguracionOperativa cfg)
    {
        _db = db; _emb = emb;
        UmbralMedio = cfg.UmbralMedio;
        UmbralAlto = cfg.UmbralAlto;
    }

    public async Task<List<CoincidenciaDireccion>> BuscarAsync(string direccion, int max = 5)
    {
        var norm = NormalizadorDirecciones.Normalizar(direccion);
        if (norm.Length == 0) return new();
        var v = _emb.Embed(norm);
        var indice = await CargarIndiceAsync();
        return indice
            .Select(e =>
            {
                var p = Puntuar(norm, v, e.D.DireccionNormalizada, e.V, out var exacta, out var det);
                return new CoincidenciaDireccion(e.D.Id, e.D.DireccionOriginal, Math.Round(p, 4), exacta, det);
            })
            .OrderByDescending(c => c.Puntaje).Take(max).ToList();
    }

    public async Task<int> EvaluarLoteAsync(int loteId)
    {
        var indice = await CargarIndiceAsync();
        if (indice.Count == 0) return 0;

        var elegibles = ReglasClasificacion.Elegibles;
        var tarjetas = await _db.Tarjetas.Include(t => t.Cliente)
            .Where(t => t.LoteId == loteId && elegibles.Contains(t.Estado)).ToListAsync();
        var ids = tarjetas.Select(t => t.Id).ToList();
        var existentes = await _db.Alertas.Where(a => ids.Contains(a.TarjetaId))
            .Select(a => new { a.TarjetaId, a.DireccionFraudulentaId }).ToListAsync();
        var yaHay = existentes.Select(e => (e.TarjetaId, e.DireccionFraudulentaId)).ToHashSet();

        var obsoletas = await _db.Alertas
            .Where(a => ids.Contains(a.TarjetaId)
             && a.Decision == DecisionAlerta.Pendiente
             && a.Puntaje < UmbralMedio)
            .ToListAsync();
        if (obsoletas.Count > 0) _db.Alertas.RemoveRange(obsoletas);

        var cache = new Dictionary<string, float[]>();
        var nuevas = 0;
        foreach (var t in tarjetas)
        {
            var norm = string.IsNullOrWhiteSpace(t.Cliente!.DireccionNormalizada)
                ? NormalizadorDirecciones.Normalizar(t.Cliente.Direccion) : t.Cliente.DireccionNormalizada!;
            if (norm.Length == 0) continue;
            if (!cache.TryGetValue(norm, out var v)) cache[norm] = v = _emb.Embed(norm);

            Entrada? mejor = null; double mejorP = 0; var ex = false; var det = "";
            foreach (var e in indice)
            {
                var p = Puntuar(norm, v, e.D.DireccionNormalizada, e.V, out var exacta, out var d);
                if (p > mejorP) { mejorP = p; mejor = e; ex = exacta; det = d; }
            }
            if (mejor is null || mejorP < UmbralMedio) continue;
            if (yaHay.Contains((t.Id, mejor.D.Id))) continue;

            _db.Alertas.Add(new AlertaDireccion
            {
                TarjetaId = t.Id,
                DireccionFraudulentaId = mejor.D.Id,
                Puntaje = Math.Round(mejorP, 4),
                Nivel = mejorP >= UmbralAlto ? NivelAlerta.Alta : NivelAlerta.Media,
                Metodo = ex ? "Exacta" : _emb.Version,
                Detalle = det,
                Decision = DecisionAlerta.Pendiente,
                FechaGeneracion = DateTime.UtcNow
            });
            nuevas++;
        }
        if (nuevas > 0 || obsoletas.Count > 0) await _db.SaveChangesAsync();
        return nuevas;
    }

    public async Task<DireccionFraudulenta> AgregarAsync(string direccion, string motivo, string usuarioId, bool validada, string? origen)
    {
        var norm = NormalizadorDirecciones.Normalizar(direccion);
        var d = new DireccionFraudulenta
        {
            DireccionOriginal = direccion.Trim(),
            DireccionNormalizada = norm,
            Motivo = motivo.Trim(),
            Origen = origen,
            Estado = EstadoDireccion.Pendiente,
            RegistradaPorId = usuarioId,
            FechaRegistro = DateTime.UtcNow
        };
        if (validada) Marcar(d, usuarioId);
        _db.DireccionesFraudulentas.Add(d);
        await _db.SaveChangesAsync();
        return d;
    }

    public async Task ValidarAsync(DireccionFraudulenta d, string usuarioId)
    {
        Marcar(d, usuarioId);
        await _db.SaveChangesAsync();
    }

    public async Task ReindexarAsync()
    {
        var lista = await _db.DireccionesFraudulentas.Where(d => d.Estado == EstadoDireccion.Validada).ToListAsync();
        foreach (var d in lista) Indexar(d);
        await _db.SaveChangesAsync();
    }

    // ---------- internos ----------
    private void Marcar(DireccionFraudulenta d, string usuarioId)
    {
        d.Estado = EstadoDireccion.Validada;
        d.ValidadaPorId = usuarioId;
        d.FechaValidacion = DateTime.UtcNow;
        Indexar(d);
    }

    private void Indexar(DireccionFraudulenta d)
    {
        d.Vector = VectorUtil.ToBytes(_emb.Embed(d.DireccionNormalizada));
        d.ModeloVersion = _emb.Version;
    }

    private async Task<List<Entrada>> CargarIndiceAsync()
    {
        var lista = await _db.DireccionesFraudulentas.Where(d => d.Estado == EstadoDireccion.Validada).ToListAsync();
        var cambios = false;
        var res = new List<Entrada>();
        foreach (var d in lista)
        {
            if (d.Vector is null || d.ModeloVersion != _emb.Version) { Indexar(d); cambios = true; }
            res.Add(new Entrada(d, VectorUtil.FromBytes(d.Vector!)));
        }
        if (cambios) await _db.SaveChangesAsync();
        return res;
    }

    private static double Puntuar(string normA, float[] vA, string normB, float[] vB, out bool exacta, out string detalle)
    {
        if (normA == normB) { exacta = true; detalle = "Coincidencia exacta"; return 1.0; }
        exacta = false;
        var cos = Math.Clamp(VectorUtil.Dot(vA, vB), 0, 1);
        var numerosIguales = Numeros(normA).SetEquals(Numeros(normB));
        detalle = $"Similitud {cos:P0}; numeración {(numerosIguales ? "igual" : "distinta (penalizada)")}";
        return numerosIguales ? cos : cos * PenalizacionNumeros;
    }

    private static HashSet<string> Numeros(string s) =>
        Regex.Matches(s, @"\d+").Select(m => m.Value).ToHashSet();
}