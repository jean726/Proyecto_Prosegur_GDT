using System.Text;
using ProsegurGDT.Tests.Apoyo;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Tests.Servicios;

/// <summary>Generación de la ficha de clientes desde el CSV de UNIBANCA (CU02): todo o nada.</summary>
public class ImportadorFichaServiceTests
{
    private const string Cabecera = "CodigoTarjeta;NombreCompleto;Documento;Telefono;Direccion\n";

    private static ImportadorFichaService Crear(ApplicationDbContext db) => new(db, [new AdaptadorCsvUnibanca()]);

    private static Task<ResultadoImportacion> Importar(ApplicationDbContext db, int loteId, string csv, string nombre = "ficha.csv") =>
        Crear(db).ImportarAsync(loteId, new MemoryStream(Encoding.UTF8.GetBytes(csv)), nombre, BaseEnMemoria.UsuarioPrueba);

    private static string Filas(int n, string prefijo = "TJ") =>
        Cabecera + string.Concat(Enumerable.Range(1, n).Select(i => $"{prefijo}-{i:000};Cliente {i};4000000{i};99900000{i};Av. Larco {100 + i}, Miraflores\n"));

    [Fact]
    public async Task Importa_las_tarjetas_con_su_cliente_estado_inicial_e_historial()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote(cantidad: 3);

        var r = await Importar(db, lote.Id, Filas(3));

        Assert.True(r.Ok, string.Join("; ", r.Errores));
        Assert.Equal(3, r.Cantidad);
        Assert.Equal("HC-PRUEBA-00001", r.CodigoHoja);
        Assert.Equal(3, db.Tarjetas.Count());
        Assert.Equal(3, db.Clientes.Count());
        Assert.All(db.Tarjetas, t => Assert.Equal(EstadoTarjeta.IngresadaEnCustodia, t.Estado));
        Assert.Equal(3, db.HistorialEstados.Count(h => h.EstadoAnterior == null && h.EstadoNuevo == EstadoTarjeta.IngresadaEnCustodia && h.UsuarioId == BaseEnMemoria.UsuarioPrueba));
        Assert.Equal("AVENIDA LARCO 101 MIRAFLORES", db.Clientes.Single(c => c.DocumentoIdentidad == "40000001").DireccionNormalizada);
    }

    [Fact]
    public async Task Dos_tarjetas_del_mismo_documento_comparten_cliente()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote(cantidad: 2);

        var r = await Importar(db, lote.Id, Cabecera + "TJ-001;Ana Torres;45871236;;Av. Larco 123\nTJ-002;Ana Torres;45871236;;Av. Larco 123\n");

        Assert.True(r.Ok);
        Assert.Single(db.Clientes);
        Assert.Equal(2, db.Tarjetas.Count());
    }

    [Fact]
    public async Task Reutiliza_el_cliente_que_ya_existe_con_ese_documento()
    {
        using var db = BaseEnMemoria.Crear();
        var previo = db.AgregarTarjeta(db.AgregarLote(1, "REM-ANTERIOR"), "VIEJA-1", "Jr. Unión 450", documento: "45871236", nombre: "Ana Torres");
        var lote = db.AgregarLote(cantidad: 1, numeroRemito: "REM-NUEVO");

        var r = await Importar(db, lote.Id, Cabecera + "TJ-001;Ana Torres;45871236;;Av. Larco 123\n");

        Assert.True(r.Ok);
        Assert.Single(db.Clientes);
        Assert.Equal(previo.ClienteId, db.Tarjetas.Single(t => t.Codigo == "TJ-001").ClienteId);
    }

    [Fact]
    public async Task Rechaza_si_la_cantidad_no_coincide_con_el_conteo_del_lote_y_no_guarda_nada()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote(cantidad: 5);

        var r = await Importar(db, lote.Id, Filas(3));

        Assert.False(r.Ok);
        Assert.Contains(r.Errores, e => e.Contains("3 registros") && e.Contains("5 tarjetas"));
        Assert.Empty(db.Tarjetas);
        Assert.Empty(db.Clientes);
    }

    [Fact]
    public async Task Rechaza_codigos_que_ya_estan_en_el_sistema()
    {
        using var db = BaseEnMemoria.Crear();
        db.AgregarTarjeta(db.AgregarLote(1, "REM-ANTERIOR"), "TJ-002", "Jr. Unión 450");
        var lote = db.AgregarLote(cantidad: 3, numeroRemito: "REM-NUEVO");

        var r = await Importar(db, lote.Id, Filas(3));

        Assert.False(r.Ok);
        Assert.Contains(r.Errores, e => e.Contains("ya registrados") && e.Contains("TJ-002"));
        Assert.Single(db.Tarjetas);
    }

    [Fact]
    public async Task Rechaza_un_lote_que_ya_tiene_ficha()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote(cantidad: 3);
        Assert.True((await Importar(db, lote.Id, Filas(3))).Ok);

        var r = await Importar(db, lote.Id, Filas(3, "OTRA"));

        Assert.False(r.Ok);
        Assert.Contains("aún no tenga ficha", Assert.Single(r.Errores));
        Assert.Equal(3, db.Tarjetas.Count());
    }

    [Fact]
    public async Task Rechaza_un_lote_inexistente_o_con_discrepancia()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote(cantidad: 3);
        lote.Estado = EstadoLote.DiscrepanciaDetectada;
        db.SaveChanges();

        Assert.False((await Importar(db, lote.Id, Filas(3))).Ok);
        Assert.False((await Importar(db, 9999, Filas(3))).Ok);
        Assert.Empty(db.Tarjetas);
    }

    [Fact]
    public async Task Rechaza_un_archivo_que_no_es_csv()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote(cantidad: 3);

        var r = await Importar(db, lote.Id, Filas(3), "ficha.xlsx");

        Assert.False(r.Ok);
        Assert.Contains("Formato de archivo no soportado", Assert.Single(r.Errores));
    }

    [Fact]
    public async Task Con_muchos_errores_muestra_los_primeros_25_y_resume_el_resto()
    {
        using var db = BaseEnMemoria.Crear();
        var lote = db.AgregarLote(cantidad: 40);
        var csv = Cabecera + string.Concat(Enumerable.Range(1, 40).Select(i => $"TJ-{i:000};;;;\n"));   // sin nombre, documento ni dirección

        var r = await Importar(db, lote.Id, csv);

        Assert.False(r.Ok);
        Assert.Equal(26, r.Errores.Count);
        Assert.Matches(@"y \d+ errores más", r.Errores[^1]);
        Assert.Empty(db.Tarjetas);
    }
}
