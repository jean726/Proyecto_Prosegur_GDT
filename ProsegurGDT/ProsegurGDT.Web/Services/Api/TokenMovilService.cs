using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Services.Api;

// B2 — Tokens JWT de la app móvil del Repartidor (CU22/CU23 por la API).
// La clave viene de Jwt:Clave (User Secrets en local, App Service Configuration en Azure; nunca en el repo).
// Si falta, se genera una clave aleatoria en memoria: los tokens dejan de valer al reiniciar la app.
public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";
    public string Emisor { get; set; } = "ProsegurGDT";
    public string Audiencia { get; set; } = "ProsegurGDT.Movil";
    public string? Clave { get; set; }
    public int HorasVigencia { get; set; } = 10;   // una jornada de reparto

    public SymmetricSecurityKey Llave { get; private set; } = null!;

    public void Preparar(ILogger log)
    {
        var bytes = string.IsNullOrWhiteSpace(Clave) ? null : Encoding.UTF8.GetBytes(Clave);
        if (bytes is null || bytes.Length < 32)
        {
            if (bytes is not null) log.LogWarning("Jwt:Clave tiene menos de 32 bytes; se ignora.");
            log.LogWarning("Jwt:Clave no está configurada: se usa una clave temporal y los tokens de la app móvil caducan al reiniciar.");
            bytes = RandomNumberGenerator.GetBytes(64);
        }
        Llave = new SymmetricSecurityKey(bytes);
    }

    public TokenValidationParameters Parametros() => new()
    {
        ValidIssuer = Emisor,
        ValidAudience = Audiencia,
        IssuerSigningKey = Llave,
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
}

public static class ClaimsMovil
{
    public const string Sello = "gdt_sello";
    public const string Repartidor = "gdt_rep";

    public static int RepartidorId(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue(Repartidor)!);
    public static string UsuarioId(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

public record TokenEmitido(string Token, DateTime ExpiraUtc);

public interface ITokenMovilService
{
    TokenEmitido Emitir(ApplicationUser u, Repartidor r, string sello);
}

public sealed class TokenMovilService : ITokenMovilService
{
    private readonly OpcionesJwt _op;
    public TokenMovilService(OpcionesJwt op) => _op = op;

    public TokenEmitido Emitir(ApplicationUser u, Repartidor r, string sello)
    {
        var expira = DateTime.UtcNow.AddHours(_op.HorasVigencia);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, u.Id),
            new(JwtRegisteredClaimNames.UniqueName, u.UserName ?? u.Email ?? u.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("role", Roles.Repartidor),
            new(ClaimsMovil.Repartidor, r.Id.ToString()),
            new(ClaimsMovil.Sello, sello)
        };
        var jwt = new JwtSecurityToken(_op.Emisor, _op.Audiencia, claims, DateTime.UtcNow, expira,
            new SigningCredentials(_op.Llave, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}

// Cada petición revalida contra la base: cuenta activa y sin bloqueo, rol Repartidor, mismo repartidor vinculado
// y mismo sello de seguridad. Desactivar la cuenta, cambiar su rol o vínculo, o cerrar sesión (CU23) revoca el token.
public static class ValidadorTokenMovil
{
    public static async Task ValidarAsync(TokenValidatedContext ctx)
    {
        var p = ctx.Principal;
        var id = p?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sello = p?.FindFirstValue(ClaimsMovil.Sello);
        var rep = p?.FindFirstValue(ClaimsMovil.Repartidor);
        if (id is null || sello is null || !int.TryParse(rep, out var repId)) { ctx.Fail("Token incompleto."); return; }

        var um = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var u = await um.FindByIdAsync(id);
        if (u is null || !u.Activo || await um.IsLockedOutAsync(u)
            || await um.GetSecurityStampAsync(u) != sello
            || !await um.IsInRoleAsync(u, Roles.Repartidor)
            || !await db.Repartidores.AnyAsync(r => r.Id == repId && r.UsuarioId == u.Id && r.Activo))
            ctx.Fail("Token revocado.");
    }

    public const string Esquema = JwtBearerDefaults.AuthenticationScheme;
}
