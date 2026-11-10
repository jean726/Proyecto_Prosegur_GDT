using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Services;
using Microsoft.AspNetCore.Mvc.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

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
    o.AccessDeniedPath = "/Home/AccesoDenegado";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<ITarjetaEstadoService, TarjetaEstadoService>();
builder.Services.AddSingleton<IEmbeddingService, HashingNgramEmbeddingService>();
builder.Services.AddScoped<IAddressRiskService, AddressRiskService>();
// Una cuenta desactivada pierde la sesión en ≤ 1 minuto
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

// Todo el sitio exige sesión; lo público se marca con [AllowAnonymous]
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AuthorizeFilter());
    o.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    o.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((v, c) => $"El valor '{v}' no es válido para {c}.");
    o.ModelBindingMessageProvider.SetValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
    o.ModelBindingMessageProvider.SetMissingBindRequiredValueAccessor(c => $"Falta el valor de {c}.");
});
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, AdaptadorValidacionEspanol>();
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

var app = builder.Build();
_ = app.Services.GetRequiredService<IConfiguracionOperativa>();

app.Logger.LogWarning("IA umbrales: medio={M} alto={A}",
    app.Configuration["IA:UmbralMedio"], app.Configuration["IA:UmbralAlto"]);

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

app.UseHttpsRedirection();

// El registro público queda cerrado: los usuarios los crea el Administrador (CU24)
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.Redirect("/Identity/Account/Login");
        return;
    }
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();