using System.Windows.Input;
using ProsegurGDT.Movil.Models;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// CU30 — Reportar de inmediato una sospecha de suplantación de identidad; la evalúa el Verificador (CU06).
public class SuplantacionViewModel(ApiClient api) : BaseViewModel
{
    public const int Minimo = 10, Maximo = 500;

    private VisitaDto? _v;

    public int HojaId { get; set; }
    public int TarjetaId { get; set; }

    // Resumen de la visita (6a): «02 · TJ-000124», titular y DNI.
    public bool HayVisita => _v is not null;
    public string Referencia => _v is null ? "" : $"{_v.Orden:00} · {_v.Codigo}";
    public string Titular => _v?.Titular ?? "";
    public string Documento => _v?.DocumentoTitular ?? "";

    private string _descripcion = "";
    public string Descripcion
    {
        get => _descripcion;
        set { if (Set(ref _descripcion, value)) { ErrorDescripcion = null; OnPropertyChanged(nameof(Contador)); } }
    }
    public string Contador => $"{Descripcion.Trim().Length} / {Maximo}";

    private string? _errorDescripcion;
    public string? ErrorDescripcion
    {
        get => _errorDescripcion;
        set { if (Set(ref _errorDescripcion, value)) OnPropertyChanged(nameof(HayErrorDescripcion)); }
    }
    public bool HayErrorDescripcion => _errorDescripcion is not null;

    private bool _sinConexion;
    public bool SinConexion { get => _sinConexion; set { if (Set(ref _sinConexion, value)) OnPropertyChanged(nameof(ErrorServidor)); } }
    public bool ErrorServidor => HayError && !SinConexion;

    private Command? _enviar, _cerrar;
    public ICommand EnviarCommand => _enviar ??= new Command(async () => await EnviarAsync());
    public ICommand CerrarCommand => _cerrar ??= new Command(async () => await Shell.Current.GoToAsync(".."));

    // Solo para mostrar el resumen; si falla, el formulario sigue disponible.
    public async Task CargarAsync()
    {
        if (_v is not null) return;
        try
        {
            var hoja = await api.HojaAsync(HojaId);
            _v = hoja.Visitas.FirstOrDefault(v => v.TarjetaId == TarjetaId);
            foreach (var p in new[] { nameof(HayVisita), nameof(Referencia), nameof(Titular), nameof(Documento) })
                OnPropertyChanged(p);
        }
        catch (ApiException) { }
    }

    private async Task EnviarAsync()
    {
        if (Ocupado) return;
        var texto = Descripcion.Trim();
        if (texto.Length < Minimo || texto.Length > Maximo)
        { ErrorDescripcion = $"La descripción debe tener entre {Minimo} y {Maximo} caracteres."; return; }

        Error = null;
        Ocupado = true;
        try
        {
            var r = await api.ReportarSuplantacionAsync(HojaId, TarjetaId, texto);
            Descripcion = "";
            // 6b: la ficha muestra el chip «Suplantación reportada · Pendiente» y el toast con el mensaje del servidor.
            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["exito"] = r.Mensaje });
        }
        catch (ApiException e)
        {
            Error = e.Message;
            SinConexion = e.SinConexion;
        }
        finally
        {
            Ocupado = false;
            OnPropertyChanged(nameof(ErrorServidor));
        }
    }
}
