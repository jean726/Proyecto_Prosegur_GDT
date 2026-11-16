using Microsoft.Maui.Controls.Shapes;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;

namespace ProsegurGDT.Movil.Controles;

// Icono lineal de 24 × 24 escalado a «Tamano». Sin fuentes de iconos ni paquetes extra.
public class Icono : ContentView
{
    public static readonly BindableProperty DatosProperty = BindableProperty.Create(
        nameof(Datos), typeof(string), typeof(Icono), "", propertyChanged: (b, _, n) => ((Icono)b).CambiarDatos((string)n));
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(Icono), Colors.White, propertyChanged: (b, _, n) => ((Icono)b)._trazo.Stroke = (Color)n);
    public static readonly BindableProperty TamanoProperty = BindableProperty.Create(
        nameof(Tamano), typeof(double), typeof(Icono), 20d, propertyChanged: (b, _, _) => ((Icono)b).Ajustar());
    public static readonly BindableProperty GrosorProperty = BindableProperty.Create(
        nameof(Grosor), typeof(double), typeof(Icono), 1.8d, propertyChanged: (b, _, _) => ((Icono)b).Ajustar());

    private static readonly PathGeometryConverter Conversor = new();
    private readonly MauiPath _trazo;

    public Icono()
    {
        _trazo = new MauiPath
        {
            Aspect = Stretch.None,
            Stroke = Colors.White,
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            InputTransparent = true
        };
        Content = _trazo;
        InputTransparent = true;
        Ajustar();
    }

    public string Datos { get => (string)GetValue(DatosProperty); set => SetValue(DatosProperty, value); }
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public double Tamano { get => (double)GetValue(TamanoProperty); set => SetValue(TamanoProperty, value); }
    public double Grosor { get => (double)GetValue(GrosorProperty); set => SetValue(GrosorProperty, value); }

    private void CambiarDatos(string datos) =>
        _trazo.Data = string.IsNullOrWhiteSpace(datos) ? null : (Geometry?)Conversor.ConvertFromInvariantString(datos);

    // La geometría se escala desde el origen; el grosor del trazo se escala aparte para que no engorde.
    private void Ajustar()
    {
        var escala = Tamano / 24d;
        WidthRequest = HeightRequest = _trazo.WidthRequest = _trazo.HeightRequest = Tamano;
        _trazo.RenderTransform = new ScaleTransform(escala, escala);
        _trazo.StrokeThickness = Grosor * escala;
    }
}
