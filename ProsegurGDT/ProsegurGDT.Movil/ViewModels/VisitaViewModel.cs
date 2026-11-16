using System.Windows.Input;
using ProsegurGDT.Movil.Models;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// Ficha de la visita (verificar identidad, RN-07) y CU29 — registrar o corregir el resultado.
// El resultado es un pre-registro (RN-30): el servidor no cambia el estado de la tarjeta.
public class VisitaViewModel(ApiClient api) : BaseViewModel
{
    public static readonly IReadOnlyList<MotivoNoEntrega> ListaMotivos = Enum.GetValues<MotivoNoEntrega>();

    private HojaRutaDto? _hoja;
    private VisitaDto? _v;

    public int HojaId { get; set; }
    public int TarjetaId { get; set; }

    // ---------- Ficha ----------
    public string Titulo => _v is null ? "" : $"Visita {_v.Orden:00} · {_v.Codigo}";
    public string Titular => _v?.Titular ?? "";
    public string Documento => _v?.DocumentoTitular ?? "";
    public string Direccion => _v?.Direccion ?? "";
    public bool HayTelefono => !string.IsNullOrWhiteSpace(_v?.Telefono);
    public string Telefono => _v?.Telefono ?? "";
    public string Cita => _v?.Cita is { } c
        ? $"{Textos.Jornada(c.Fecha)} · {Textos.Hora(c.Hora)} · {c.Lugar}" : "Sin cita registrada";
    public bool HayObservaciones => !string.IsNullOrWhiteSpace(_v?.Cita?.Observaciones);
    public string Observaciones => _v?.Cita?.Observaciones ?? "";

    // ---------- Estado de la visita ----------
    public bool HayResultado => _v?.Resultado is not null;
    public string ResultadoActual
    {
        get
        {
            var r = _v?.Resultado;
            if (r is null) return "";
            var cuando = $"Registrado el {Textos.FechaHora(r.FechaActualizacionUtc)}";
            return r.Resultado == ResultadoVisita.Entregada
                ? $"Entregada a {r.ReceptorNombre} ({r.ReceptorDocumento})\n{cuando}"
                : $"No entregada: {(r.Motivo is { } m ? Textos.Motivo(m) : "")}{(string.IsNullOrEmpty(r.Detalle) ? "" : $" — {r.Detalle}")}\n{cuando}";
        }
    }

    public bool Editable => _v?.Editable == true;
    // Motivo por el que no se puede registrar (RN-06, descargo iniciado o tarjeta ya procesada).
    public string? Bloqueo => _hoja is null || _v is null || Editable ? null
        : !_hoja.RecepcionConfirmada ? "Confirma la recepción de la hoja antes de registrar visitas."
        : _hoja.DescargoIniciado ? "El descargo de esta hoja ya comenzó: el resultado no puede modificarse desde la app."
        : "Esta tarjeta ya fue procesada en el descargo.";
    public bool HayBloqueo => Bloqueo is not null;

    // Formulario visible si se puede editar y (no hay resultado o se pulsó «Corregir»).
    private bool _corrigiendo;
    public bool MostrarFormulario => Editable && (!HayResultado || _corrigiendo);
    public bool PuedeCorregir => Editable && HayResultado && !_corrigiendo;
    public bool Corrigiendo => _corrigiendo;

    // CU30 — solo con la hoja confirmada y sin otro reporte pendiente para la tarjeta.
    public bool PuedeReportar => _hoja is { RecepcionConfirmada: true }
        && _v?.Suplantacion?.Estado != EstadoReporteSuplantacion.Pendiente;
    public bool HaySuplantacion => _v?.Suplantacion is not null;
    public string Suplantacion => _v?.Suplantacion is { } s
        ? $"Suplantación reportada · {Textos.Suplantacion(s.Estado)} ({Textos.FechaHora(s.FechaHoraUtc)})" : "";

    // ---------- Formulario CU29 ----------
    private bool _esEntregada = true;
    public bool EsEntregada
    {
        get => _esEntregada;
        set
        {
            if (!Set(ref _esEntregada, value)) return;
            OnPropertyChanged(nameof(EsNoEntregada));
            OnPropertyChanged(nameof(ColorEntregada));
            OnPropertyChanged(nameof(ColorNoEntregada));
            OnPropertyChanged(nameof(TextoEntregada));
            OnPropertyChanged(nameof(TextoNoEntregada));
        }
    }
    public bool EsNoEntregada => !_esEntregada;
    public Color ColorEntregada => Paleta.De(_esEntregada ? "TAmarillo" : "TSuperficie2");
    public Color ColorNoEntregada => Paleta.De(_esEntregada ? "TSuperficie2" : "TAmarillo");
    public Color TextoEntregada => Paleta.De(_esEntregada ? "TFondo" : "TTexto");
    public Color TextoNoEntregada => Paleta.De(_esEntregada ? "TTexto" : "TFondo");

    private string _receptorNombre = "";
    public string ReceptorNombre { get => _receptorNombre; set => Set(ref _receptorNombre, value); }

    private string _receptorDocumento = "";
    public string ReceptorDocumento
    {
        get => _receptorDocumento;
        set { if (Set(ref _receptorDocumento, value)) { OnPropertyChanged(nameof(DocumentoDistinto)); } }
    }
    // Aviso suave: se acepta igual y el Descargador lo deriva a verificación (mismo criterio de documento que la web).
    public bool DocumentoDistinto => EsEntregada && Normalizar(ReceptorDocumento).Length > 0
        && Normalizar(ReceptorDocumento) != Normalizar(Documento);

