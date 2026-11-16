using System.Text.RegularExpressions;
namespace ProsegurGDT.Web.Domain;

public static class EnumExt
{
    // Solo la primera palabra y los nombres de áreas y roles llevan mayúscula
    private static readonly HashSet<string> Propios = new(StringComparer.OrdinalIgnoreCase)
        { "Buscador", "Verificador", "Descargador", "Descargo", "Supervisor", "Clasificador" };

    // Palabras sin tilde en los nombres de los enums
    private static readonly Dictionary<string, string> Tildes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Aprobacion"] = "aprobación", ["Correccion"] = "corrección", ["Documentacion"] = "documentación",
        ["Direccion"] = "dirección", ["Verificacion"] = "verificación", ["Boveda"] = "bóveda",
        ["Clasificacion"] = "clasificación", ["Conciliacion"] = "conciliación",
        ["Asignacion"] = "asignación", ["Numero"] = "número", ["Buzon"] = "buzón",
    };

    public static string Texto(this Enum valor)
    {
        var palabras = Regex.Split(valor.ToString(), "(?<!^)(?=[A-Z])");
        for (var i = 0; i < palabras.Length; i++)
        {
            var p = palabras[i];
            if (Tildes.TryGetValue(p, out var conTilde)) p = conTilde;
            if (Propios.Contains(p)) p = char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant();
            else p = p.ToLowerInvariant();
            if (i == 0) p = char.ToUpperInvariant(p[0]) + p[1..];
            palabras[i] = p;
        }
        return string.Join(' ', palabras);
    }
}
