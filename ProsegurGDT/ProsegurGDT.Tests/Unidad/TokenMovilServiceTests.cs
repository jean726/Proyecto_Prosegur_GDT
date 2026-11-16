using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services.Api;

namespace ProsegurGDT.Tests.Unidad;

/// <summary>Token JWT de la app móvil del Repartidor (CU22, CU23, RN-29).</summary>
public class TokenMovilServiceTests
{
    private const string ClaveDePrueba = "clave-de-prueba-solo-para-los-tests-0123456789-abcdefghij-0123456789";

    private static OpcionesJwt Opciones(string? clave = ClaveDePrueba, int horas = 10)
    {
        var op = new OpcionesJwt { Clave = clave, HorasVigencia = horas };
        op.Preparar(NullLogger.Instance);
        return op;
    }

    private static readonly ApplicationUser Usuario = new() { Id = "id-usuario", UserName = "repartidor@gdt.local", Email = "repartidor@gdt.local" };
    private static readonly Repartidor Carlos = new() { Id = 7, Nombre = "Carlos Mendoza", Tipo = TipoRepartidor.Motorizado };

    private static ClaimsPrincipal Validar(string token, OpcionesJwt op)
    {
        var manejador = new JwtSecurityTokenHandler { MapInboundClaims = true };
        return manejador.ValidateToken(token, op.Parametros(), out _);
    }

    [Fact]
    public void El_token_emitido_se_valida_y_lleva_usuario_rol_repartidor_y_sello()
    {
        var op = Opciones();
        var emitido = new TokenMovilService(op).Emitir(Usuario, Carlos, "sello-1");

        var p = Validar(emitido.Token, op);

        Assert.Equal("id-usuario", p.UsuarioId());
        Assert.Equal(7, p.RepartidorId());
        Assert.Equal("sello-1", p.FindFirstValue(ClaimsMovil.Sello));
        Assert.True(p.IsInRole(Roles.Repartidor));
        Assert.Equal("repartidor@gdt.local", p.Identity!.Name);
    }

    [Fact]
    public void Vence_a_las_diez_horas_por_defecto()
    {
        var antes = DateTime.UtcNow;
        var emitido = new TokenMovilService(Opciones()).Emitir(Usuario, Carlos, "s");

        Assert.InRange(emitido.ExpiraUtc, antes.AddHours(10).AddSeconds(-5), DateTime.UtcNow.AddHours(10).AddSeconds(5));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(emitido.Token);
        Assert.Equal(emitido.ExpiraUtc, jwt.ValidTo, TimeSpan.FromSeconds(1));
        Assert.Equal("ProsegurGDT", jwt.Issuer);
        Assert.Contains("ProsegurGDT.Movil", jwt.Audiences);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.SignatureAlgorithm);
    }

    [Fact]
    public void Cada_token_tiene_un_identificador_distinto()
    {
        var servicio = new TokenMovilService(Opciones());
        var a = new JwtSecurityTokenHandler().ReadJwtToken(servicio.Emitir(Usuario, Carlos, "s").Token);
        var b = new JwtSecurityTokenHandler().ReadJwtToken(servicio.Emitir(Usuario, Carlos, "s").Token);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Un_token_firmado_con_otra_clave_se_rechaza()
    {
        var emitido = new TokenMovilService(Opciones("otra-clave-distinta-de-al-menos-treinta-y-dos-bytes-000000")).Emitir(Usuario, Carlos, "s");
        Assert.ThrowsAny<SecurityTokenException>(() => Validar(emitido.Token, Opciones()));
    }

    [Fact]
    public void Un_token_alterado_se_rechaza()
    {
        var op = Opciones();
        var token = new TokenMovilService(op).Emitir(Usuario, Carlos, "s").Token;
        var partes = token.Split('.');
        var alterado = $"{partes[0]}.{partes[1]}.{new string(partes[2].Reverse().ToArray())}";

        Assert.ThrowsAny<Exception>(() => Validar(alterado, op));
    }

    [Fact]
    public void Un_token_vencido_se_rechaza()
    {
        // Mismo emisor, audiencia y clave, pero emitido hace 11 horas y vencido hace una
        var op = Opciones();
        var jwt = new JwtSecurityToken(op.Emisor, op.Audiencia, [new Claim(JwtRegisteredClaimNames.Sub, Usuario.Id)],
            DateTime.UtcNow.AddHours(-11), DateTime.UtcNow.AddHours(-1), new SigningCredentials(op.Llave, SecurityAlgorithms.HmacSha256));
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        Assert.Throws<SecurityTokenExpiredException>(() => Validar(token, op));
    }

    [Fact]
    public void Un_token_de_otro_emisor_o_audiencia_se_rechaza()
    {
        var emisor = Opciones(); emisor.Emisor = "Otro";
        var audiencia = Opciones(); audiencia.Audiencia = "OtraApp";

        Assert.Throws<SecurityTokenInvalidIssuerException>(() => Validar(new TokenMovilService(emisor).Emitir(Usuario, Carlos, "s").Token, Opciones()));
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => Validar(new TokenMovilService(audiencia).Emitir(Usuario, Carlos, "s").Token, Opciones()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta")]
    public void Sin_clave_o_con_una_clave_corta_se_genera_una_temporal_de_64_bytes(string? clave)
    {
        var a = Opciones(clave);
        var b = Opciones(clave);

        Assert.Equal(64, a.Llave.Key.Length);
        Assert.NotEqual(a.Llave.Key, b.Llave.Key);   // aleatoria en cada arranque
    }

    [Fact]
    public void Solo_se_acepta_el_algoritmo_HS256()
    {
        Assert.Equal([SecurityAlgorithms.HmacSha256], Opciones().Parametros().ValidAlgorithms);
    }
}