    public IReadOnlyList<string> Motivos { get; } = ListaMotivos.Select(Textos.Motivo).ToList();

    private int _motivoIndice = -1;
    public int MotivoIndice
    {
        get => _motivoIndice;
        set { if (Set(ref _motivoIndice, value)) OnPropertyChanged(nameof(EtiquetaDetalle)); }
    }
    private bool MotivoEsOtro => MotivoIndice >= 0 && ListaMotivos[MotivoIndice] == MotivoNoEntrega.Otro;
    public string EtiquetaDetalle => MotivoEsOtro ? "Detalle (obligatorio, mínimo 5 caracteres)" : "Detalle (opcional)";

    private string _detalle = "";
    public string Detalle { get => _detalle; set => Set(ref _detalle, value); }

    private string? _exito;
    public string? Exito
    {
        get => _exito;
        set { if (Set(ref _exito, value)) OnPropertyChanged(nameof(HayExito)); }
    }
    public bool HayExito => !string.IsNullOrEmpty(_exito);

    private Command? _entregada, _noEntregada, _guardar, _corregir, _cancelar, _llamar, _reportar;
    public ICommand EntregadaCommand => _entregada ??= new Command(() => EsEntregada = true);
    public ICommand NoEntregadaCommand => _noEntregada ??= new Command(() => EsEntregada = false);
    public ICommand GuardarCommand => _guardar ??= new Command(async () => await GuardarAsync());
    public ICommand CorregirCommand => _corregir ??= new Command(() => { _corrigiendo = true; Exito = null; Notificar(); });
    public ICommand CancelarCommand => _cancelar ??= new Command(() => { _corrigiendo = false; Error = null; CargarFormulario(); Notificar(); });
    public ICommand LlamarCommand => _llamar ??= new Command(Llamar);
    public ICommand ReportarCommand => _reportar ??= new Command(async () =>
        await Shell.Current.GoToAsync($"suplantacion?hoja={HojaId}&tarjeta={TarjetaId}"));

    public async Task CargarAsync()
    {
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            _hoja = await api.HojaAsync(HojaId);
            _v = _hoja.Visitas.FirstOrDefault(v => v.TarjetaId == TarjetaId);
            if (_v is null) Error = "La tarjeta no pertenece a esta hoja de ruta.";
            else if (!_corrigiendo) CargarFormulario();
            Notificar();
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

    // Precarga el formulario con el resultado guardado (para corregirlo) o lo deja en blanco.
    private void CargarFormulario()
    {
        var r = _v?.Resultado;
        EsEntregada = r?.Resultado != ResultadoVisita.NoEntregada;
        ReceptorNombre = r?.ReceptorNombre ?? "";
        ReceptorDocumento = r?.ReceptorDocumento ?? "";
        MotivoIndice = r?.Motivo is { } m ? ListaMotivos.ToList().IndexOf(m) : -1;
        Detalle = r?.Detalle ?? "";
    }

    private async Task GuardarAsync()
    {
        if (Ocupado || _v is null) return;
        Exito = null;

        // Mismas reglas que el servidor (RN-07); los mensajes del servidor siguen mandando.
        SolicitudResultado s;
        if (EsEntregada)
        {
            if (string.IsNullOrWhiteSpace(ReceptorNombre) || string.IsNullOrWhiteSpace(ReceptorDocumento))
            { Error = "Para registrar la entrega ingrese el nombre y el documento de identidad del receptor."; return; }
            s = new(ResultadoVisita.Entregada, ReceptorNombre.Trim(), ReceptorDocumento.Trim(), null, null);
        }
        else
        {
            if (MotivoIndice < 0) { Error = "Seleccione el motivo de no entrega."; return; }
            if (MotivoEsOtro && Detalle.Trim().Length < 5)
            { Error = "Describa el motivo de no entrega (mínimo 5 caracteres)."; return; }
            s = new(ResultadoVisita.NoEntregada, null, null, ListaMotivos[MotivoIndice],
                string.IsNullOrWhiteSpace(Detalle) ? null : Detalle.Trim());
        }

        Error = null;
        Ocupado = true;
        try
        {
            var r = await api.RegistrarResultadoAsync(HojaId, TarjetaId, s);
            Exito = r.Mensaje;
            _corrigiendo = false;
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

    private async void Llamar()
    {
        try
        {
            if (PhoneDialer.Default.IsSupported) PhoneDialer.Default.Open(Telefono);
            else await Launcher.Default.OpenAsync($"tel:{Telefono}");
        }
        catch
        {
            Error = "No se pudo abrir el marcador de llamadas.";
        }
    }

    private static string Normalizar(string? s) =>
        new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private void Notificar()
    {
        foreach (var p in new[] { nameof(Titulo), nameof(Titular), nameof(Documento), nameof(Direccion), nameof(HayTelefono),
                     nameof(Telefono), nameof(Cita), nameof(HayObservaciones), nameof(Observaciones), nameof(HayResultado),
                     nameof(ResultadoActual), nameof(Editable), nameof(Bloqueo), nameof(HayBloqueo), nameof(MostrarFormulario),
                     nameof(PuedeCorregir), nameof(Corrigiendo), nameof(PuedeReportar), nameof(HaySuplantacion), nameof(Suplantacion),
                     nameof(DocumentoDistinto) })
            OnPropertyChanged(p);
    }
}
