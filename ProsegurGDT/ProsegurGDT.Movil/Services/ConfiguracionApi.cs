namespace ProsegurGDT.Movil.Services;

// URL base de la API. Debug (API_LOCAL): la web local (perfil http, puerto 5282) a través del cable USB
// con «adb reverse tcp:5282 tcp:5282», que funciona igual en un móvil real y en el emulador. Release: Azure.
public static class ConfiguracionApi
{
#if API_LOCAL
    public const string UrlBase = "http://localhost:5282/";
#else
    public const string UrlBase = "https://gdt-app-jean726.azurewebsites.net/";
#endif

    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(30);

    // C8 — Reintento de las lecturas ante un fallo de red: hasta 3 intentos, con 1 s y 2 s de pausa.
    public const int IntentosDeLectura = 3;
    public static readonly TimeSpan PausaEntreIntentos = TimeSpan.FromSeconds(1);
}
