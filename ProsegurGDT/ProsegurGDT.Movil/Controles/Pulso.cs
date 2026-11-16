namespace ProsegurGDT.Movil.Controles;

// Punto amarillo con un pulso suave: marca la siguiente visita pendiente (única animación de la app).
public class Pulso : Grid
{
    private readonly BoxView _anillo;
    private bool _activo;

    public Pulso()
    {
        WidthRequest = HeightRequest = 24;
        HorizontalOptions = LayoutOptions.Start;
        InputTransparent = true;
        var amarillo = Models.Paleta.De("TAmarillo");
        _anillo = new BoxView { Color = amarillo, CornerRadius = 5, WidthRequest = 10, HeightRequest = 10, Opacity = 0,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        Children.Add(_anillo);
        Children.Add(new BoxView { Color = amarillo, CornerRadius = 5, WidthRequest = 10, HeightRequest = 10,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center });
        Loaded += (_, _) => Iniciar();
        Unloaded += (_, _) => Detener();
    }

    private void Iniciar()
    {
        if (_activo) return;
        _activo = true;
        new Animation(t =>
        {
            _anillo.Scale = 1 + 1.4 * t;
            _anillo.Opacity = 0.55 * (1 - t);
        }).Commit(this, "Pulso", length: 1800, easing: Easing.CubicOut, repeat: () => _activo);
    }

    private void Detener()
    {
        _activo = false;
        this.AbortAnimation("Pulso");
    }
}
