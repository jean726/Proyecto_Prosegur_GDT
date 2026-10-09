using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Domain;

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

        if (!await db.Bancos.AnyAsync())
        {
            db.Bancos.Add(new Banco { Nombre = "UNIBANCA" });
            await db.SaveChangesAsync();
        }

        if (cargarDemo && !await db.Remitos.AnyAsync())
        {
            var banco = await db.Bancos.FirstAsync();
            db.Remitos.AddRange(
                new Remito { Numero = "REM-0001", BancoId = banco.Id, CantidadDeclarada = 50, FechaEmision = DateOnly.FromDateTime(DateTime.Today) },
                new Remito { Numero = "REM-0002", BancoId = banco.Id, CantidadDeclarada = 30, FechaEmision = DateOnly.FromDateTime(DateTime.Today) });
            await db.SaveChangesAsync();
        }
    }
}