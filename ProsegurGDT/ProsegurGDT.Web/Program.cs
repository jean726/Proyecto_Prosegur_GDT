using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Services;
using ProsegurGDT.Web.Services.Api;
using ProsegurGDT.Web.Services.Salud;
using ProsegurGDT.Web.Services.Seguridad;
using Microsoft.AspNetCore.Mvc.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);
// C2: no anunciar el servidor en la cabecera «Server»
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 8;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddRoles<IdentityRole>()
.AddErrorDescriber<IdentityErrorDescriberEspanol>()
.AddSignInManager<ApplicationSignInManager>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// RNF04: cierre de sesión por inactividad
builder.Services.ConfigureApplicationCookie(o =>
{
    o.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    o.SlidingExpiration = true;
    o.LoginPath = "/Identity/Account/Login";
    // RN-29: una cookie con rol Repartidor no es válida en la web (defensa adicional al bloqueo del login)
    var validarSello = o.Events.OnValidatePrincipal;
    o.Events.OnValidatePrincipal = async ctx =>
    {
        if (ctx.Principal?.IsInRole(ProsegurGDT.Web.Domain.Roles.Repartidor) == true)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }
        await validarSello(ctx);
    };
    o.AccessDeniedPath = "/Home/AccesoDenegado";
});
// C2: cookies solo por HTTPS fuera de desarrollo (en local se admite el perfil http)
var cookiesSeguras = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.Configure<CookiePolicyOptions>(o =>
{
    o.Secure = cookiesSeguras;
    o.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    o.MinimumSameSitePolicy = SameSiteMode.Lax;
});
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = cookiesSeguras);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<ITarjetaEstadoService, TarjetaEstadoService>();
// Motor de IA: IA:Motor = hibrido (por defecto) | onnx | hash. IA:PesoOnnx (0-1, por defecto 0.5) pondera el modelo en el híbrido.
// Si el modelo ONNX no está en IA:ModeloRuta, se usa hash como respaldo.
builder.Services.AddSingleton<IEmbeddingService>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("MotorIA");
    var motor = (cfg["IA:Motor"] ?? "hibrido").Trim().ToLowerInvariant();
    if (motor != "hash")
    {
        var carpeta = cfg["IA:ModeloRuta"];
        if (string.IsNullOrWhiteSpace(carpeta))
            carpeta = Path.Combine(sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "modelos", "paraphrase-multilingual-MiniLM-L12-v2");
        if (OnnxEmbeddingService.Disponible(carpeta))
        {
            try
            {
                var onnx = new OnnxEmbeddingService(carpeta);
                if (motor == "onnx") return onnx;
                var peso = double.TryParse(cfg["IA:PesoOnnx"], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 0.5;
                return new HybridEmbeddingService(onnx, new HashingNgramEmbeddingService(), peso);
            }
            catch (Exception ex) { log.LogError(ex, "No se pudo cargar el modelo ONNX de {Carpeta}; se usa el motor hash.", carpeta); }
        }
        else log.LogWarning("Modelo ONNX no encontrado en {Carpeta}; se usa el motor hash.", carpeta);
    }
    return new HashingNgramEmbeddingService();
});
builder.Services.AddScoped<IAddressRiskService, AddressRiskService>();
// Una cuenta desactivada pierde la sesión en ≤ 1 minuto
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

// Todo el sitio exige sesión; lo público se marca con [AllowAnonymous]
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AuthorizeFilter());
    // C2: todo POST/PUT/DELETE de la web exige token antiforgery aunque la acción no lo declare (la API usa JWT y se exime)
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    o.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    o.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((v, c) => $"El valor '{v}' no es válido para {c}.");
    o.ModelBindingMessageProvider.SetValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
    o.ModelBindingMessageProvider.SetMissingBindRequiredValueAccessor(c => $"Falta el valor de {c}.");
})
.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, AdaptadorValidacionEspanol>();

