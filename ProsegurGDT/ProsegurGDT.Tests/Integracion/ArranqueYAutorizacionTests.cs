using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Tests.Integracion;

/// <summary>Arranque sobre una base vacía: migraciones, datos demo y reglas del modelo.</summary>
[Collection(ColeccionIntegracion.Nombre)]
public class ArranqueTests(FabricaWeb fabrica)
{
    [Fact]
    public async Task Las_migraciones_dejan_la_base_al_dia_y_el_modelo_no_tiene_cambios_sin_migrar()
    {
        var (pendientes, cambios) = await fabrica.ConBaseAsync(async (db, _) =>
            ((await db.Database.GetPendingMigrationsAsync()).ToList(), db.Database.HasPendingModelChanges()));

        Assert.Empty(pendientes);
        Assert.False(cambios, "El modelo cambió y falta una migración.");
    }

    [Fact]
    public async Task Existen_los_diez_roles_y_una_cuenta_demo_por_rol()
    {
        await fabrica.ConBaseAsync(async (db, sp) =>
        {
            var roles = await db.Roles.Select(r => r.Name).ToListAsync();
            Assert.Equal(Roles.Todos.Order(), roles.Order());

            var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var rol in Roles.Todos)
            {
                var u = await um.FindByEmailAsync(FabricaWeb.CorreoDemo(rol));
                Assert.True(u is not null, $"Falta la cuenta demo de {rol}");
                Assert.True(await um.IsInRoleAsync(u!, rol));
                Assert.True(u!.Activo);
            }
            return 0;
        });
    }

    [Fact]
    public async Task El_lote_demo_trae_29_tarjetas_6_hojas_de_ruta_y_4_direcciones_fraudulentas()
    {
        await fabrica.ConBaseAsync(async (db, _) =>
        {
            var lote = await db.Lotes.Include(l => l.Remito).SingleAsync(l => l.Remito!.Numero == "REM-DEMO-01");
            Assert.Equal(29, await db.Tarjetas.CountAsync(t => t.LoteId == lote.Id));
            Assert.Equal(6, await db.HojasRuta.CountAsync());
            Assert.Equal(4, await db.DireccionesFraudulentas.CountAsync(d => d.Origen == "Demo" && d.Estado == EstadoDireccion.Validada));
            Assert.Equal(12, await db.Zonas.CountAsync());
            Assert.Equal(6, await db.Repartidores.CountAsync());
            return 0;
        });
    }

    [Fact]
    public async Task El_repartidor_demo_esta_vinculado_a_Carlos_Mendoza()
    {
        var nombre = await fabrica.ConBaseAsync(async (db, sp) =>
        {
            var u = await sp.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("repartidor@gdt.local");
            return (await db.Repartidores.SingleAsync(r => r.UsuarioId == u!.Id)).Nombre;
        });
        Assert.Equal("Carlos Mendoza", nombre);
    }

    [Fact]
    public async Task Ninguna_relacion_del_dominio_borra_en_cascada()
    {
        var enCascada = await fabrica.ConBaseAsync((db, _) => Task.FromResult(
            db.Model.GetEntityTypes()
                .Where(e => e.ClrType.Namespace == typeof(Tarjeta).Namespace)
                .SelectMany(e => e.GetForeignKeys())
                .Where(fk => fk.DeleteBehavior != DeleteBehavior.Restrict)
                .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name} → {fk.PrincipalEntityType.ClrType.Name}")
                .ToList()));

        Assert.Empty(enCascada);
    }

    [Fact]
    public async Task Cada_tarjeta_demo_tiene_historial_y_su_ultimo_estado_coincide()
    {
        await fabrica.ConBaseAsync(async (db, _) =>
        {
            var tarjetas = await db.Tarjetas.AsNoTracking().Include(t => t.Historial).ToListAsync();
            Assert.NotEmpty(tarjetas);
            foreach (var t in tarjetas)
            {
                Assert.True(t.Historial.Count > 0, $"La tarjeta {t.Codigo} no tiene historial");
                Assert.Equal(t.Estado, t.Historial.OrderBy(h => h.Id).Last().EstadoNuevo);
            }
            return 0;
        });
    }
}

/// <summary>Autorización por rol de cada pantalla de la web (RNF de seguridad, CU24): quién entra y quién no.</summary>
[Collection(ColeccionIntegracion.Nombre)]
public class AutorizacionPorRolTests(FabricaWeb fabrica)
{
    private static readonly string[] RolesWeb = Roles.Todos.Where(r => r != Roles.Repartidor).ToArray();

