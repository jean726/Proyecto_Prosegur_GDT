using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProsegurGDT.Movil.ViewModels;

// MVVM mínimo sin paquetes externos: notificación de cambios, estado «ocupado» y mensaje de error.
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _ocupado;
    public bool Ocupado
    {
        get => _ocupado;
        set { if (Set(ref _ocupado, value)) OnPropertyChanged(nameof(Libre)); }
    }
    public bool Libre => !_ocupado;

    private string? _error;
    public string? Error
    {
        get => _error;
        set { if (Set(ref _error, value)) OnPropertyChanged(nameof(HayError)); }
    }
    public bool HayError => !string.IsNullOrEmpty(_error);

    protected bool Set<T>(ref T campo, T valor, [CallerMemberName] string? nombre = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
        campo = valor;
        OnPropertyChanged(nombre);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}
