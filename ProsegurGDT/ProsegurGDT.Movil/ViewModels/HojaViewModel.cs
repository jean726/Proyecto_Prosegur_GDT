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

    // ---------- Resumen ----------
    public string Jornada => _hoja is null ? "" : Textos.Fecha(_hoja.Jornada);
    public string Zona => _hoja is null ? "" :
        string.IsNullOrEmpty(_hoja.Sector) || _hoja.Sector == _hoja.Zona ? _hoja.Zona : $"{_hoja.Zona} · {_hoja.Sector}";
    private int ConResultado => _hoja?.Visitas.Count(v => v.Resultado is not null) ?? 0;
    public int Tarjetas => _hoja?.Visitas.Count ?? 0;
    public string Avance => $"{ConResultado} / {Tarjetas}";
    public double Progreso => Tarjetas == 0 ? 0 : (double)ConResultado / Tarjetas;
    public bool HayHoja => _hoja is not null;

    public bool PorConfirmar => _hoja is { RecepcionConfirmada: false };
    public bool DescargoIniciado => _hoja is { DescargoIniciado: true };
    public string Estado => PorConfirmar ? "Por confirmar" : "En curso";
    public Color FondoEstado => PorConfirmar ? Paleta.De("TAmarillo") : Colors.Transparent;
    public Color TextoEstado => Paleta.De(PorConfirmar ? "TFondo" : "TAmarillo");
    public string TituloVisitas => $"Visitas · {Tarjetas}";

    private bool _actualizando;
    public bool Actualizando { get => _actualizando; set => Set(ref _actualizando, value); }

    // Sin conexión (franja neutra) frente a error del servidor (aviso rojo).
    private bool _sinConexion;
    public bool SinConexion { get => _sinConexion; set { if (Set(ref _sinConexion, value)) OnPropertyChanged(nameof(ErrorServidor)); } }
    public bool ErrorServidor => HayError && !SinConexion;
    private string _tituloError = "";
    public string TituloError { get => _tituloError; set => Set(ref _tituloError, value); }

    // ---------- CU31: hoja inferior (diseño 4a y 4b) ----------
    private bool _mostrarConfirmar;
    public bool MostrarConfirmar { get => _mostrarConfirmar; set { if (Set(ref _mostrarConfirmar, value)) OnPropertyChanged(nameof(HayHojaInferior)); } }
    private bool _mostrarConfirmada;
    public bool MostrarConfirmada { get => _mostrarConfirmada; set { if (Set(ref _mostrarConfirmada, value)) OnPropertyChanged(nameof(HayHojaInferior)); } }
    public bool HayHojaInferior => MostrarConfirmar || MostrarConfirmada;
    public string FechaConfirmacion => _hoja?.FechaConfirmacionUtc is { } f ? Textos.FechaHora(f) : "";
    public bool HayFechaConfirmacion => _hoja?.FechaConfirmacionUtc is not null;

    private Command? _cargar, _pedir, _confirmar, _cerrarHoja, _volver;
    public ICommand CargarCommand => _cargar ??= new Command(async () => await CargarAsync());
    public ICommand PedirConfirmacionCommand => _pedir ??= new Command(() => { if (_hoja is not null) MostrarConfirmar = true; });
    public ICommand ConfirmarCommand => _confirmar ??= new Command(async () => await ConfirmarAsync());
    public ICommand CerrarHojaInferiorCommand => _cerrarHoja ??= new Command(CerrarHojaInferior);
    public ICommand VolverCommand => _volver ??= new Command(async () => await Shell.Current.GoToAsync(".."));
    private Command<VisitaItem>? _abrirVisita;
    public ICommand AbrirVisitaCommand => _abrirVisita ??= new Command<VisitaItem>(async v =>
    {
        if (v is not null) await Shell.Current.GoToAsync($"visita?hoja={Id}&tarjeta={v.Dto.TarjetaId}");
    });

    public void CerrarHojaInferior()
    {
        if (Ocupado) return;
        MostrarConfirmar = false;
        MostrarConfirmada = false;
    }

    public async Task CargarAsync()
    {
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            _hoja = await api.HojaAsync(Id);
            var confirmada = _hoja.RecepcionConfirmada;
            var ordenadas = _hoja.Visitas.OrderBy(v => v.Orden).ToList();
            // Siguiente visita: la primera pendiente, solo con la ruta en curso.
            var siguiente = confirmada && !_hoja.DescargoIniciado
                ? ordenadas.FirstOrDefault(v => v.Resultado is null)?.TarjetaId : null;
            Visitas.Clear();
            foreach (var v in ordenadas)
                Visitas.Add(new VisitaItem(v, v.TarjetaId == siguiente, confirmada, _hoja.DescargoIniciado));
            Error = null;
            SinConexion = false;
            Notificar();
        }
        catch (ApiException e)
        {
            TituloError = "No se pudo cargar la hoja de ruta";
            Error = e.Message;
            SinConexion = e.SinConexion;
        }
        finally
        {
            Ocupado = false;
            Actualizando = false;
            OnPropertyChanged(nameof(ErrorServidor));
        }
    }

    // CU31 — Aprobada → Firmada. Un 409 (ya confirmada) se muestra tal cual y se recarga la hoja.
    private async Task ConfirmarAsync()
    {
        if (_hoja is null || Ocupado) return;
        Ocupado = true;
        var ok = false;
        try
        {
            await api.ConfirmarRecepcionAsync(_hoja.Id);
            Error = null;
            SinConexion = false;
            ok = true;
        }
        catch (ApiException e)
        {
            TituloError = "No se pudo confirmar la recepción";
            Error = e.Message;
            SinConexion = e.SinConexion;
        }
        finally
        {
            Ocupado = false;
            MostrarConfirmar = false;
            OnPropertyChanged(nameof(ErrorServidor));
        }
        await CargarAsync();
        // 4b: estado de éxito con la fecha y hora que devuelve la hoja recargada.
        if (ok) MostrarConfirmada = true;
    }

    private void Notificar()
    {
        foreach (var p in new[] { nameof(Jornada), nameof(Zona), nameof(Tarjetas), nameof(Avance), nameof(Progreso), nameof(HayHoja),
                     nameof(PorConfirmar), nameof(DescargoIniciado), nameof(Estado), nameof(FondoEstado), nameof(TextoEstado),
                     nameof(TituloVisitas), nameof(FechaConfirmacion), nameof(HayFechaConfirmacion) })
            OnPropertyChanged(p);
    }
}
