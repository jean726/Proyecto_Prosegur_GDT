using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Tests.Integracion;

/// <summary>
/// Arranca la web real en memoria contra una base SQL Server nueva: aplica las migraciones y carga los datos demo,
/// igual que en Development. La base se llama «gdt-pruebas-…» y se borra al terminar.
/// El servidor se toma de la variable GDT_TEST_SQL (por defecto, LocalDB).
/// </summary>
public sealed class FabricaWeb : WebApplicationFactory<Program>
{
    public const string PrefijoBase = "gdt-pruebas-";
    public const string AdminCorreo = "admin.pruebas@gdt.local";
    public const string AdminClave = "Adm1n#Pruebas-2026";
    public const string ClaveDemo = "Pruebas#2026Gdt";

    public string NombreBase { get; } = PrefijoBase + Guid.NewGuid().ToString("N")[..12];

    public FabricaWeb()
    {
        var servidor = Environment.GetEnvironmentVariable("GDT_TEST_SQL");
        if (string.IsNullOrWhiteSpace(servidor)) servidor = @"(localdb)\mssqllocaldb";

        // Variables de entorno: tienen prioridad sobre appsettings y los secretos de usuario, y Program.cs
        // las lee antes de construir la aplicación. Así las pruebas nunca tocan la base de desarrollo.
        Fijar("ConnectionStrings__DefaultConnection",
            $"Server={servidor};Database={NombreBase};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
        Fijar("SeedAdmin__Email", AdminCorreo);
        Fijar("SeedAdmin__Password", AdminClave);
        Fijar("Seed__Demo", "true");
        Fijar("Seed__ClaveDemo", ClaveDemo);
        // Como detrás del proxy de Azure: la IP del cliente llega en X-Forwarded-For. Cada sesión de prueba
        // usa una IP distinta, así los límites de intentos por IP no se mezclan entre pruebas.
        Fijar("Intranet__ConfiarEnProxy", "true");
        Fijar("IA__Motor", "hash");                 // sin el modelo ONNX: rápido y determinista
        Fijar("IA__UmbralMedio", "0.60");
        Fijar("IA__UmbralAlto", "0.90");
        Fijar("Jwt__Clave", "clave-jwt-solo-para-las-pruebas-de-integracion-0123456789-abcdefghij");
        Fijar("Intranet__Habilitada", "false");
    }

    private static void Fijar(string clave, string valor) => Environment.SetEnvironmentVariable(clave, valor);

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    /// <summary>Ámbito de servicios para consultar la base o usar UserManager desde una prueba.</summary>
    public async Task<T> ConBaseAsync<T>(Func<ApplicationDbContext, IServiceProvider, Task<T>> accion)
    {
        using var ambito = Services.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ComprobarBaseDePruebas(db);
        return await accion(db, ambito.ServiceProvider);
    }

    private static void ComprobarBaseDePruebas(ApplicationDbContext db)
    {
        var nombre = db.Database.GetDbConnection().Database;
        if (!nombre.StartsWith(PrefijoBase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Las pruebas apuntan a la base «{nombre}», que no es de pruebas. Se detienen para no tocarla.");
    }

    private int _ips;

    public SesionWeb NuevaSesion()
    {
        var http = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        var n = Interlocked.Increment(ref _ips);
        var ip = $"10.20.{n / 250}.{n % 250 + 1}";
        http.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return new SesionWeb(http, ip);
    }

    public static string CorreoDemo(string rol) => rol == Roles.Administrador ? AdminCorreo : $"{rol.ToLowerInvariant()}@gdt.local";
    public static string ClaveDe(string rol) => rol == Roles.Administrador ? AdminClave : ClaveDemo;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<SesionWeb>> _sesiones = new();

    /// <summary>Sesión compartida del rol (una por rol en toda la ejecución). No cerrarla: para eso, <see cref="NuevaSesionDeAsync"/>.</summary>
    public Task<SesionWeb> SesionDeAsync(string rol) => _sesiones.GetOrAdd(rol, NuevaSesionDeAsync);

    /// <summary>Sesión propia de la prueba, con su propio inicio de sesión.</summary>
    public async Task<SesionWeb> NuevaSesionDeAsync(string rol)
    {
        var s = NuevaSesion();
        var r = await s.IniciarAsync(CorreoDemo(rol), ClaveDe(rol));
        if (r.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"No se pudo iniciar sesión como {rol}: {(int)r.StatusCode}");
        return s;
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            using var ambito = Services.CreateScope();
            var db = ambito.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (db.Database.GetDbConnection().Database.StartsWith(PrefijoBase, StringComparison.Ordinal))
                await db.Database.EnsureDeletedAsync();
        }
        catch (Exception) { /* si la base ya no existe o el host no llegó a arrancar, no hay nada que limpiar */ }
        await base.DisposeAsync();
    }
}

/// <summary>Todas las pruebas de integración comparten una sola aplicación y una sola base, y corren en serie.</summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionIntegracion : ICollectionFixture<FabricaWeb>
{
    public const string Nombre = "Integración";
}

/// <summary>Navegador mínimo: guarda la cookie de sesión y envía el token antiforgery de los formularios.</summary>
public sealed partial class SesionWeb(HttpClient http, string ip)
{
    public HttpClient Http { get; } = http;
    public string Ip { get; } = ip;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenAntiforgery();

    public static string? ExtraerToken(string html) => TokenAntiforgery().Match(html) is { Success: true } m ? m.Groups[1].Value : null;

    public Task<HttpResponseMessage> GetAsync(string ruta) => Http.GetAsync(ruta);

    public async Task<string> HtmlAsync(string ruta)
    {
        var r = await Http.GetAsync(ruta);
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"GET {ruta} devolvió {(int)r.StatusCode}");
        return await r.Content.ReadAsStringAsync();
    }

    /// <summary>Abre la página que contiene el formulario, toma su token y envía el POST.</summary>
    public async Task<HttpResponseMessage> PostAsync(string rutaFormulario, string rutaPost, params (string, string)[] campos)
    {
        var token = ExtraerToken(await HtmlAsync(rutaFormulario)) ?? throw new InvalidOperationException($"{rutaFormulario} no tiene token antiforgery.");
        var datos = campos.Select(c => KeyValuePair.Create(c.Item1, c.Item2)).Append(KeyValuePair.Create("__RequestVerificationToken", token));
        return await Http.PostAsync(rutaPost, new FormUrlEncodedContent(datos));
    }

    public Task<HttpResponseMessage> IniciarAsync(string correo, string clave) =>
        PostAsync("/Identity/Account/Login", "/Identity/Account/Login", ("Input.Email", correo), ("Input.Password", clave));
}

public static class RespuestaExt
{
    /// <summary>Ruta de la redirección, sin el dominio ni los parámetros.</summary>
    public static string RutaDestino(this HttpResponseMessage r)
    {
        var u = r.Headers.Location ?? throw new InvalidOperationException("La respuesta no redirige.");
        return u.IsAbsoluteUri ? u.AbsolutePath : u.OriginalString.Split('?')[0];
    }
}
