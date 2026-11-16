using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProsegurGDT.Web.Data;

namespace ProsegurGDT.Web.Services.Salud;

// C4 — Comprobaciones de /health: base de datos y motor de IA.
// La respuesta no incluye datos sensibles (ni cadenas de conexión ni rutas ni mensajes de excepción).

public sealed class ComprobacionBaseDatos : IHealthCheck
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ComprobacionBaseDatos> _log;
    public ComprobacionBaseDatos(ApplicationDbContext db, ILogger<ComprobacionBaseDatos> log) { _db = db; _log = log; }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("No se pudo conectar con la base de datos.");
            var pendientes = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            return pendientes == 0
                ? HealthCheckResult.Healthy("Conectada y con las migraciones al día.")
                : HealthCheckResult.Degraded($"Conectada, con {pendientes} migraciones pendientes.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Comprobación de salud: fallo al consultar la base de datos.");
            return HealthCheckResult.Unhealthy("No se pudo consultar la base de datos.");
        }
    }
}

public sealed class ComprobacionMotorIa : IHealthCheck
{
    private readonly IEmbeddingService _motor;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ComprobacionMotorIa> _log;
    public ComprobacionMotorIa(IEmbeddingService motor, IConfiguration cfg, ILogger<ComprobacionMotorIa> log) { _motor = motor; _cfg = cfg; _log = log; }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Una dirección de ejemplo debe dar un vector unitario de la dimensión del motor
            var v = _motor.Embed("AVENIDA LARCO 105 MIRAFLORES");
            var norma = Math.Sqrt(v.Sum(x => (double)x * x));
            if (v.Length != _motor.Dimension || Math.Abs(norma - 1) > 0.01)
                return Task.FromResult(HealthCheckResult.Unhealthy("El motor de IA devolvió un vector no válido."));

            var datos = new Dictionary<string, object> { ["motor"] = _motor.Version, ["modeloOnnx"] = _motor.ModeloIaActivo };
            var pedido = (_cfg["IA:Motor"] ?? "hibrido").Trim().ToLowerInvariant();
            return Task.FromResult(!_motor.ModeloIaActivo && pedido != "hash"
                ? HealthCheckResult.Degraded("Modelo ONNX no disponible: funciona el motor de respaldo (hash).", data: datos)
                : HealthCheckResult.Healthy("Motor de IA operativo.", datos));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Comprobación de salud: fallo del motor de IA.");
            return Task.FromResult(HealthCheckResult.Unhealthy("El motor de IA no responde."));
        }
    }
}

public static class SaludRespuesta
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static string Texto(HealthStatus s) => s switch
    {
        HealthStatus.Healthy => "Correcto",
        HealthStatus.Degraded => "Degradado",
        _ => "Fallo"
    };

    public static readonly HealthCheckOptions Opciones = new()
    {
        AllowCachingResponses = false,
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,      // degradado = sigue atendiendo
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = (ctx, informe) =>
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                estado = Texto(informe.Status),
                duracionMs = Math.Round(informe.TotalDuration.TotalMilliseconds),
                comprobaciones = informe.Entries.Select(e => new
                {
                    nombre = e.Key,
                    estado = Texto(e.Value.Status),
                    detalle = e.Value.Description,
                    duracionMs = Math.Round(e.Value.Duration.TotalMilliseconds),
                    datos = e.Value.Data.Count > 0 ? e.Value.Data : null
                })
            }, Json));
        }
    };
}
