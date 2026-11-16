using System.Collections.ObjectModel;
using System.Windows.Input;
using ProsegurGDT.Movil.Models;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// CU28 — Detalle de la hoja con las visitas en orden de cita; CU31 — confirmar la recepción conforme.
// RN-06: sin confirmar la recepción no se pueden registrar resultados ni suplantaciones.
public class HojaViewModel(ApiClient api) : BaseViewModel
{
    private HojaRutaDto? _hoja;

    public int Id { get; set; }
    public ObservableCollection<VisitaItem> Visitas { get; } = [];

    public string Jornada => _hoja is null ? "" : Textos.Jornada(_hoja.Jornada);
    public string Zona => _hoja is null ? "" :
        string.IsNullOrEmpty(_hoja.Sector) || _hoja.Sector == _hoja.Zona ? _hoja.Zona : $"{_hoja.Zona} · {_hoja.Sector}";
    public string Avance
    {
        get
        {
            if (_hoja is null) return "";
            var con = _hoja.Visitas.Count(v => v.Resultado is not null);
            return $"{con} de {_hoja.Visitas.Count} con resultado";
        }
    }
    public double Progreso => _hoja is null || _hoja.Visitas.Count == 0 ? 0
        : (double)_hoja.Visitas.Count(v => v.Resultado is not null) / _hoja.Visitas.Count;

    public bool PorConfirmar => _hoja is { RecepcionConfirmada: false };
    public bool Confirmada => _hoja is { RecepcionConfirmada: true, DescargoIniciado: false };
    public string TextoConfirmada => _hoja?.FechaConfirmacionUtc is { } f
        ? $"Recepción confirmada el {Textos.FechaHora(f)}" : "Recepción confirmada";
    public bool DescargoIniciado => _hoja is { DescargoIniciado: true };

    private bool _actualizando;
    public bool Actualizando { get => _actualizando; set => Set(ref _actualizando, value); }

    private string? _exito;
    public string? Exito
    {
        get => _exito;
        set { if (Set(ref _exito, value)) OnPropertyChanged(nameof(HayExito)); }
    }
    public bool HayExito => !string.IsNullOrEmpty(_exito);

    private Command? _cargar, _confirmar;
    public ICommand CargarCommand => _cargar ??= new Command(async () => await CargarAsync());
    public ICommand ConfirmarCommand => _confirmar ??= new Command(async () => await ConfirmarAsync());

    public async Task CargarAsync()
    {
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            _hoja = await api.HojaAsync(Id);
            Visitas.Clear();
            foreach (var v in _hoja.Visitas.OrderBy(v => v.Orden)) Visitas.Add(new VisitaItem(v));
            Error = null;
            Notificar();
        }
        catch (ApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            Ocupado = false;
            Actualizando = false;
        }
    }

    // CU31 — Aprobada → Firmada. Un 409 (ya confirmada) se muestra tal cual y se recarga la hoja.
    private async Task ConfirmarAsync()
    {
        if (_hoja is null || Ocupado) return;
        var ok = await Shell.Current.DisplayAlertAsync("Confirmar recepción",
            $"{Jornada} · {Zona}\n{_hoja.Visitas.Count} tarjetas.\n\nConfirmas que recibiste la hoja de ruta y las tarjetas conformes.",
            "Confirmar recepción conforme", "Cancelar");
        if (!ok) return;

        Ocupado = true;
        try
        {
            var r = await api.ConfirmarRecepcionAsync(_hoja.Id);
            Exito = r.Mensaje;
            Error = null;
        }
        catch (ApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            Ocupado = false;
        }
        await CargarAsync();
    }

    private void Notificar()
    {
        foreach (var p in new[] { nameof(Jornada), nameof(Zona), nameof(Avance), nameof(Progreso), nameof(PorConfirmar),
                     nameof(Confirmada), nameof(TextoConfirmada), nameof(DescargoIniciado) })
            OnPropertyChanged(p);
    }
}
