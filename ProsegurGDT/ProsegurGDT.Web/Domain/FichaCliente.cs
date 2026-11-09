namespace ProsegurGDT.Web.Domain;

public static class FichaCliente
{
    public static bool TelefonoValido(string? t) => !string.IsNullOrWhiteSpace(t) && t.Count(char.IsDigit) >= 7;
    public static bool DatosCompletos(Cliente c) => DatosCompletos(c.Telefono, c.Direccion);

    public static bool DatosCompletos(string? telefono, string? direccion) =>
        TelefonoValido(telefono) && (direccion?.Trim().Length ?? 0) >= 8;
}