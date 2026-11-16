using System.Collections.ObjectModel;
using System.Windows.Input;
using ProsegurGDT.Movil.Models;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// CU28 — Mis hojas de ruta (aprobadas o firmadas, RN-29) y CU23 — cerrar sesión.
public class HojasViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly SesionService _sesion;

    public HojasViewModel(ApiClient api, SesionService sesion)
    {
        _api = api;
        _sesion = sesion;
        CargarCommand = new Command(async () => await CargarAsync());
        AbrirCommand = new Command<HojaItem>(async h => await AbrirAsync(h));
        CerrarSesionCommand = new Command(async () => await CerrarSesionAsync(), () => Libre);
    }

    public ObservableCollection<HojaItem> Hojas { get; } = [];

    public string Nombre => _sesion.Repartidor?.Nombre ?? "";
    public string Detalle
    {
        get
        {
            var r = _sesion.Repartidor;
            if (r is null) return "";
            var tipo = Textos.Tipo(r.Tipo);
            return string.IsNullOrEmpty(r.ZonaHabitual) ? tipo : $"{tipo} · {r.ZonaHabitual}";
        }
    }

    private bool _actualizando;
    public bool Actualizando { get => _actualizando; set => Set(ref _actualizando, value); }

    private bool _vacio;
    public bool Vacio { get => _vacio; set => Set(ref _vacio, value); }

    public ICommand CargarCommand { get; }
    public ICommand AbrirCommand { get; }
    public ICommand CerrarSesionCommand { get; }

    public async Task CargarAsync()
    {
        OnPropertyChanged(nameof(Nombre));
        OnPropertyChanged(nameof(Detalle));
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            var lista = await _api.HojasAsync();
            Hojas.Clear();
            foreach (var h in lista.OrderBy(h => h.Jornada)) Hojas.Add(new HojaItem(h));
            Error = null;
            Vacio = Hojas.Count == 0;
        }
        catch (ApiException e)
        {
            Error = e.Message;
            Vacio = false;
        }
        finally
        {
            Ocupado = false;
            Actualizando = false;
        }
    }

    private static Task AbrirAsync(HojaItem? h) =>
        h is null ? Task.CompletedTask : Shell.Current.GoToAsync($"hoja?id={h.Id}");

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
        Hojas.Clear();
        await Shell.Current.GoToAsync($"//login?aviso={Uri.EscapeDataString(aviso)}");
    }
}
