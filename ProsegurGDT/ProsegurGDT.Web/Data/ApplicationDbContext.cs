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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RegistroLlamada> Llamadas => Set<RegistroLlamada>();
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<DireccionFraudulenta> DireccionesFraudulentas => Set<DireccionFraudulenta>();
    public DbSet<AlertaDireccion> Alertas => Set<AlertaDireccion>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Remito>().HasIndex(x => x.Numero).IsUnique();
        b.Entity<Tarjeta>().HasIndex(x => x.Codigo).IsUnique();

        b.Entity<Lote>().Property(x => x.Estado).HasConversion<string>().HasMaxLength(40);
        b.Entity<Tarjeta>().Property(x => x.Estado).HasConversion<string>().HasMaxLength(40);
        b.Entity<HistorialEstadoTarjeta>().Property(x => x.EstadoNuevo).HasConversion<string>().HasMaxLength(40);
        b.Entity<HistorialEstadoTarjeta>().Property(x => x.EstadoAnterior).HasConversion<string>().HasMaxLength(40);

        b.Entity<RegistroLlamada>().Property(x => x.Resultado).HasConversion<string>().HasMaxLength(40);
        b.Entity<Cliente>().HasIndex(x => x.DocumentoIdentidad).IsUnique();

        b.Entity<Zona>().HasIndex(x => x.Nombre).IsUnique();
        b.Entity<DireccionFraudulenta>().Property(x => x.Estado).HasConversion<string>().HasMaxLength(40);
        b.Entity<DireccionFraudulenta>().HasIndex(x => x.DireccionNormalizada);
        b.Entity<AlertaDireccion>().Property(x => x.Nivel).HasConversion<string>().HasMaxLength(40);
        b.Entity<AlertaDireccion>().Property(x => x.Decision).HasConversion<string>().HasMaxLength(40);
        b.Entity<AlertaDireccion>().HasIndex(x => new { x.TarjetaId, x.DireccionFraudulentaId });

        // Cadena de custodia: nada se borra en cascada (evita además errores de "multiple cascade paths")
        foreach (var fk in b.Model.GetEntityTypes()
                     .Where(e => e.ClrType.Namespace == typeof(Banco).Namespace)
                     .SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}