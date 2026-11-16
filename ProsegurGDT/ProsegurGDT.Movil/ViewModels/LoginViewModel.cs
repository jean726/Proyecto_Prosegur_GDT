using System.Net;
using System.Windows.Input;
using ProsegurGDT.Movil.Controles;
using ProsegurGDT.Movil.Models;
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
        set
        {
            if (!Set(ref _contrasenaOculta, value)) return;
            OnPropertyChanged(nameof(IconoOjo));
            OnPropertyChanged(nameof(ColorOjo));
            OnPropertyChanged(nameof(FuenteContrasena));
        }
    }
    // Ojo tachado y amarillo mientras la contraseña está visible; visible en mono, como en el diseño.
    public string IconoOjo => ContrasenaOculta ? Iconos.Ojo : Iconos.OjoTachado;
    public Color ColorOjo => Paleta.De(ContrasenaOculta ? "TTenue" : "TAmarillo");
    public string FuenteContrasena => ContrasenaOculta ? "Plex" : "Mono";

    // 403 = cuenta bloqueada (candado); el resto, triángulo. El texto es siempre el del servidor.
    private bool _errorEsBloqueo;
    public bool ErrorEsBloqueo
    {
        get => _errorEsBloqueo;
        set { if (Set(ref _errorEsBloqueo, value)) OnPropertyChanged(nameof(IconoError)); }
    }
    public string IconoError => ErrorEsBloqueo ? Iconos.Candado : Iconos.Alerta;

    public string TextoBoton => Ocupado ? "Ingresando…" : "Ingresar";
    public double OpacidadCampos => Ocupado ? 0.55 : 1;

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
        ErrorEsBloqueo = false;
        if (correo.Length == 0) { Error = "Ingresa tu correo."; return; }
        if (Contrasena.Length == 0) { Error = "Ingresa tu contraseña."; return; }

        Error = null;
        ErrorEsBloqueo = false;
        Ocupado = true;
        NotificarCarga();
        try
        {
            var t = await _api.IniciarSesionAsync(correo, Contrasena);
            await _sesion.GuardarAsync(t);
            Contrasena = "";
            await Shell.Current.GoToAsync("//hojas");
        }
        catch (ApiException e)
        {
            ErrorEsBloqueo = e.Estado == HttpStatusCode.Forbidden;
            Error = e.Message;
        }
        finally
        {
            Ocupado = false;
            NotificarCarga();
        }
    }

    private void NotificarCarga()
    {
        ((Command)IngresarCommand).ChangeCanExecute();
        OnPropertyChanged(nameof(TextoBoton));
        OnPropertyChanged(nameof(OpacidadCampos));
    }
}
