using System.Text;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Services;

public record FilaFicha(int Linea, string Codigo, string Nombre, string Documento, string Telefono, string Direccion);
public record LecturaFicha(List<FilaFicha> Filas, List<string> Errores);
public record ResultadoImportacion(bool Ok, List<string> Errores, int Cantidad = 0, string? CodigoHoja = null);

// Adapter: traduce un formato externo (CSV de UNIBANCA) al modelo interno.
public interface IAdaptadorFichaClientes
{
    bool Soporta(string nombreArchivo);
    Task<LecturaFicha> LeerAsync(Stream archivo);
}

public class AdaptadorCsvUnibanca : IAdaptadorFichaClientes
{
    public bool Soporta(string nombreArchivo) => nombreArchivo.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<LecturaFicha> LeerAsync(Stream archivo)
    {
        var lineas = new List<string>();
        using (var sr = new StreamReader(archivo, Encoding.UTF8, true))
        {
            string? linea;
            while ((linea = await sr.ReadLineAsync()) != null)
                if (!string.IsNullOrWhiteSpace(linea)) lineas.Add(linea);
        }
        if (lineas.Count < 2) return new(new(), new() { "El archivo no contiene registros." });

        var sep = lineas[0].Count(c => c == ';') > lineas[0].Count(c => c == ',') ? ';' : ',';
        var cab = ParseLinea(lineas[0], sep).Select(c => c.Trim().ToLowerInvariant()).ToList();
        string[] req = { "codigotarjeta", "nombrecompleto", "documento", "telefono", "direccion" };
        var faltan = req.Where(r => !cab.Contains(r)).ToList();
        if (faltan.Count > 0)
            return new(new(), new() { "Faltan columnas: " + string.Join(", ", faltan) + ". Cabecera esperada: CodigoTarjeta;NombreCompleto;Documento;Telefono;Direccion" });

        int iCod = cab.IndexOf("codigotarjeta"), iNom = cab.IndexOf("nombrecompleto"), iDoc = cab.IndexOf("documento"),
            iTel = cab.IndexOf("telefono"), iDir = cab.IndexOf("direccion");

        var filas = new List<FilaFicha>();
        var errores = new List<string>();
        var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lineas.Count; i++)
        {
            var campos = ParseLinea(lineas[i], sep);
            string G(int idx) => idx < campos.Count ? campos[idx].Trim() : "";
            var f = new FilaFicha(i + 1, G(iCod), G(iNom), G(iDoc), G(iTel), G(iDir));
            if (f.Codigo.Length == 0 || f.Codigo.Length > 30) errores.Add($"Línea {f.Linea}: código de tarjeta vacío o mayor a 30 caracteres.");
            else if (!codigos.Add(f.Codigo)) errores.Add($"Línea {f.Linea}: código de tarjeta duplicado ({f.Codigo}).");
            if (f.Nombre.Length == 0 || f.Nombre.Length > 150) errores.Add($"Línea {f.Linea}: nombre vacío o mayor a 150 caracteres.");
            if (f.Documento.Length == 0 || f.Documento.Length > 20) errores.Add($"Línea {f.Linea}: documento vacío o mayor a 20 caracteres.");
            if (f.Direccion.Length == 0 || f.Direccion.Length > 250) errores.Add($"Línea {f.Linea}: dirección vacía o mayor a 250 caracteres.");
            if (f.Telefono.Length > 20) errores.Add($"Línea {f.Linea}: teléfono mayor a 20 caracteres.");
            filas.Add(f);
        }
        return new(filas, errores);
    }

    private static List<string> ParseLinea(string linea, char sep)
    {
        var campos = new List<string>();
        var sb = new StringBuilder();
        var comillas = false;
        for (int i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (comillas)
            {
                if (c == '"') { if (i + 1 < linea.Length && linea[i + 1] == '"') { sb.Append('"'); i++; } else comillas = false; }
                else sb.Append(c);
            }
            else if (c == '"') comillas = true;
            else if (c == sep) { campos.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        campos.Add(sb.ToString().Trim());
        return campos;
    }
}

public interface IImportadorFichaService
{
    Task<ResultadoImportacion> ImportarAsync(int loteId, Stream archivo, string nombreArchivo, string usuarioId);
}

// Regla de negocio de CU02, separada del controlador y del formato del archivo.
public class ImportadorFichaService : IImportadorFichaService
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IAdaptadorFichaClientes> _adaptadores;

    public ImportadorFichaService(ApplicationDbContext db, IEnumerable<IAdaptadorFichaClientes> adaptadores)
    { _db = db; _adaptadores = adaptadores; }

    public async Task<ResultadoImportacion> ImportarAsync(int loteId, Stream archivo, string nombreArchivo, string usuarioId)
    {
        var errores = new List<string>();

        var lote = await _db.Lotes.FirstOrDefaultAsync(l => l.Id == loteId && l.Estado == EstadoLote.IngresadoEnCustodia);
        if (lote is null || await _db.Tarjetas.AnyAsync(t => t.LoteId == loteId))
            errores.Add("Seleccione un lote ingresado en custodia que aún no tenga ficha de clientes.");

        var adaptador = _adaptadores.FirstOrDefault(a => a.Soporta(nombreArchivo));
        if (adaptador is null) errores.Add("Formato de archivo no soportado. Use un archivo .csv.");
        if (errores.Count > 0) return new(false, errores);

        var lectura = await adaptador!.LeerAsync(archivo);
        if (lectura.Filas.Count == 0) return new(false, lectura.Errores);
        errores.AddRange(lectura.Errores);
        var filas = lectura.Filas;

        if (filas.Count != lote!.CantidadContada)
            errores.Add($"El archivo tiene {filas.Count} registros y el lote tiene {lote.CantidadContada} tarjetas contadas. Deben coincidir.");

        var codigos = filas.Select(f => f.Codigo).ToList();
        var yaExisten = await _db.Tarjetas.Where(t => codigos.Contains(t.Codigo)).Select(t => t.Codigo).Take(5).ToListAsync();
        if (yaExisten.Count > 0) errores.Add("Códigos ya registrados en el sistema: " + string.Join(", ", yaExisten));

        if (errores.Count > 0)
        {
            var visibles = errores.Take(25).ToList();
            if (errores.Count > 25) visibles.Add($"… y {errores.Count - 25} errores más.");
            return new(false, visibles);
        }

        var docs = filas.Select(f => f.Documento).Distinct().ToList();
        var clientes = await _db.Clientes.Where(c => docs.Contains(c.DocumentoIdentidad)).ToDictionaryAsync(c => c.DocumentoIdentidad);
        var ahora = DateTime.UtcNow;

        foreach (var f in filas)
        {
            if (!clientes.TryGetValue(f.Documento, out var cli))
            {
                cli = new Cliente
                {
                    NombreCompleto = f.Nombre,
                    DocumentoIdentidad = f.Documento,
                    Telefono = f.Telefono.Length == 0 ? null : f.Telefono,
                    Direccion = f.Direccion,
                    DireccionNormalizada = NormalizadorDirecciones.Normalizar(f.Direccion)
                };
                clientes[f.Documento] = cli;
                _db.Clientes.Add(cli);
            }
            var tj = new Tarjeta { Codigo = f.Codigo, Lote = lote, Cliente = cli, Estado = EstadoTarjeta.IngresadaEnCustodia, FechaActualizacion = ahora };
            _db.Tarjetas.Add(tj);
            _db.HistorialEstados.Add(new HistorialEstadoTarjeta
            {
                Tarjeta = tj,
                EstadoAnterior = null,
                EstadoNuevo = EstadoTarjeta.IngresadaEnCustodia,
                UsuarioId = usuarioId,
                FechaHora = ahora,
                Observacion = "Ficha de clientes generada"
            });
        }
        await _db.SaveChangesAsync();   // todo o nada
        return new(true, new(), filas.Count, lote.CodigoHojaControl);
    }
}