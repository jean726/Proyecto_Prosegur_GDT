using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Controllers;

[Authorize(Roles = Roles.Administrador)]
public class AuditoriaController : Controller
{
    private readonly ApplicationDbContext _db;
    public AuditoriaController(ApplicationDbContext db) => _db = db;

    private const int PorPagina = 100;

    public async Task<IActionResult> Index(string? q, int pagina = 1)
    {
        var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(a => a.UsuarioNombre.Contains(q) || a.Accion.Contains(q) || (a.Detalle != null && a.Detalle.Contains(q)));
        }
        var total = await query.CountAsync();
        var paginas = Math.Max(1, (int)Math.Ceiling(total / (double)PorPagina));
        pagina = Math.Clamp(pagina, 1, paginas);
        ViewBag.Q = q;
        ViewBag.Total = total;
        ViewBag.Pagina = pagina;
        ViewBag.Paginas = paginas;
        return View(await query.OrderByDescending(a => a.Id).Skip((pagina - 1) * PorPagina).Take(PorPagina).ToListAsync());
    }
}