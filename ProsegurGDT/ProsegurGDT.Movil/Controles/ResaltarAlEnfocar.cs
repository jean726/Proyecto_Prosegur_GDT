namespace ProsegurGDT.Movil.Controles;

// Pone amarillo el borde de la caja (Border padre) mientras el campo tiene el foco, como en el diseño.
public class ResaltarAlEnfocar : Behavior<InputView>
{
    private Color? _original;

    protected override void OnAttachedTo(InputView campo)
    {
        base.OnAttachedTo(campo);
        campo.Focused += AlEnfocar;
        campo.Unfocused += AlDesenfocar;
    }

    protected override void OnDetachingFrom(InputView campo)
    {
        campo.Focused -= AlEnfocar;
        campo.Unfocused -= AlDesenfocar;
        base.OnDetachingFrom(campo);
    }

    private void AlEnfocar(object? sender, FocusEventArgs e)
    {
        if (Caja(sender) is not { } caja) return;
        _original ??= (caja.Stroke as SolidColorBrush)?.Color;
        caja.Stroke = Models.Paleta.De("TAmarillo");
    }

    private void AlDesenfocar(object? sender, FocusEventArgs e)
    {
        if (Caja(sender) is { } caja) caja.Stroke = _original ?? Models.Paleta.De("TBorde");
    }

    private static Border? Caja(object? sender)
    {
        for (var e = (sender as Element)?.Parent; e is not null; e = e.Parent)
            if (e is Border b) return b;
        return null;
    }
}
