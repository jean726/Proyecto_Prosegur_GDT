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

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(a => a.UsuarioNombre.Contains(q) || a.Accion.Contains(q) || (a.Detalle != null && a.Detalle.Contains(q)));
        }
        ViewBag.Q = q;
        return View(await query.OrderByDescending(a => a.Id).Take(200).ToListAsync());
    }
}