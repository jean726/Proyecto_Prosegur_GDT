using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, bool cargarDemo)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in Roles.Todos)
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));

        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByEmailAsync(email) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    NombreCompleto = "Administrador del Sistema",
                    Activo = true
                };
                var result = await userManager.CreateAsync(admin, password);
                if (result.Succeeded) await userManager.AddToRoleAsync(admin, Roles.Administrador);
            }
        }

        if (cargarDemo) await CrearUsuariosDemoAsync(sp);

        if (!await db.Bancos.AnyAsync())
        {
            db.Bancos.Add(new Banco { Nombre = "UNIBANCA" });
            await db.SaveChangesAsync();
        }

        if (cargarDemo && !await db.Remitos.AnyAsync())
        {
            var banco = await db.Bancos.FirstAsync();
            db.Remitos.AddRange(
                new Remito { Numero = "REM-0001", BancoId = banco.Id, CantidadDeclarada = 50, FechaEmision = DateOnly.FromDateTime(HoraLima.Ahora) },
                new Remito { Numero = "REM-0002", BancoId = banco.Id, CantidadDeclarada = 30, FechaEmision = DateOnly.FromDateTime(HoraLima.Ahora) });
            await db.SaveChangesAsync();
        }

        if (!await db.Zonas.AnyAsync())
        {
            var nombres = new[] { "Miraflores", "San Isidro", "San Borja", "Surquillo", "Barranco", "Santiago de Surco",
                "Lince", "Jesús María", "Magdalena del Mar", "San Miguel", "La Molina", "Lima Cercado" };
            db.Zonas.AddRange(nombres.Select(n => new Zona { Nombre = n }));
            await db.SaveChangesAsync();
        }

        if (cargarDemo && !await db.Repartidores.AnyAsync())
        {
            var zonas = await db.Zonas.ToDictionaryAsync(z => z.Nombre, z => z.Id);
            var repartidores = new (string Nombre, TipoRepartidor Tipo, string Zona, int Capacidad)[]
            {
                ("Carlos Mendoza", TipoRepartidor.Motorizado, "Miraflores", 25),
                ("Luis Ramírez", TipoRepartidor.Motorizado, "San Isidro", 25),
                ("Jorge Salazar", TipoRepartidor.Motorizado, "Santiago de Surco", 30),
                ("Ana Torres", TipoRepartidor.Caminante, "San Borja", 15),
                ("Miguel Castro", TipoRepartidor.Caminante, "Barranco", 12),
                ("Rosa Quispe", TipoRepartidor.Caminante, "Lince", 15),
            };

            foreach (var r in repartidores)
                db.Repartidores.Add(new Repartidor
                {
                    Nombre = r.Nombre,
                    Tipo = r.Tipo,
                    ZonaHabitualId = zonas.TryGetValue(r.Zona, out var zonaId) ? zonaId : null,
                    CapacidadTarjetas = r.Capacidad,
                    Activo = true
                });

            await db.SaveChangesAsync();
        }

        if (cargarDemo && !await db.InventarioBoveda.AnyAsync())
        {
            var tarjetasEnBoveda = await db.Tarjetas
                .Where(t => t.Estado == EstadoTarjeta.EnCustodia
                         || t.Estado == EstadoTarjeta.IngresadaEnCustodia
                         || t.Estado == EstadoTarjeta.NoClasificable
                         || t.Estado == EstadoTarjeta.ApartadaPorFraude)
                .OrderBy(t => t.Id)
                .Take(12)
                .ToListAsync();

            var ubicaciones = new[] { "Bóveda - Estante A-1", "Bóveda - Estante A-2", "Bóveda - Caja fuerte 1" };
            for (var i = 0; i < tarjetasEnBoveda.Count; i++)
                db.InventarioBoveda.Add(new InventarioBoveda
                {
                    TarjetaId = tarjetasEnBoveda[i].Id,
                    Ubicacion = ubicaciones[i % ubicaciones.Length],
                    EstadoFisico = tarjetasEnBoveda[i].Estado == EstadoTarjeta.ApartadaPorFraude
                        ? "Apartada por fraude"
                        : "En custodia",
                    FechaActualizacion = DateTime.UtcNow
                });

            await db.SaveChangesAsync();
        }

        if (cargarDemo && !await db.DireccionesFraudulentas.AnyAsync() && !string.IsNullOrWhiteSpace(email))
        {
            var adminDemo = await sp.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
            if (adminDemo is not null)
            {
                var riesgo = sp.GetRequiredService<IAddressRiskService>();
                const string motivo = "Domicilio ficticio reportado en jornada anterior (dato de demostración)";
                await riesgo.AgregarAsync("Avda. Larco 105, Mirafores", motivo, adminDemo.Id, true, "Demo");           // variante con error de tipeo
                await riesgo.AgregarAsync("Av. Benavides 108, Miraflores", motivo, adminDemo.Id, true, "Demo");        // coincide exacta
                await riesgo.AgregarAsync("Jirón Unión 111, Miraflores", motivo, adminDemo.Id, true, "Demo");          // palabras omitidas
                await riesgo.AgregarAsync("Calle Berlin 117, Miraflores", motivo, adminDemo.Id, true, "Demo");         // numeración distinta
            }
        }

        if (cargarDemo)
            await DbSeederLoteDemo.SembrarAsync(sp, sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder"));
    }

    // Nombre legible del rol: «AuxiliarTelefonista» → «Auxiliar Telefonista», «EncargadoDeBoveda» → «Encargado de Bóveda»
    private static string Texto(this string rol) =>
        System.Text.RegularExpressions.Regex.Replace(rol, "(?<!^)([A-Z])", " $1").Replace(" De ", " de ").Replace("Boveda", "Bóveda");

    private static async Task CrearUsuariosDemoAsync(IServiceProvider sp)
    {
        var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var rol in Roles.Todos.Where(r => r != Roles.Administrador))
        {
            var email = $"{rol.ToLowerInvariant()}@gdt.local";
            var existente = await um.FindByEmailAsync(email);
            if (existente is not null)
            {
                // Las cuentas creadas antes se llamaban «Demo {rol}» (o «Demo - {rol}»): se deja solo el nombre del rol
                if (existente.NombreCompleto.StartsWith("Demo", StringComparison.OrdinalIgnoreCase))
                {
                    existente.NombreCompleto = rol.Texto();
                    await um.UpdateAsync(existente);
                }
                continue;
            }
            var u = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                NombreCompleto = rol.Texto(),
                Activo = true
            };
            var r = await um.CreateAsync(u, "<clave-demo>");
            if (r.Succeeded) await um.AddToRoleAsync(u, rol);
        }
    }
}