    public static TheoryData<string, string[]> Pantallas => new()
    {
        { "/Lotes", [Roles.Supervisor] },
        { "/Lotes/Registrar", [Roles.Supervisor] },
        { "/Clientes", [Roles.Supervisor, Roles.AuxiliarTelefonista] },
        { "/Clientes/Importar", [Roles.Supervisor] },
        { "/Citas", [Roles.AuxiliarTelefonista] },
        { "/HojasRuta", [Roles.Supervisor, Roles.AuxiliarTelefonista] },
        { "/ReportesEntregas", [Roles.Supervisor] },
        { "/Fraude", [Roles.Verificador] },
        { "/Descargo", [Roles.Descargador] },
        { "/ReportesDescargo", [Roles.Descargador] },
        { "/Defraudacion", [Roles.Descargador, Roles.EncargadoDeBoveda] },
        { "/Defraudacion/Inventario", [Roles.EncargadoDeBoveda] },
        { "/VerificacionDescargo", [Roles.Buscador] },
        { "/Clasificacion", [Roles.Clasificador] },
        { "/Planillas", [Roles.Clasificador] },
        { "/Densidad", [Roles.Clasificador] },
        { "/Conciliacion", [Roles.Clasificador] },
        { "/DireccionesFraudulentas", [Roles.Buscador, Roles.Clasificador, Roles.AnalistaDeRutas] },
        { "/DireccionesFraudulentas/Crear", [Roles.Buscador] },
        { "/DireccionesFraudulentas/Alertas", [Roles.Buscador] },
        { "/Faltantes", [Roles.Buscador] },
        { "/Asignaciones", [Roles.AnalistaDeRutas] },
        { "/Usuarios", [Roles.Administrador] },
        { "/Usuarios/Crear", [Roles.Administrador] },
        { "/Auditoria", [Roles.Administrador] },
    };

    [Theory]
    [MemberData(nameof(Pantallas))]
    public async Task Cada_pantalla_solo_abre_a_sus_roles(string ruta, string[] permitidos)
    {
        foreach (var rol in RolesWeb)
        {
            var sesion = await fabrica.SesionDeAsync(rol);
            var r = await sesion.GetAsync(ruta);

            if (permitidos.Contains(rol))
                Assert.True(r.StatusCode == HttpStatusCode.OK, $"{rol} debería abrir {ruta} y recibió {(int)r.StatusCode}");
            else
            {
                Assert.True(r.StatusCode == HttpStatusCode.Redirect, $"{rol} no debería abrir {ruta} y recibió {(int)r.StatusCode}");
                Assert.Equal("/Home/AccesoDenegado", r.RutaDestino());
            }
        }
    }

    [Theory]
    [MemberData(nameof(Pantallas))]
    public async Task Sin_sesion_cada_pantalla_lleva_al_login(string ruta, string[] _)
    {
        var r = await fabrica.NuevaSesion().GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Identity/Account/Login", r.RutaDestino());
    }

    [Fact]
    public async Task Cada_rol_web_abre_su_panel_de_inicio()
    {
        foreach (var rol in RolesWeb)
        {
            var html = await (await fabrica.SesionDeAsync(rol)).HtmlAsync("/");
            Assert.Contains("pg-torre", html);
            Assert.Contains(FabricaWeb.CorreoDemo(rol), html);
        }
    }

    [Fact]
    public async Task El_menu_de_cada_rol_solo_enlaza_pantallas_que_ese_rol_puede_abrir()
    {
        foreach (var rol in RolesWeb)
        {
            var sesion = await fabrica.SesionDeAsync(rol);
            var html = await sesion.HtmlAsync("/");
            var menu = html[html.IndexOf("pg-menu", StringComparison.Ordinal)..html.IndexOf("pg-lateral-pie", StringComparison.Ordinal)];
            var enlaces = System.Text.RegularExpressions.Regex.Matches(menu, "href=\"(/[^\"]*)\"").Select(m => m.Groups[1].Value).Distinct().ToList();

            Assert.NotEmpty(enlaces);
            foreach (var enlace in enlaces)
            {
                var r = await sesion.GetAsync(enlace);
                Assert.True(r.StatusCode == HttpStatusCode.OK, $"El menú de {rol} enlaza {enlace}, que devuelve {(int)r.StatusCode}");
            }
        }
    }

    [Fact]
    public async Task Un_POST_sin_token_antiforgery_se_rechaza()
    {
        var sesion = await fabrica.SesionDeAsync(Roles.Supervisor);

        var r = await sesion.Http.PostAsync("/ReportesEntregas/Crear", new FormUrlEncodedContent([KeyValuePair.Create("fecha", "2026-11-16")]));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Un_rol_no_puede_ejecutar_el_POST_de_otro_rol()
    {
        // El Verificador abre una página suya para tener un token válido e intenta aprobar una hoja de ruta (Supervisor)
        var sesion = await fabrica.SesionDeAsync(Roles.Verificador);

        var r = await sesion.PostAsync("/Fraude", "/HojasRuta/Aprobar/1");

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Home/AccesoDenegado", r.RutaDestino());
        var estado = await fabrica.ConBaseAsync(async (db, _) => (await db.HojasRuta.AsNoTracking().SingleAsync(h => h.Id == 1)).Estado);
        Assert.NotEqual(EstadoHojaRuta.Firmada, estado);
    }
}

/// <summary>Inicio de sesión de la web (CU22): credenciales, bloqueo, cuenta desactivada y rechazo del Repartidor (RN-29).</summary>
[Collection(ColeccionIntegracion.Nombre)]
public class LoginWebTests(FabricaWeb fabrica)
{
    private async Task<string> CrearCuentaAsync(string correo, string rol, bool activa = true)
    {
        const string clave = "Temporal#2026x";
        await fabrica.ConBaseAsync(async (_, sp) =>
        {
            var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser { UserName = correo, Email = correo, EmailConfirmed = true, NombreCompleto = "Cuenta de prueba", Activo = activa };
            var r = await um.CreateAsync(u, clave);
            Assert.True(r.Succeeded, string.Join("; ", r.Errors.Select(e => e.Description)));
            await um.AddToRoleAsync(u, rol);
            return 0;
        });
        return clave;
    }

