using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProsegurGDT.Movil.Models;

namespace ProsegurGDT.Movil.Services;

// Error de la API con el mensaje del servidor tal cual (ProblemDetails en español) o uno local si no hay red.
public class ApiException(HttpStatusCode? estado, string mensaje) : Exception(mensaje)
{
    public HttpStatusCode? Estado { get; } = estado;
    public bool SinConexion => Estado is null;
}

// Cliente de /api/v1. Añade el token Bearer, traduce los errores y avisa de la sesión expirada (401).
public class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string SinConexion = "Sin conexión. Revisa tu señal e inténtalo de nuevo.";
    private const string ErrorGenerico = "No se pudo completar la operación. Inténtalo de nuevo.";

    private readonly HttpClient _http;
    private readonly SesionService _sesion;

    // Se dispara cuando la API responde 401 a una petición autenticada (token caducado o revocado).
    public event EventHandler<string>? SesionExpirada;

    public ApiClient(SesionService sesion)
    {
        _sesion = sesion;
        _http = new HttpClient { BaseAddress = new Uri(ConfiguracionApi.UrlBase), Timeout = ConfiguracionApi.Espera };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // CU22 — el 401 aquí son credenciales incorrectas, no una sesión expirada.
    public Task<TokenDto> IniciarSesionAsync(string correo, string contrasena) =>
        EnviarAsync<TokenDto>(HttpMethod.Post, "api/v1/auth/token", new SolicitudToken(correo, contrasena), autenticada: false);

    // CU23
    public Task<MensajeDto> CerrarSesionAsync() =>
        EnviarAsync<MensajeDto>(HttpMethod.Post, "api/v1/auth/logout", null);

    public Task<RepartidorDto> YoAsync() =>
        EnviarAsync<RepartidorDto>(HttpMethod.Get, "api/v1/auth/yo", null);

    // CU28
    public Task<List<HojaRutaResumenDto>> HojasAsync() =>
        EnviarAsync<List<HojaRutaResumenDto>>(HttpMethod.Get, "api/v1/hojas-ruta", null);

    public Task<HojaRutaDto> HojaAsync(int id) =>
        EnviarAsync<HojaRutaDto>(HttpMethod.Get, $"api/v1/hojas-ruta/{id}", null);

    // CU31
    public Task<MensajeDto> ConfirmarRecepcionAsync(int id) =>
        EnviarAsync<MensajeDto>(HttpMethod.Post, $"api/v1/hojas-ruta/{id}/confirmacion", null);

    // CU29
    public Task<MensajeDto> RegistrarResultadoAsync(int id, int tarjetaId, SolicitudResultado s) =>
        EnviarAsync<MensajeDto>(HttpMethod.Put, $"api/v1/hojas-ruta/{id}/visitas/{tarjetaId}/resultado", s);

    // CU30
    public Task<MensajeDto> ReportarSuplantacionAsync(int id, int tarjetaId, string descripcion) =>
        EnviarAsync<MensajeDto>(HttpMethod.Post, $"api/v1/hojas-ruta/{id}/visitas/{tarjetaId}/suplantacion",
            new SolicitudSuplantacion(descripcion));

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string ruta, object? cuerpo, bool autenticada = true)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            throw new ApiException(null, SinConexion);

        // Las lecturas (GET) se reintentan solas ante un corte breve de señal: no cambian nada en el servidor.
        // Los registros (POST/PUT) no se repiten automáticamente para no duplicar una operación que sí llegó.
        var intentos = metodo == HttpMethod.Get ? ConfiguracionApi.IntentosDeLectura : 1;
        HttpResponseMessage? resp = null;
        for (var intento = 1; resp is null; intento++)
        {
            using var req = new HttpRequestMessage(metodo, ruta);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: Json);
            if (autenticada && _sesion.Token is not null)
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sesion.Token);
            try
            {
                resp = await _http.SendAsync(req);
                // 502/503/504: el servidor está arrancando (plan gratuito de Azure) o saturado un instante
                if (intento < intentos && (int)resp.StatusCode is 502 or 503 or 504)
                {
                    resp.Dispose();
                    resp = null;
                    await Task.Delay(ConfiguracionApi.PausaEntreIntentos * intento);
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                if (intento >= intentos) throw new ApiException(null, SinConexion);
                await Task.Delay(ConfiguracionApi.PausaEntreIntentos * intento);
            }
        }

        using (resp)
        {
            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<T>(Json))!;

            var mensaje = await LeerProblemaAsync(resp);
            if (autenticada && resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _sesion.Limpiar();
                SesionExpirada?.Invoke(this, mensaje);
            }
            throw new ApiException(resp.StatusCode, mensaje);
        }
    }

    // Prioridad: errores de validación (uno por línea) → detail → title → genérico. 429 sin cuerpo propio.
    private static async Task<string> LeerProblemaAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            return "Demasiados intentos. Espera un minuto e inténtalo de nuevo.";
        try
        {
            var p = await resp.Content.ReadFromJsonAsync<ProblemaDto>(Json);
            if (p?.Errors is { Count: > 0 })
                return string.Join("\n", p.Errors.Values.SelectMany(v => v).Distinct());
            if (!string.IsNullOrWhiteSpace(p?.Detail)) return p.Detail;
            if (!string.IsNullOrWhiteSpace(p?.Title)) return p.Title;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException) { }
        return ErrorGenerico;
    }
}
