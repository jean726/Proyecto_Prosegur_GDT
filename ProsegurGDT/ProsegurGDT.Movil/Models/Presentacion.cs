namespace ProsegurGDT.Movil.Models;

// Modelos de presentación: los DTO de la API con los textos y colores del diseño ya resueltos para las listas.

public static class Paleta
{
    public static Color De(string clave) =>
        Application.Current?.Resources.TryGetValue(clave, out var c) == true && c is Color color ? color : Colors.Gray;
}

// Colores de un chip (fondo, borde, texto).
public record ColoresChip(Color Fondo, Color Borde, Color Texto)
{
    public static ColoresChip Entregada => new(Paleta.De("TOkFondo"), Paleta.De("TOkBorde"), Paleta.De("TOk"));
    public static ColoresChip NoEntregada => new(Paleta.De("TSuperficie2"), Paleta.De("TBordeFuerte"), Paleta.De("TTexto"));
    public static ColoresChip Pendiente => new(Paleta.De("TSuperficie2"), Paleta.De("TBorde"), Paleta.De("TTenue"));
    public static ColoresChip Riesgo => new(Paleta.De("TRiesgoFondo"), Paleta.De("TRiesgoBorde"), Paleta.De("TRiesgoTexto"));

    public static ColoresChip De(ResultadoVisita? r) => r switch
    {
        ResultadoVisita.Entregada => Entregada,
        ResultadoVisita.NoEntregada => NoEntregada,
        _ => Pendiente
    };
}

public static class TextoResultado
{
    public static string De(ResultadoVisita? r) => r switch
    {
        ResultadoVisita.Entregada => "Entregada",
        ResultadoVisita.NoEntregada => "No entregada",
        _ => "Pendiente"
    };
}

// CU28 — una tarjeta de «Mis hojas de ruta». Aprobada = «Por confirmar» (chip relleno); Firmada = «En curso» (contorno).
public class HojaItem(HojaRutaResumenDto h)
{
    public int Id => h.Id;
    public string Jornada => Textos.Fecha(h.Jornada);
    public string Zona => h.Zona;
    public string Sector => h.Sector ?? "";
    public bool HaySector => !string.IsNullOrEmpty(h.Sector) && h.Sector != h.Zona;

    public bool PorConfirmar => !h.RecepcionConfirmada;
    public string Estado => PorConfirmar ? "Por confirmar" : "En curso";
    public Color FondoEstado => PorConfirmar ? Paleta.De("TAmarillo") : Colors.Transparent;
    public Color TextoEstado => Paleta.De(PorConfirmar ? "TFondo" : "TAmarillo");
    public Color BordeTarjeta => Paleta.De(PorConfirmar ? "TAmarillo" : "TBorde");

    public string Avance => $"{h.ConResultado} / {h.Tarjetas}";
    public double Progreso => h.Tarjetas == 0 ? 0 : (double)h.ConResultado / h.Tarjetas;
}

// CU28 — una fila de visita del detalle de la hoja, en orden de cita.
// «Siguiente» = primera visita sin resultado con la recepción confirmada: número y hora en amarillo y pulso.
public class VisitaItem(VisitaDto v, bool siguiente, bool confirmada, bool descargoIniciado)
{
    public VisitaDto Dto => v;
    public string Orden => v.Orden.ToString("00");
    public string Titular => v.Titular;
    public string Direccion => v.Direccion;

    public bool EsSiguiente => siguiente;
    public bool NoEsSiguiente => !siguiente;
    public Color ColorOrden => Paleta.De(siguiente ? "TAmarillo" : v.Resultado is null ? "TTenue" : "TTenue2");
    public Color FondoFila => siguiente ? Paleta.De("TSuperficie") : Colors.Transparent;
    // Sin recepción confirmada las visitas se ven atenuadas (RN-06), como en el diseño.
    public double Opacidad => confirmada ? 1 : 0.6;
    public bool MostrarFlecha => !descargoIniciado;

    // Hora de la cita en mono; «Sin cita» en gris. Con el descargo iniciado no se muestra la hora.
    public bool MostrarHora => !descargoIniciado;
    public bool HayCita => v.Cita is not null;
    public string Hora => v.Cita is null ? "Sin cita" : Textos.Hora(v.Cita.Hora);
    public string FuenteHora => v.Cita is null ? "Plex" : siguiente ? "MonoSemi" : "MonoMedio";
    public Color ColorHora => Paleta.De(v.Cita is null ? "TTenue2" : siguiente ? "TAmarillo" : confirmada ? "TTenue" : "TTexto");

    public string Resultado => TextoResultado.De(v.Resultado?.Resultado);
    public ColoresChip Chip => ColoresChip.De(v.Resultado?.Resultado);

    public bool HaySuplantacion => v.Suplantacion is not null;
    public string Suplantacion => v.Suplantacion is null ? "" : $"Suplantación · {Textos.Suplantacion(v.Suplantacion.Estado)}";
    public ColoresChip ChipRiesgo => ColoresChip.Riesgo;
}

// CU29 — una opción de la lista de motivos de no entrega (radio).
public class OpcionMotivo(int indice, string texto) : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public int Indice => indice;
    public string Texto => texto;
    public bool EsUltima { get; init; }

    private bool _seleccionada;
    public bool Seleccionada
    {
        get => _seleccionada;
        set
        {
            if (_seleccionada == value) return;
            _seleccionada = value;
            foreach (var p in new[] { nameof(Seleccionada), nameof(ColorAro), nameof(Fuente) })
                PropertyChanged?.Invoke(this, new(p));
        }
    }
    public Color ColorAro => Paleta.De(_seleccionada ? "TAmarillo" : "TApagado");
    public string Fuente => _seleccionada ? "PlexSemi" : "Plex";
}
