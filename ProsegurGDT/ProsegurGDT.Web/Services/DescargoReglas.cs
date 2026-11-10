using System.Globalization;
using System.Text;
using ProsegurGDT.Web.Data;
using ProsegurGDT.Web.Domain;

namespace ProsegurGDT.Web.Services;

public record ResultadoDescargoOp(bool Ok, string Mensaje, string? Detalle = null, bool Aviso = false);

// Reglas compartidas por los servicios de Descargo (Fase 9).
public static class DescargoReglas
{
    // Hojas de ruta firmadas e incluidas en un reporte de entregas remitido al área de Descargo (CU08)
    public static IQueryable<HojaRuta> Elegibles(ApplicationDbContext db) =>
        db.HojasRuta.Where(h => h.Estado == EstadoHojaRuta.Firmada
            && db.ReportesEntregasLineas.Any(l => l.HojaRutaId == h.Id && l.ReporteEntregas!.Estado == EstadoReporteEntregas.Remitido));

    // Jornada = fecha de emisión de la hoja de asignación, en hora de Lima
    public static DateOnly Jornada(HojaRuta h) =>
        DateOnly.FromDateTime(HoraLima.Convertir(h.AsignacionRuta!.HojaAsignacion!.FechaHoraEmision));

    public static EstadoTarjeta Destino(ResultadoDescargo r) => r switch
    {
        ResultadoDescargo.Entregada => EstadoTarjeta.Entregada,
        ResultadoDescargo.Devuelta => EstadoTarjeta.Devuelta,
        ResultadoDescargo.ConIncidencia => EstadoTarjeta.ConIncidencia,
        _ => EstadoTarjeta.EnCustodia
    };

    // El receptor del cargo debe coincidir con el titular: mismo documento y nombre compatible
    public static bool IdentidadCoincide(string? receptorNombre, string? receptorDoc, Cliente cliente)
    {
        var docR = new string((receptorDoc ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var docC = new string((cliente.DocumentoIdentidad ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (docR.Length == 0 || docR != docC) return false;

        var a = Tokens(receptorNombre);
        var b = Tokens(cliente.NombreCompleto);
        if (a.Count == 0 || b.Count == 0) return false;
        var comunes = a.Intersect(b).Count();
        return comunes >= Math.Min(2, Math.Min(a.Count, b.Count));
    }

    private static HashSet<string> Tokens(string? texto)
    {
        var sb = new StringBuilder();
        foreach (var c in (texto ?? "").Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.IsLetter(c) ? char.ToUpperInvariant(c) : ' ');
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }
}