    [Fact]
    public async Task Con_credenciales_correctas_entra_y_recibe_la_cookie_de_sesion()
    {
        var sesion = fabrica.NuevaSesion();

        var r = await sesion.IniciarAsync("supervisor@gdt.local", FabricaWeb.ClaveDemo);

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/", r.RutaDestino());
        Assert.Contains(r.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await sesion.GetAsync("/Lotes")).StatusCode);
    }

    [Fact]
    public async Task Con_una_contrasena_incorrecta_no_entra()
    {
        var clave = await CrearCuentaAsync("clave.mala@gdt.local", Roles.Clasificador);
        var sesion = fabrica.NuevaSesion();

        var r = await sesion.IniciarAsync("clave.mala@gdt.local", clave + "x");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Usuario o contrase", await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await sesion.GetAsync("/Clasificacion")).StatusCode);
    }

    [Fact]
    public async Task Tras_cinco_intentos_fallidos_la_cuenta_se_bloquea_aunque_luego_la_clave_sea_correcta()
    {
        var clave = await CrearCuentaAsync("bloqueo@gdt.local", Roles.Clasificador);
        var sesion = fabrica.NuevaSesion();

        for (var i = 0; i < 5; i++) await sesion.IniciarAsync("bloqueo@gdt.local", "incorrecta-" + i);
        var r = await sesion.IniciarAsync("bloqueo@gdt.local", clave);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("bloqueada", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Una_cuenta_desactivada_no_entra()
    {
        var clave = await CrearCuentaAsync("inactiva@gdt.local", Roles.Buscador, activa: false);

        var r = await fabrica.NuevaSesion().IniciarAsync("inactiva@gdt.local", clave);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("desactivada", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_repartidor_no_entra_a_la_web_y_no_recibe_cookie()
    {
        var sesion = fabrica.NuevaSesion();

        var r = await sesion.IniciarAsync("repartidor@gdt.local", FabricaWeb.ClaveDemo);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("repartidor ingresan desde la aplicaci", await r.Content.ReadAsStringAsync());
        Assert.False(r.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith(".AspNetCore.Identity.Application=")));
        Assert.Equal(HttpStatusCode.Redirect, (await sesion.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task El_registro_publico_esta_cerrado()
    {
        var r = await fabrica.NuevaSesion().GetAsync("/Identity/Account/Register");

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Identity/Account/Login", r.RutaDestino());
    }

    [Fact]
    public async Task Al_cerrar_sesion_la_cookie_deja_de_valer()
    {
        var sesion = await fabrica.NuevaSesionDeAsync(Roles.AnalistaDeRutas);
        Assert.Equal(HttpStatusCode.OK, (await sesion.GetAsync("/Asignaciones")).StatusCode);

        var r = await sesion.PostAsync("/", "/Identity/Account/Logout");

        Assert.True((int)r.StatusCode is >= 200 and < 400);
        Assert.Equal(HttpStatusCode.Redirect, (await sesion.GetAsync("/Asignaciones")).StatusCode);
    }

    [Fact]
    public async Task Desactivar_una_cuenta_desde_Usuarios_le_impide_volver_a_entrar()
    {
        var clave = await CrearCuentaAsync("por.desactivar@gdt.local", Roles.Verificador);
        var id = await fabrica.ConBaseAsync(async (db, _) => (await db.Users.SingleAsync(u => u.Email == "por.desactivar@gdt.local")).Id);
        var admin = await fabrica.SesionDeAsync(Roles.Administrador);

        var r = await admin.PostAsync("/Usuarios", "/Usuarios/Desactivar", ("id", id));

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var intento = await fabrica.NuevaSesion().IniciarAsync("por.desactivar@gdt.local", clave);
        Assert.Contains("desactivada", await intento.Content.ReadAsStringAsync());
        var auditado = await fabrica.ConBaseAsync((db, _) => db.AuditLogs.AnyAsync(a => a.EntidadId == id && a.UsuarioNombre == FabricaWeb.AdminCorreo));
        Assert.True(auditado, "La desactivación debe quedar en la auditoría");
    }
}
