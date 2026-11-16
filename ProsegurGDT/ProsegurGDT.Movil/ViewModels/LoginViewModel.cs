using System.Windows.Input;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// CU22 — Iniciar sesión. Los mensajes de error del servidor (401/403/429) se muestran tal cual.
public class LoginViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly SesionService _sesion;

    public LoginViewModel(ApiClient api, SesionService sesion)
    {
        _api = api;
        _sesion = sesion;
        IngresarCommand = new Command(async () => await IngresarAsync(), () => Libre);
        AlternarContrasenaCommand = new Command(() => ContrasenaOculta = !ContrasenaOculta);
    }

    private string _correo = "";
    public string Correo { get => _correo; set => Set(ref _correo, value); }

    private string _contrasena = "";
    public string Contrasena { get => _contrasena; set => Set(ref _contrasena, value); }

    private bool _contrasenaOculta = true;
    public bool ContrasenaOculta
    {
        get => _contrasenaOculta;
        set { if (Set(ref _contrasenaOculta, value)) OnPropertyChanged(nameof(TextoAlternar)); }
    }
    public string TextoAlternar => ContrasenaOculta ? "Mostrar" : "Ocultar";

    public ICommand IngresarCommand { get; }
    public ICommand AlternarContrasenaCommand { get; }

    // Mensaje informativo que deja el cierre de sesión o la sesión expirada al volver a esta pantalla.
    private string? _aviso;
    public string? Aviso
    {
        get => _aviso;
        set { if (Set(ref _aviso, value)) OnPropertyChanged(nameof(HayAviso)); }
    }
    public bool HayAviso => !string.IsNullOrEmpty(_aviso);

    private async Task IngresarAsync()
    {
        Aviso = null;
        var correo = Correo.Trim();
        if (correo.Length == 0) { Error = "Ingrese su correo."; return; }
        if (Contrasena.Length == 0) { Error = "Ingrese su contraseña."; return; }

        Error = null;
        Ocupado = true;
        ((Command)IngresarCommand).ChangeCanExecute();
        try
        {
            var t = await _api.IniciarSesionAsync(correo, Contrasena);
            await _sesion.GuardarAsync(t);
            Contrasena = "";
            await Shell.Current.GoToAsync("//hojas");
        }
        catch (ApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            Ocupado = false;
            ((Command)IngresarCommand).ChangeCanExecute();
        }
    }
}
