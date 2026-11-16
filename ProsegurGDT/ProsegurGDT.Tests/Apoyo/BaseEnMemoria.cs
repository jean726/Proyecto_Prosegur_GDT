using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Tests.Apoyo;

/// <summary>Contexto de EF en memoria para probar servicios sin SQL Server. Cada llamada crea una base nueva.</summary>
public static class BaseEnMemoria
{
    public const string UsuarioPrueba = "usuario-prueba";

    public static ApplicationDbContext Crear() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("gdt-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>Lote ingresado en custodia con su remito, listo para recibir tarjetas.</summary>
    public static Lote AgregarLote(this ApplicationDbContext db, int cantidad = 3, string numeroRemito = "REM-PRUEBA")
    {
        var banco = new Banco { Nombre = "UNIBANCA" };
        var remito = new Remito { Numero = numeroRemito, Banco = banco, CantidadDeclarada = cantidad, FechaEmision = new DateOnly(2026, 11, 16) };
        var lote = new Lote
        {
            Remito = remito, FechaRecepcion = new DateOnly(2026, 11, 16), CantidadDeclarada = cantidad, CantidadContada = cantidad,
            Estado = EstadoLote.IngresadoEnCustodia, CodigoHojaControl = "HC-PRUEBA-00001",
            RegistradoPorId = UsuarioPrueba, FechaHoraRegistro = DateTime.UtcNow
        };
        db.Lotes.Add(lote);
        db.SaveChanges();
        return lote;
    }

    public static Tarjeta AgregarTarjeta(this ApplicationDbContext db, Lote lote, string codigo, string direccion,
        EstadoTarjeta estado = EstadoTarjeta.IngresadaEnCustodia, string? documento = null, string nombre = "Titular De Prueba")
    {
        var cliente = new Cliente
        {
            NombreCompleto = nombre, DocumentoIdentidad = documento ?? ("DOC" + codigo), Direccion = direccion,
            DireccionNormalizada = ProsegurGDT.Web.Services.NormalizadorDirecciones.Normalizar(direccion)
        };
        var tarjeta = new Tarjeta { Codigo = codigo, Lote = lote, Cliente = cliente, Estado = estado, FechaActualizacion = DateTime.UtcNow };
        db.Tarjetas.Add(tarjeta);
        db.SaveChanges();
        return tarjeta;
    }
}
