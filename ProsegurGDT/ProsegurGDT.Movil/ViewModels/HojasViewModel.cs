using System.Windows.Input;
using ProsegurGDT.Movil.Models;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// Pantalla principal tras iniciar sesión. Por ahora: datos del repartidor y CU23 (cerrar sesión).
// La lista de hojas de ruta (CU28) se añade en el paso siguiente.
public class HojasViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly SesionService _sesion;

    public HojasViewModel(ApiClient api, SesionService sesion)
    {
        _api = api;
        _sesion = sesion;
        CerrarSesionCommand = new Command(async () => await CerrarSesionAsync(), () => Libre);
    }

    public string Nombre => _sesion.Repartidor?.Nombre ?? "";
    public string Detalle
    {
        get
        {
            var r = _sesion.Repartidor;
            if (r is null) return "";
            var tipo = r.Tipo == TipoRepartidor.Motorizado ? "Motorizado" : "Caminante";
            return string.IsNullOrEmpty(r.ZonaHabitual) ? tipo : $"{tipo} · {r.ZonaHabitual}";
        }
    }

    public ICommand CerrarSesionCommand { get; }

    public void Refrescar()
    {
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Detalle));
    }

    // CU23 — invalida el token en el servidor (cambia el sello de seguridad) y lo borra del dispositivo.
    // Si no hay red, se borra igual del dispositivo y se avisa: el token caduca solo al final de la jornada.
    private async Task CerrarSesionAsync()
    {
        var ok = await Shell.Current.DisplayAlertAsync("¿Cerrar sesión?",
            "Tendrás que volver a ingresar tus credenciales.", "Cerrar sesión", "Cancelar");
        if (!ok) return;

        Ocupado = true;
        string aviso;
        try
        {
            var r = await _api.CerrarSesionAsync();
            aviso = r.Mensaje;
        }
        catch (ApiException e) when (e.SinConexion)
        {
            aviso = "Sesión cerrada en este dispositivo. No se pudo avisar al servidor por falta de conexión.";
        }
        catch (ApiException e)
        {
            aviso = e.Message;
        }
        finally
        {
            Ocupado = false;
        }

        _sesion.Limpiar();
        await Shell.Current.GoToAsync($"//login?aviso={Uri.EscapeDataString(aviso)}");
    }
}