// B2 — API REST /api/v1 para la app móvil del Repartidor. Las rutas /api se autentican solo con JWT
// (las cookies de la web no valen allí y no se redirige al login: responde 401/403).
var opcionesJwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
opcionesJwt.Preparar(LoggerFactory.Create(l => l.AddConsole()).CreateLogger("Jwt"));
builder.Services.AddSingleton(opcionesJwt);
builder.Services.AddSingleton<ITokenMovilService, TokenMovilService>();
builder.Services.AddScoped<IRepartidorMovilService, RepartidorMovilService>();
const string EsquemaPorRuta = "PorRuta";
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = EsquemaPorRuta;
    o.DefaultAuthenticateScheme = EsquemaPorRuta;
    o.DefaultChallengeScheme = EsquemaPorRuta;
    o.DefaultForbidScheme = EsquemaPorRuta;
})
.AddPolicyScheme(EsquemaPorRuta, "Cookie (web) o JWT (/api)", o => o.ForwardDefaultSelector = ctx =>
    ctx.Request.Path.StartsWithSegments("/api") ? JwtBearerDefaults.AuthenticationScheme : IdentityConstants.ApplicationScheme)
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = opcionesJwt.Parametros();
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = ValidadorTokenMovil.ValidarAsync,
        // 401/403 con ProblemDetails en español, sin redirigir al login de la web
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.Headers.WWWAuthenticate = "Bearer";
            await ctx.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 401, Title = "No autorizado",
                Detail = "Su sesión no es válida o expiró. Inicie sesión nuevamente en la aplicación móvil."
            }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
        },
        OnForbidden = ctx => ctx.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 403, Title = "Acceso denegado",
            Detail = "Su cuenta no tiene acceso a la aplicación móvil."
        }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json")
    };
});
// CU22: máximo de intentos de inicio de sesión por minuto y dirección IP (además del bloqueo de la cuenta).
// Limites:TokenMovilPorMinuto para la API y Limites:LoginWebPorMinuto para la web; 10 por defecto.
var limites = builder.Configuration.GetSection(OpcionesLimites.Seccion).Get<OpcionesLimites>() ?? new OpcionesLimites();
builder.Services.AddSingleton(limites);
builder.Services.AddSingleton<ILimitadorLoginWeb, LimitadorLoginWeb>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 429, Title = "Demasiados intentos",
            Detail = "Demasiados intentos de inicio de sesión. Espere un minuto e intente nuevamente."
        }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
    };
    o.AddPolicy("token-movil", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limites.TokenMovilPorMinuto, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
// C4 — /health: estado de la base de datos y del motor de IA (para monitoreo y para comprobar un despliegue)
builder.Services.AddHealthChecks()
    .AddCheck<ComprobacionBaseDatos>("base-de-datos")
    .AddCheck<ComprobacionMotorIa>("motor-ia");
builder.Services.AddOpenApi("v1", o => o.ShouldInclude = d => d.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);
// Singleton
builder.Services.AddSingleton<IConfiguracionOperativa, ConfiguracionOperativa>();
// Strategy: una estrategia por resultado de llamada
builder.Services.AddScoped<IManejadorResultadoLlamada, ManejadorCitaExitosa>();
builder.Services.AddScoped<IManejadorResultadoLlamada, ManejadorContactoSinAcuerdo>();
builder.Services.AddScoped<IManejadorResultadoLlamada, ManejadorDatosInsuficientes>();
builder.Services.AddScoped<IManejadorResultadoLlamada, ManejadorNoContacto>();
// Adapter
builder.Services.AddSingleton<IAdaptadorFichaClientes, AdaptadorCsvUnibanca>();
builder.Services.AddScoped<IImportadorFichaService, ImportadorFichaService>();
// Fase 7 — Rutas
builder.Services.AddScoped<IPlanillaArmadoService, PlanillaArmadoService>();
builder.Services.AddScoped<IDensidadService, DensidadService>();
builder.Services.AddScoped<IAsignacionService, AsignacionService>();
builder.Services.AddScoped<IConciliacionService, ConciliacionService>();
builder.Services.AddScoped<IFaltantesService, FaltantesService>();
// Fase 8 — Mensajería
builder.Services.AddScoped<IHojaRutaService, HojaRutaService>();
builder.Services.AddScoped<IFraudeService, FraudeService>();
builder.Services.AddScoped<IReporteEntregasService, ReporteEntregasService>();
// Fase 9 — Descargo
builder.Services.AddScoped<IDescargoService, DescargoService>();
builder.Services.AddScoped<IDefraudacionService, DefraudacionService>();
builder.Services.AddScoped<IReportesDescargoService, ReportesDescargoService>();

// B4 — Intranet: la web solo responde desde las redes de la empresa; /api queda fuera (JWT).
// Detrás del proxy de Azure la IP real llega en X-Forwarded-For: se toma solo el último salto (el que añade Azure),
// así una cabecera inventada por el cliente no salta el filtro. También la usa el limitador del login móvil.
builder.Services.Configure<OpcionesIntranet>(builder.Configuration.GetSection(OpcionesIntranet.Seccion));
var confiarEnProxy = builder.Configuration.GetValue<bool?>("Intranet:ConfiarEnProxy")
    ?? !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
// Si el host ya procesa las cabeceras (ASPNETCORE_FORWARDEDHEADERS_ENABLED), no se repite: una segunda pasada
// consumiría el valor puesto por el cliente.
var forwardedDelHost = builder.Configuration.GetValue<bool>("FORWARDEDHEADERS_ENABLED");
if (confiarEnProxy || forwardedDelHost)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

var app = builder.Build();
if (confiarEnProxy && !forwardedDelHost) app.UseForwardedHeaders();
app.UseMiddleware<CabecerasSeguridadMiddleware>();
_ = app.Services.GetRequiredService<IConfiguracionOperativa>();

var operativa = app.Services.GetRequiredService<IConfiguracionOperativa>();
app.Logger.LogInformation("Umbrales de la IA: medio={Medio} alto={Alto}; intentos de contacto: {Intentos}",
    operativa.UmbralMedio, operativa.UmbralAlto, operativa.MaxIntentosContacto);

using (var scope = app.Services.CreateScope())
{
    // Los datos demo se cargan en Development o con Seed:Demo=true (presentación en Azure)
    var cargarDemo = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:Demo");
    await DbSeeder.SeedAsync(scope.ServiceProvider, cargarDemo);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// C4 — Las respuestas de error sin cuerpo (404, 400, 405…) muestran una página propia en español.
// La API y /health conservan sus respuestas JSON.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/health")
        && !ctx.Request.Path.StartsWithSegments("/openapi"),
    rama => rama.UseStatusCodePagesWithReExecute("/Home/Estado/{0}"));

app.UseHttpsRedirection();
app.UseCookiePolicy();
app.UseMiddleware<FiltroIntranetMiddleware>();

// De las páginas de Identity solo se usan el inicio y el cierre de sesión. El registro público queda cerrado
// (los usuarios los crea el Administrador, CU24) y el resto de la interfaz por defecto (recuperar contraseña,
// gestión de la cuenta, borrado de datos personales, 2FA) no forma parte del sistema: responde 404.
app.Use(async (ctx, next) =>
{
    var ruta = ctx.Request.Path;
    if (ruta.StartsWithSegments("/Identity", StringComparison.OrdinalIgnoreCase)
        && !ruta.Equals("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
        && !ruta.Equals("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase))
    {
        if (ruta.StartsWithSegments("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase))
            ctx.Response.Redirect("/Identity/Account/Login");
        else
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health", SaludRespuesta.Opciones).AllowAnonymous();

// Documento OpenAPI de /api/v1 (solo en desarrollo): /openapi/v1.json
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
// C1: las pruebas de integración (WebApplicationFactory) necesitan ver la clase Program.
public partial class Program;
