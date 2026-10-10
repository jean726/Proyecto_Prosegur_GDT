using System.Security.Claims;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Services;

public interface IAuditoriaService
{
    Task RegistrarAsync(string accion, string? entidad = null, string? entidadId = null, string? detalle = null);
}

public class AuditoriaService : IAuditoriaService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    public AuditoriaService(ApplicationDbContext db, IHttpContextAccessor http) { _db = db; _http = http; }

    public async Task RegistrarAsync(string accion, string? entidad = null, string? entidadId = null, string? detalle = null)
    {
        var user = _http.HttpContext?.User;
        _db.AuditLogs.Add(new AuditLog
        {
            FechaHoraUtc = DateTime.UtcNow,
            UsuarioId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            UsuarioNombre = user?.Identity?.Name ?? "sistema",
            Accion = accion, Entidad = entidad, EntidadId = entidadId,
            Detalle = detalle is { Length: > 500 } ? detalle[..500] : detalle
        });
        await _db.SaveChangesAsync();
    }
}