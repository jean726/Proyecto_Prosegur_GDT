using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models;
using System.Diagnostics;
using ProsegurGDT.Web.Services;

namespace ProsegurGDT.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDashboardService _paneles;
        public HomeController(IDashboardService paneles) => _paneles = paneles;

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole(Roles.Administrador))
                return View("IndexAdmin", await _paneles.AdminAsync());

            if (User.IsInRole(Roles.Supervisor))
                return View("IndexSupervisor", await _paneles.SupervisorAsync());

            if (User.IsInRole(Roles.Clasificador))
                return View("IndexClasificador", await _paneles.ClasificadorAsync());

            if (User.IsInRole(Roles.Buscador))
                return View("IndexBuscador", await _paneles.BuscadorAsync());

            if (User.IsInRole(Roles.AuxiliarTelefonista))
                return View("IndexTelefonista", await _paneles.TelefonistaAsync());

            if (User.IsInRole(Roles.AnalistaDeRutas))
                return View("IndexAnalista", await _paneles.AnalistaAsync());

            if (User.IsInRole(Roles.Verificador))
                return View("IndexVerificador", await _paneles.VerificadorAsync());

            if (User.IsInRole(Roles.Descargador))
                return View("IndexDescargador", await _paneles.DescargadorAsync());

            if (User.IsInRole(Roles.EncargadoDeBoveda))
                return View("IndexBoveda", await _paneles.BovedaAsync());

            return View();
        }

        // C4 — Error no controlado (UseExceptionHandler): misma página de estado, con código de seguimiento.
        // Se vuelve a ejecutar con el método original (puede ser POST), por eso no exige antiforgery.
        [AllowAnonymous, IgnoreAntiforgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => VistaEstado(StatusCodes.Status500InternalServerError);

        // C4 — Páginas propias para las respuestas sin cuerpo (404, 400, 405…): UseStatusCodePagesWithReExecute
        [AllowAnonymous, IgnoreAntiforgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Estado(int id) => VistaEstado(id is >= 400 and <= 599 ? id : StatusCodes.Status404NotFound);

        private ViewResult VistaEstado(int codigo)
        {
            var (titulo, mensaje) = codigo switch
            {
                400 => ("Solicitud no válida", "El formulario expiró o la solicitud no es válida. Vuelva a la página anterior, actualícela e intente de nuevo."),
                401 => ("Sesión requerida", "Inicie sesión para continuar."),
                403 => ("Acceso denegado", "Su rol no tiene permiso para esta funcionalidad."),
                404 => ("Página no encontrada", "La dirección que abrió no existe o el registro ya no está disponible."),
                405 => ("Operación no permitida", "Esta dirección no admite la operación solicitada."),
                413 => ("Archivo demasiado grande", "El archivo supera el tamaño permitido. Reduzca su tamaño e intente de nuevo."),
                429 => ("Demasiadas solicitudes", "Se superó el número de intentos permitidos. Espere un minuto e intente nuevamente."),
                >= 500 => ("No se pudo completar la operación", "Ocurrió un problema inesperado al atender la solicitud. Intente nuevamente en unos minutos."),
                _ => ("No se pudo completar la operación", "La solicitud no pudo atenderse.")
            };
            Response.StatusCode = codigo;
            return View("Estado", new EstadoViewModel
            {
                Codigo = codigo, Titulo = titulo, Mensaje = mensaje,
                Seguimiento = codigo >= 500 ? Activity.Current?.Id ?? HttpContext.TraceIdentifier : null
            });
        }

        [AllowAnonymous]
        public IActionResult AccesoDenegado() => View();

        // B4 — Intranet: página que ve quien entra a la web desde fuera de las redes permitidas
        [AllowAnonymous]
        public IActionResult FueraDeRed()
        {
            if (HttpContext.Items[FiltroIntranetMiddleware.ClaveBloqueo] is not true)
                return RedirectToAction(nameof(Index));
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}