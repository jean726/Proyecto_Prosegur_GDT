using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Banco> Bancos => Set<Banco>();
    public DbSet<Remito> Remitos => Set<Remito>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Tarjeta> Tarjetas => Set<Tarjeta>();
    public DbSet<HistorialEstadoTarjeta> HistorialEstados => Set<HistorialEstadoTarjeta>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Remito>().HasIndex(x => x.Numero).IsUnique();
        b.Entity<Tarjeta>().HasIndex(x => x.Codigo).IsUnique();

        b.Entity<Lote>().Property(x => x.Estado).HasConversion<string>().HasMaxLength(40);
        b.Entity<Tarjeta>().Property(x => x.Estado).HasConversion<string>().HasMaxLength(40);
        b.Entity<HistorialEstadoTarjeta>().Property(x => x.EstadoNuevo).HasConversion<string>().HasMaxLength(40);
        b.Entity<HistorialEstadoTarjeta>().Property(x => x.EstadoAnterior).HasConversion<string>().HasMaxLength(40);

        // Cadena de custodia: nada se borra en cascada (evita además errores de "multiple cascade paths")
        foreach (var fk in b.Model.GetEntityTypes()
                     .Where(e => e.ClrType.Namespace == typeof(Banco).Namespace)
                     .SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
}