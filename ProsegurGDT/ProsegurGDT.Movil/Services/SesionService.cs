using System.Text.Json;
using ProsegurGDT.Movil.Models;

namespace ProsegurGDT.Movil.Services;

// Sesión del repartidor (CU22/CU23). El token JWT se guarda en el almacenamiento seguro del dispositivo
// (Android Keystore vía SecureStorage) y se mantiene en memoria mientras la app está abierta.
public class SesionService
{
    private const string ClaveToken = "gdt_token";
    private const string ClaveExpira = "gdt_expira";
    private const string ClaveRepartidor = "gdt_repartidor";

    public string? Token { get; private set; }
    public DateTime ExpiraUtc { get; private set; }
    public RepartidorDto? Repartidor { get; private set; }

    public bool Activa => Token is not null && DateTime.UtcNow < ExpiraUtc;

    // Recupera la sesión guardada al abrir la app. Si el almacén seguro falla (p. ej. tras restaurar el
    // dispositivo), se descarta y se vuelve a iniciar sesión.
    public async Task<bool> CargarAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(ClaveToken);
            var expira = await SecureStorage.Default.GetAsync(ClaveExpira);
            var rep = await SecureStorage.Default.GetAsync(ClaveRepartidor);
            if (token is null || expira is null || rep is null) return false;

            Token = token;
            ExpiraUtc = DateTime.Parse(expira, null, System.Globalization.DateTimeStyles.RoundtripKind);
            Repartidor = JsonSerializer.Deserialize<RepartidorDto>(rep, ApiClient.Json);
            if (!Activa) { Limpiar(); return false; }
            return true;
        }
        catch
        {
            Limpiar();
            return false;
        }
    }

    public async Task GuardarAsync(TokenDto t)
    {
        Token = t.Token;
        ExpiraUtc = t.ExpiraUtc.ToUniversalTime();
        Repartidor = t.Repartidor;
        await SecureStorage.Default.SetAsync(ClaveToken, t.Token);
        await SecureStorage.Default.SetAsync(ClaveExpira, ExpiraUtc.ToString("O"));
        await SecureStorage.Default.SetAsync(ClaveRepartidor, JsonSerializer.Serialize(t.Repartidor, ApiClient.Json));
    }

    public void Limpiar()
    {
        Token = null;
        Repartidor = null;
        ExpiraUtc = default;
        SecureStorage.Default.Remove(ClaveToken);
        SecureStorage.Default.Remove(ClaveExpira);
        SecureStorage.Default.Remove(ClaveRepartidor);
    }
}
