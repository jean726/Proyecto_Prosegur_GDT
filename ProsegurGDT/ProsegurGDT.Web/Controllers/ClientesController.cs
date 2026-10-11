using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers;

[Authorize(Roles = Roles.Supervisor + "," + Roles.AuxiliarTelefonista)]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _audit;
    public ClientesController(ApplicationDbContext db, IAuditoriaService audit) { _db = db; _audit = audit; }

    public async Task<IActionResult> Index(string? q, EstadoTarjeta? estado)
    {
        var query = _db.Tarjetas.Include(t => t.Cliente).Include(t => t.Lote).ThenInclude(l => l!.Remito).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(t => t.Codigo.Contains(q) || t.Cliente!.NombreCompleto.Contains(q) || t.Cliente!.DocumentoIdentidad.Contains(q));
        }
        if (estado.HasValue) query = query.Where(t => t.Estado == estado.Value);
        ViewBag.Q = q;
        return View(await query.OrderByDescending(t => t.Id).Take(200).ToListAsync());
    }

    // ---------- Corregir datos de la ficha (solo Supervisor) ----------
    [Authorize(Roles = Roles.Supervisor)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var c = await _db.Clientes.FindAsync(id);
        if (c is null) return NotFound();
        return View(new ClienteEditarVM { Id = c.Id, NombreCompleto = c.NombreCompleto, Telefono = c.Telefono, Direccion = c.Direccion });
    }

    [Authorize(Roles = Roles.Supervisor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(ClienteEditarVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var c = await _db.Clientes.FindAsync(vm.Id);
        if (c is null) return NotFound();
        c.NombreCompleto = vm.NombreCompleto.Trim();
        c.Telefono = string.IsNullOrWhiteSpace(vm.Telefono) ? null : vm.Telefono.Trim();
        c.Direccion = vm.Direccion.Trim();
        c.DireccionNormalizada = NormalizadorDirecciones.Normalizar(c.Direccion);
        await _db.SaveChangesAsync();
        await _audit.RegistrarAsync("ModificarFichaCliente", "Cliente", c.Id.ToString(), c.DocumentoIdentidad);
        TempData["Ok"] = "Ficha del cliente actualizada.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Generar ficha desde CSV de UNIBANCA (solo Supervisor) ----------
    [Authorize(Roles = Roles.Supervisor)]
    [HttpGet]
    public async Task<IActionResult> Importar()
    {
        var vm = new ImportarFichaVM();
        await CargarLotes(vm);
        return View(vm);
    }

    [Authorize(Roles = Roles.Supervisor)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Importar(ImportarFichaVM vm)
    {
        ModelState.Clear();
        await CargarLotes(vm);
        var errores = new List<string>();

        var lote = await _db.Lotes.FirstOrDefaultAsync(l => l.Id == vm.LoteId && l.Estado == EstadoLote.IngresadoEnCustodia);
        if (lote is null || await _db.Tarjetas.AnyAsync(t => t.LoteId == vm.LoteId))
            errores.Add("Seleccione un lote ingresado en custodia que aún no tenga ficha de clientes.");
        if (vm.Archivo is null || vm.Archivo.Length == 0)
            errores.Add("Seleccione el archivo CSV remitido por UNIBANCA.");
        else if (vm.Archivo.Length > 2_000_000 || !vm.Archivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            errores.Add("El archivo debe ser .csv y pesar menos de 2 MB.");
        if (errores.Count > 0) { vm.Errores = errores; return View(vm); }

        var lineas = new List<string>();
        using (var sr = new StreamReader(vm.Archivo!.OpenReadStream(), Encoding.UTF8, true))
        {
            string? linea;
            while ((linea = await sr.ReadLineAsync()) != null)
                if (!string.IsNullOrWhiteSpace(linea)) lineas.Add(linea);
        }
        if (lineas.Count < 2) { vm.Errores = new() { "El archivo no contiene registros." }; return View(vm); }

        var sep = lineas[0].Count(c => c == ';') > lineas[0].Count(c => c == ',') ? ';' : ',';
        var cab = ParseLinea(lineas[0], sep).Select(c => c.Trim().ToLowerInvariant()).ToList();
        string[] req = { "codigotarjeta", "nombrecompleto", "documento", "telefono", "direccion" };
        var faltan = req.Where(r => !cab.Contains(r)).ToList();
        if (faltan.Count > 0)
        {
            vm.Errores = new() { "Faltan columnas: " + string.Join(", ", faltan) + ". Cabecera esperada: CodigoTarjeta;NombreCompleto;Documento;Telefono;Direccion" };
            return View(vm);
        }
        int iCod = cab.IndexOf("codigotarjeta"), iNom = cab.IndexOf("nombrecompleto"), iDoc = cab.IndexOf("documento"),
            iTel = cab.IndexOf("telefono"), iDir = cab.IndexOf("direccion");

        var filas = new List<(string Cod, string Nom, string Doc, string Tel, string Dir)>();
        var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lineas.Count; i++)
        {
            var campos = ParseLinea(lineas[i], sep);
            string G(int idx) => idx < campos.Count ? campos[idx].Trim() : "";
            var f = (Cod: G(iCod), Nom: G(iNom), Doc: G(iDoc), Tel: G(iTel), Dir: G(iDir));
            var n = i + 1;
            if (f.Cod.Length == 0 || f.Cod.Length > 30) errores.Add($"Línea {n}: código de tarjeta vacío o mayor a 30 caracteres.");
            else if (!codigos.Add(f.Cod)) errores.Add($"Línea {n}: código de tarjeta duplicado ({f.Cod}).");
            if (f.Nom.Length == 0 || f.Nom.Length > 150) errores.Add($"Línea {n}: nombre vacío o mayor a 150 caracteres.");
            if (f.Doc.Length == 0 || f.Doc.Length > 20) errores.Add($"Línea {n}: documento vacío o mayor a 20 caracteres.");
            if (f.Dir.Length == 0 || f.Dir.Length > 250) errores.Add($"Línea {n}: dirección vacía o mayor a 250 caracteres.");
            if (f.Tel.Length > 20) errores.Add($"Línea {n}: teléfono mayor a 20 caracteres.");
            filas.Add(f);
        }

        if (filas.Count != lote!.CantidadContada)
            errores.Add($"El archivo tiene {filas.Count} registros y el lote tiene {lote.CantidadContada} tarjetas contadas. Deben coincidir.");
        var listaCod = codigos.ToList();
        var yaExisten = await _db.Tarjetas.Where(t => listaCod.Contains(t.Codigo)).Select(t => t.Codigo).Take(5).ToListAsync();
        if (yaExisten.Count > 0) errores.Add("Códigos ya registrados en el sistema: " + string.Join(", ", yaExisten));

        if (errores.Count > 0)
        {
            vm.Errores = errores.Take(25).ToList();
            if (errores.Count > 25) vm.Errores.Add($"… y {errores.Count - 25} errores más.");
            return View(vm);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var docs = filas.Select(f => f.Doc).Distinct().ToList();
        var clientes = await _db.Clientes.Where(c => docs.Contains(c.DocumentoIdentidad)).ToDictionaryAsync(c => c.DocumentoIdentidad);
        var ahora = DateTime.UtcNow;

        foreach (var f in filas)
        {
            if (!clientes.TryGetValue(f.Doc, out var cli))
            {
                cli = new Cliente
                {
                    NombreCompleto = f.Nom,
                    DocumentoIdentidad = f.Doc,
                    Telefono = f.Tel.Length == 0 ? null : f.Tel,
                    Direccion = f.Dir,
                    DireccionNormalizada = NormalizadorDirecciones.Normalizar(f.Dir)
                };
                clientes[f.Doc] = cli;
                _db.Clientes.Add(cli);
            }
            var tj = new Tarjeta { Codigo = f.Cod, Lote = lote, Cliente = cli, Estado = EstadoTarjeta.IngresadaEnCustodia, FechaActualizacion = ahora };
            _db.Tarjetas.Add(tj);
            _db.HistorialEstados.Add(new HistorialEstadoTarjeta
            {
                Tarjeta = tj,
                EstadoAnterior = null,
                EstadoNuevo = EstadoTarjeta.IngresadaEnCustodia,
                UsuarioId = userId,
                FechaHora = ahora,
                Observacion = "Ficha de clientes generada"
            });
        }
        await _db.SaveChangesAsync();   // todo o nada
        await _audit.RegistrarAsync("GenerarFichaClientes", "Lote", lote.Id.ToString(), $"{filas.Count} tarjetas");
        TempData["Ok"] = $"Ficha de clientes generada: {filas.Count} tarjetas registradas para el lote {lote.CodigoHojaControl}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarLotes(ImportarFichaVM vm)
    {
        var lotes = await _db.Lotes.Include(l => l.Remito)
            .Where(l => l.Estado == EstadoLote.IngresadoEnCustodia && !_db.Tarjetas.Any(t => t.LoteId == l.Id))
            .OrderByDescending(l => l.Id).ToListAsync();
        vm.Lotes = lotes.Select(l => new SelectListItem($"{l.CodigoHojaControl} — {l.Remito?.Numero} — {l.CantidadContada} tarjetas", l.Id.ToString())).ToList();
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