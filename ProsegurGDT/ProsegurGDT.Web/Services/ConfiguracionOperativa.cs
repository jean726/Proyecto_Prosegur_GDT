namespace ProsegurGDT.Web.Services;

public interface IConfiguracionOperativa
{
    int MaxIntentosContacto { get; }
    double UmbralMedio { get; }
    double UmbralAlto { get; }
}

// Patrón Singleton: una única instancia, leída y validada una sola vez al arrancar.
public sealed class ConfiguracionOperativa : IConfiguracionOperativa
{
    public int MaxIntentosContacto { get; }
    public double UmbralMedio { get; }
    public double UmbralAlto { get; }

    public ConfiguracionOperativa(IConfiguration cfg)
    {
        MaxIntentosContacto = Math.Max(1, cfg.GetValue<int?>("Contacto:MaxIntentos") ?? 3);
        UmbralMedio = cfg.GetValue<double?>("IA:UmbralMedio") ?? 0.60;
        UmbralAlto = cfg.GetValue<double?>("IA:UmbralAlto") ?? 0.90;

        if (UmbralMedio is <= 0 or >= 1 || UmbralAlto <= UmbralMedio || UmbralAlto > 1)
            throw new InvalidOperationException(
                "Configuración inválida: se requiere 0 < IA:UmbralMedio < IA:UmbralAlto <= 1.");
    }
}