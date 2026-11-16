using System.Windows.Input;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// CU30 — Reportar de inmediato una sospecha de suplantación de identidad; la evalúa el Verificador (CU06).
public class SuplantacionViewModel(ApiClient api) : BaseViewModel
{
    public const int Minimo = 10, Maximo = 500;

    public int HojaId { get; set; }
    public int TarjetaId { get; set; }

    private string _descripcion = "";
    public string Descripcion
    {
        get => _descripcion;
        set { if (Set(ref _descripcion, value)) OnPropertyChanged(nameof(Contador)); }
    }
    public string Contador => $"{Descripcion.Trim().Length} / {Maximo}";

    private Command? _enviar;
    public ICommand EnviarCommand => _enviar ??= new Command(async () => await EnviarAsync());

    private async Task EnviarAsync()
    {
        if (Ocupado) return;
        var texto = Descripcion.Trim();
        if (texto.Length < Minimo || texto.Length > Maximo)
        { Error = $"La descripción debe tener entre {Minimo} y {Maximo} caracteres."; return; }

        Error = null;
        Ocupado = true;
        try
        {
            var r = await api.ReportarSuplantacionAsync(HojaId, TarjetaId, texto);
            await Shell.Current.DisplayAlertAsync("Reporte enviado", r.Mensaje, "Entendido");
            Descripcion = "";
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }
}
