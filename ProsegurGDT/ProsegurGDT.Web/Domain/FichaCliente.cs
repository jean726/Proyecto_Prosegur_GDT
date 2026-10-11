namespace ProsegurGDT.Web.Domain;

public static class FichaCliente
{
    public static bool TelefonoValido(string? t) => !string.IsNullOrWhiteSpace(t) && t.Count(char.IsDigit) >= 7;
    public static bool DatosCompletos(Cliente c) =>
        TelefonoValido(c.Telefono) && (c.Direccion?.Trim().Length ?? 0) >= 8;
}