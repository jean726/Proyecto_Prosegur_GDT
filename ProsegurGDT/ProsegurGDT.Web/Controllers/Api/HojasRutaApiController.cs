using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProsegurGDT.Web.Domain;
using ProsegurGDT.Web.Models.Api;
using ProsegurGDT.Web.Services;
using ProsegurGDT.Web.Services.Api;

namespace ProsegurGDT.Web.Controllers.Api;

// CU28–CU31 — Hoja de ruta del Repartidor en la app móvil (RF35–RF38, RN-06, RN-07, RN-29, RN-30).
[ApiController]
[IgnoreAntiforgeryToken]   // la API se autentica con JWT en la cabecera, no con cookies
[Route("api/v1/hojas-ruta")]
[Authorize(Roles = Roles.Repartidor)]
[Produces("application/json")]
public class HojasRutaApiController : ControllerBase
{
    private readonly IRepartidorMovilService _movil;
    private readonly IAuditoriaService _audit;
    public HojasRutaApiController(IRepartidorMovilService movil, IAuditoriaService audit) { _movil = movil; _audit = audit; }

    /// <summary>CU28 — Hojas de ruta aprobadas o firmadas del repartidor (sin las de descargo cerrado).</summary>
    [HttpGet]
    [ProducesResponseType<List<HojaRutaResumenDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar() => Ok(await _movil.ListarHojasAsync(User.RepartidorId()));

    /// <summary>CU28 — Detalle de la hoja: visitas en orden, datos del titular, cita y resultado registrado.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<HojaRutaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int id)
    {
        var h = await _movil.ObtenerHojaAsync(User.RepartidorId(), id);
        return h is null ? Problem(detail: "La hoja de ruta no existe o no está asignada a usted.", statusCode: 404) : Ok(h);
    }

    /// <summary>CU31 — Confirma la recepción conforme de la hoja de ruta aprobada (firma del repartidor, RN-06).</summary>
    [HttpPost("{id:int}/confirmacion")]
    [ProducesResponseType<MensajeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirmar(int id) =>
        await ResponderAsync("ConfirmarHojaRutaMovil", "HojaRuta", id.ToString(),
            await _movil.ConfirmarRecepcionAsync(User.RepartidorId(), User.UsuarioId(), id));

    /// <summary>CU29 — Registra o corrige el resultado de la visita (pre-registro hasta el descargo, RN-30).</summary>
    [HttpPut("{id:int}/visitas/{tarjetaId:int}/resultado")]
    [ProducesResponseType<ResultadoCampoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ResultadoCampoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resultado(int id, int tarjetaId, SolicitudResultado s) =>
        await ResponderAsync("RegistrarResultadoCampo", "Tarjeta", tarjetaId.ToString(),
            await _movil.RegistrarResultadoAsync(User.RepartidorId(), id, tarjetaId, s));

    /// <summary>CU30 — Reporta una sospecha de suplantación de identidad al Verificador.</summary>
    [HttpPost("{id:int}/visitas/{tarjetaId:int}/suplantacion")]
    [ProducesResponseType<SuplantacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suplantacion(int id, int tarjetaId, SolicitudSuplantacion s) =>
        await ResponderAsync("ReportarSuplantacion", "Tarjeta", tarjetaId.ToString(),
            await _movil.ReportarSuplantacionAsync(User.RepartidorId(), id, tarjetaId, s));

    // Éxito → { mensaje, datos } y auditoría; error → ProblemDetails con el mensaje en español
    private async Task<IActionResult> ResponderAsync(string accion, string entidad, string entidadId, ResultadoMovil r)
    {
        if (!r.Ok) return Problem(detail: r.Mensaje, statusCode: r.Estado);
        await _audit.RegistrarAsync(accion, entidad, entidadId, r.Auditoria);
        return StatusCode(r.Estado, new { mensaje = r.Mensaje, datos = r.Datos });
    }
}
