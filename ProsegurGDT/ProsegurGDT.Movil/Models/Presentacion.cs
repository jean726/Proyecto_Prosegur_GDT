namespace ProsegurGDT.Movil.Models;

// Modelos de presentación: los DTO de la API con los textos y colores ya resueltos para las listas.

public static class Paleta
{
    public static Color De(string clave) =>
        Application.Current?.Resources.TryGetValue(clave, out var c) == true && c is Color color ? color : Colors.Gray;
}

// CU28 — una fila de «Mis hojas de ruta».
public class HojaItem(HojaRutaResumenDto h)
{
    public int Id => h.Id;
    public string Jornada => Textos.Jornada(h.Jornada);
    public string Zona => string.IsNullOrEmpty(h.Sector) || h.Sector == h.Zona ? h.Zona : $"{h.Zona} · {h.Sector}";
    public string Estado => h.RecepcionConfirmada ? "En curso" : "Por confirmar";
    public Color ColorEstado => Paleta.De(h.RecepcionConfirmada ? "TTexto" : "TAmarillo");
    public string Avance => $"{h.ConResultado} de {h.Tarjetas} con resultado";
    public double Progreso => h.Tarjetas == 0 ? 0 : (double)h.ConResultado / h.Tarjetas;
}

// CU28 — una visita del detalle de la hoja, en orden de cita.
public class VisitaItem(VisitaDto v)
{
    public VisitaDto Dto => v;
    public string Orden => v.Orden.ToString("00");
    public string Titular => v.Titular;
    public string Direccion => v.Direccion;
    public string Cita => v.Cita is null ? "Sin cita" : $"Cita {Textos.Hora(v.Cita.Hora)} · {v.Cita.Lugar}";

    public string Resultado => v.Resultado?.Resultado switch
    {
        ResultadoVisita.Entregada => "Entregada",
        ResultadoVisita.NoEntregada => "No entregada",
        _ => "Pendiente"
    };
    public Color ColorResultado => Paleta.De(v.Resultado?.Resultado switch
    {
        ResultadoVisita.Entregada => "TOk",
        ResultadoVisita.NoEntregada => "TTenue",
        _ => "TAmarillo"
    });

    public bool HaySuplantacion => v.Suplantacion is not null;
    public string Suplantacion => v.Suplantacion is null ? "" : $"Suplantación · {Textos.Suplantacion(v.Suplantacion.Estado)}";
}
