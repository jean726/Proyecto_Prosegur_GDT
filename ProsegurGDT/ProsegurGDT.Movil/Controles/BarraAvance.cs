using Microsoft.Maui.Controls.Shapes;

namespace ProsegurGDT.Movil.Controles;

// Barra de avance de 6 px con extremos redondeados («Con resultado n / m»).
public class BarraAvance : Border
{
    public static readonly BindableProperty ProgresoProperty = BindableProperty.Create(
        nameof(Progreso), typeof(double), typeof(BarraAvance), 0d, propertyChanged: (b, _, _) => ((BarraAvance)b).Ajustar());

    private readonly BoxView _relleno;

    public BarraAvance()
    {
        HeightRequest = 6;
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 3 };
        BackgroundColor = Paleta("TBorde");
        _relleno = new BoxView { Color = Paleta("TAmarillo"), HorizontalOptions = LayoutOptions.Start, WidthRequest = 0 };
        Content = _relleno;
        SizeChanged += (_, _) => Ajustar();
    }

    public double Progreso { get => (double)GetValue(ProgresoProperty); set => SetValue(ProgresoProperty, value); }

    private void Ajustar() => _relleno.WidthRequest = Width <= 0 ? 0 : Width * Math.Clamp(Progreso, 0, 1);

    private static Color Paleta(string clave) => Models.Paleta.De(clave);
}
