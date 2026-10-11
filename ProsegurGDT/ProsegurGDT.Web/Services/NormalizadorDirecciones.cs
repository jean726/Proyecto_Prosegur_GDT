using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace ProsegurGDT.Web.Services;

public static class NormalizadorDirecciones
{
    private static readonly Dictionary<string, string> Abrev = new()
    {
        ["AV"] = "AVENIDA",
        ["AVDA"] = "AVENIDA",
        ["JR"] = "JIRON",
        ["CAL"] = "CALLE",
        ["PSJE"] = "PASAJE",
        ["PJ"] = "PASAJE",
        ["URB"] = "URBANIZACION",
        ["DPTO"] = "DEPARTAMENTO",
        ["DEP"] = "DEPARTAMENTO",
        ["INT"] = "INTERIOR",
        ["MZ"] = "MANZANA",
        ["LT"] = "LOTE",
        ["NRO"] = "",
        ["NUM"] = "",
        ["N"] = ""
    };

    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        var s = Regex.Replace(sb.ToString().ToUpperInvariant(), @"[^A-Z0-9 ]", " ");
        var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Abrev.TryGetValue(t, out var full) ? full : t)
            .Where(t => t.Length > 0);
        return string.Join(' ', tokens);
    }
}