using System.Collections.ObjectModel;
using System.Windows.Input;
using ProsegurGDT.Movil.Models;
using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil.ViewModels;

// Ficha de la visita (verificar identidad, RN-07) y CU29 — registrar o corregir el resultado.
// El resultado es un pre-registro (RN-30): el servidor no cambia el estado de la tarjeta.
public class VisitaViewModel : BaseViewModel
{
    public static readonly IReadOnlyList<MotivoNoEntrega> ListaMotivos = Enum.GetValues<MotivoNoEntrega>();
    public const int MinimoDetalle = 5, MaximoDetalle = 300;

    private readonly ApiClient _api;
    private HojaRutaDto? _hoja;
    private VisitaDto? _v;

    public VisitaViewModel(ApiClient api)
    {
        _api = api;
        Opciones = new ObservableCollection<OpcionMotivo>(
            ListaMotivos.Select((m, i) => new OpcionMotivo(i, Textos.Motivo(m)) { EsUltima = i == ListaMotivos.Count - 1 }));
    }

    public int HojaId { get; set; }
    public int TarjetaId { get; set; }

    // ---------- Barra ----------
    public string Orden => _v?.Orden.ToString("00") ?? "";
    public string Codigo => _v?.Codigo ?? "";
    public bool HayVisita => _v is not null;

    // ---------- Ficha del titular ----------
    public string Titular => _v?.Titular ?? "";
    public string Documento => _v?.DocumentoTitular ?? "";
    public string Direccion => _v?.Direccion ?? "";
    public bool HayTelefono => !string.IsNullOrWhiteSpace(_v?.Telefono);
    public string Telefono => _v?.Telefono ?? "";
    public bool HayCita => _v?.Cita is not null;
    public bool SinCita => _v is not null && _v.Cita is null;
    public string CitaFechaHora => _v?.Cita is { } c ? $"{Textos.Fecha(c.Fecha)} · {Textos.Hora(c.Hora)}" : "";
    public string CitaLugar => _v?.Cita?.Lugar ?? "";
    public bool HayObservaciones => !string.IsNullOrWhiteSpace(_v?.Cita?.Observaciones);
    public string Observaciones => _v?.Cita?.Observaciones ?? "";

    // ---------- Resultado registrado (5d, 6b) ----------
    public bool HayResultado => _v?.Resultado is not null;
    public bool MostrarResultado => HayResultado && !_corrigiendo;
    public bool ResultadoEntregado => _v?.Resultado?.Resultado == ResultadoVisita.Entregada;
    public bool ResultadoNoEntregado => _v?.Resultado?.Resultado == ResultadoVisita.NoEntregada;
    public string TextoResultadoActual => TextoResultado.De(_v?.Resultado?.Resultado);
    public ColoresChip ChipResultado => ColoresChip.De(_v?.Resultado?.Resultado);
    public Color BordeResultado => Paleta.De(ResultadoEntregado ? "TOkBorde" : "TBorde");
    public string Registrado => _v?.Resultado is { } r ? Textos.Registrado(r.FechaActualizacionUtc) : "";
    public string Receptor => _v?.Resultado?.ReceptorNombre ?? "";
    public string DocumentoReceptor => _v?.Resultado?.ReceptorDocumento ?? "";
    public string MotivoActual => _v?.Resultado?.Motivo is { } m ? Textos.Motivo(m) : "";
    public bool HayDetalleActual => !string.IsNullOrWhiteSpace(_v?.Resultado?.Detalle);
    public string DetalleActual => _v?.Resultado?.Detalle ?? "";

    public bool Editable => _v?.Editable == true;
    // Motivo por el que no se puede registrar (RN-06, descargo iniciado o tarjeta ya procesada).
    public string? Bloqueo => _hoja is null || _v is null || Editable ? null
        : !_hoja.RecepcionConfirmada ? "Confirma la recepción de la hoja antes de registrar visitas."
        : _hoja.DescargoIniciado ? "Descargo iniciado: ya no se pueden modificar resultados."
        : "Esta tarjeta ya fue procesada en el descargo.";
    public bool HayBloqueo => Bloqueo is not null;

    // Formulario visible si se puede editar y (no hay resultado o se pulsó «Corregir»).
    private bool _corrigiendo;
    public bool MostrarFormulario => Editable && (!HayResultado || _corrigiendo);
    public bool PuedeCorregir => Editable && HayResultado && !_corrigiendo;
    public bool Corrigiendo => _corrigiendo;

    // CU30 — solo con la hoja confirmada y sin otro reporte pendiente para la tarjeta.
    public bool PuedeReportar => _hoja is { RecepcionConfirmada: true } && _v is not null
        && _v.Suplantacion?.Estado != EstadoReporteSuplantacion.Pendiente;
    public bool HaySuplantacion => _v?.Suplantacion is not null;
    public string Suplantacion => _v?.Suplantacion is { } s ? $"Suplantación reportada · {Textos.Suplantacion(s.Estado)}" : "";

    // ---------- Formulario CU29 (5a–5c) ----------
    private bool _esEntregada = true;
    public bool EsEntregada
    {
        get => _esEntregada;
        set
        {
            if (!Set(ref _esEntregada, value)) return;
            foreach (var p in new[] { nameof(EsNoEntregada), nameof(FondoEntregada), nameof(FondoNoEntregada),
                         nameof(TextoEntregada), nameof(TextoNoEntregada), nameof(DocumentoDistinto) })
                OnPropertyChanged(p);
        }
    }
    public bool EsNoEntregada => !_esEntregada;
    public Color FondoEntregada => _esEntregada ? Paleta.De("TAmarillo") : Colors.Transparent;
    public Color FondoNoEntregada => _esEntregada ? Colors.Transparent : Paleta.De("TAmarillo");
    public Color TextoEntregada => Paleta.De(_esEntregada ? "TFondo" : "TTenue");
    public Color TextoNoEntregada => Paleta.De(_esEntregada ? "TTenue" : "TFondo");

    private string _receptorNombre = "";
    public string ReceptorNombre
    {
        get => _receptorNombre;
        set { if (Set(ref _receptorNombre, value)) ErrorNombre = null; }
    }

    private string _receptorDocumento = "";
    public string ReceptorDocumento
    {
        get => _receptorDocumento;
        set { if (Set(ref _receptorDocumento, value)) { ErrorDocumento = null; OnPropertyChanged(nameof(DocumentoDistinto)); } }
    }
    // Aviso suave: se acepta igual y el Descargador lo deriva a verificación (mismo criterio de documento que la web).
    public bool DocumentoDistinto => EsEntregada && Normalizar(ReceptorDocumento).Length > 0
        && Normalizar(ReceptorDocumento) != Normalizar(Documento);

    public ObservableCollection<OpcionMotivo> Opciones { get; }

    private int _motivoIndice = -1;
    public int MotivoIndice
    {
        get => _motivoIndice;
        set
        {
            if (!Set(ref _motivoIndice, value)) return;
            foreach (var o in Opciones) o.Seleccionada = o.Indice == value;
            ErrorMotivo = null;
            ErrorDetalle = null;
            OnPropertyChanged(nameof(DetalleObligatorio));
            OnPropertyChanged(nameof(NotaDetalle));
        }
    }
    private bool MotivoEsOtro => MotivoIndice >= 0 && ListaMotivos[MotivoIndice] == MotivoNoEntrega.Otro;
    public bool DetalleObligatorio => MotivoEsOtro;
    public string NotaDetalle => MotivoEsOtro ? " (obligatorio)" : " (opcional)";

    private string _detalle = "";
    public string Detalle
    {
        get => _detalle;
        set { if (Set(ref _detalle, value)) { ErrorDetalle = null; OnPropertyChanged(nameof(ContadorDetalle)); } }
    }
    public string ContadorDetalle => $"{Detalle.Trim().Length} / {MaximoDetalle}";

    // Errores bajo cada campo (5c).
    private string? _errorNombre, _errorDocumento, _errorMotivo, _errorDetalle;
    public string? ErrorNombre { get => _errorNombre; set { if (Set(ref _errorNombre, value)) OnPropertyChanged(nameof(HayErrorNombre)); } }
    public string? ErrorDocumento { get => _errorDocumento; set { if (Set(ref _errorDocumento, value)) OnPropertyChanged(nameof(HayErrorDocumento)); } }
    public string? ErrorMotivo { get => _errorMotivo; set { if (Set(ref _errorMotivo, value)) OnPropertyChanged(nameof(HayErrorMotivo)); } }
    public string? ErrorDetalle { get => _errorDetalle; set { if (Set(ref _errorDetalle, value)) OnPropertyChanged(nameof(HayErrorDetalle)); } }
    public bool HayErrorNombre => _errorNombre is not null;
    public bool HayErrorDocumento => _errorDocumento is not null;
    public bool HayErrorMotivo => _errorMotivo is not null;
    public bool HayErrorDetalle => _errorDetalle is not null;

    // ---------- Mensajes ----------
    // Error del servidor (T1) con título según la operación, o franja sin conexión (T2); «Reintentar» repite la operación.
    private bool _sinConexion;
    public bool SinConexion { get => _sinConexion; set { if (Set(ref _sinConexion, value)) OnPropertyChanged(nameof(ErrorServidor)); } }
    public bool ErrorServidor => HayError && !SinConexion;
    private string _tituloError = "";
    public string TituloError { get => _tituloError; set => Set(ref _tituloError, value); }
    private bool _falloGuardar;

    // Toast de éxito con el mensaje del servidor; se oculta solo a los 3 s.
    private string? _exito;
    public string? Exito
    {
        get => _exito;
        set
        {
            if (!Set(ref _exito, value)) return;
            OnPropertyChanged(nameof(HayExito));
            if (!string.IsNullOrEmpty(value)) _ = OcultarExitoAsync(value);
        }
    }
    public bool HayExito => !string.IsNullOrEmpty(_exito);

    private async Task OcultarExitoAsync(string mensaje)
    {
        await Task.Delay(3000);
        if (_exito == mensaje) Exito = null;
    }

    private Command? _entregada, _noEntregada, _guardar, _corregir, _cancelar, _llamar, _reportar, _reintentar, _volver;
    private Command<OpcionMotivo>? _motivo;
    public ICommand EntregadaCommand => _entregada ??= new Command(() => EsEntregada = true);
    public ICommand NoEntregadaCommand => _noEntregada ??= new Command(() => EsEntregada = false);
    public ICommand MotivoCommand => _motivo ??= new Command<OpcionMotivo>(o => { if (o is not null) MotivoIndice = o.Indice; });
    public ICommand GuardarCommand => _guardar ??= new Command(async () => await GuardarAsync());
    public ICommand CorregirCommand => _corregir ??= new Command(() => { _corrigiendo = true; Exito = null; Notificar(); });
    public ICommand CancelarCommand => _cancelar ??= new Command(() => { _corrigiendo = false; Error = null; CargarFormulario(); Notificar(); });
    public ICommand LlamarCommand => _llamar ??= new Command(Llamar);
    public ICommand ReportarCommand => _reportar ??= new Command(async () =>
        await Shell.Current.GoToAsync($"suplantacion?hoja={HojaId}&tarjeta={TarjetaId}"));
    public ICommand ReintentarCommand => _reintentar ??= new Command(async () =>
    {
        if (_falloGuardar) await GuardarAsync(); else await CargarAsync();
    });
    public ICommand VolverCommand => _volver ??= new Command(async () => await Shell.Current.GoToAsync(".."));

    public async Task CargarAsync()
    {
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            _hoja = await _api.HojaAsync(HojaId);
            _v = _hoja.Visitas.FirstOrDefault(v => v.TarjetaId == TarjetaId);
            if (_v is null)
            {
                TituloError = "No se pudo cargar la visita";
                Error = "La tarjeta no pertenece a esta hoja de ruta.";
            }
            else
            {
                if (!_falloGuardar) Error = null;
                SinConexion = false;
                if (!_corrigiendo) CargarFormulario();
            }
            Notificar();
        }
        catch (ApiException e)
        {
            _falloGuardar = false;
            TituloError = "No se pudo cargar la visita";
            Error = e.Message;
            SinConexion = e.SinConexion;
        }
        finally
        {
            Ocupado = false;
            OnPropertyChanged(nameof(ErrorServidor));
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
        ErrorNombre = ErrorDocumento = ErrorMotivo = ErrorDetalle = null;
    }

    private async Task GuardarAsync()
    {
        if (Ocupado || _v is null) return;
        Exito = null;

        // Mismas reglas que el servidor (RN-07), con el error bajo cada campo; los mensajes del servidor siguen mandando.
        SolicitudResultado s;
        if (EsEntregada)
        {
            ErrorNombre = string.IsNullOrWhiteSpace(ReceptorNombre) ? "Ingresa el nombre del receptor." : null;
            ErrorDocumento = string.IsNullOrWhiteSpace(ReceptorDocumento) ? "Ingresa el documento del receptor." : null;
            if (HayErrorNombre || HayErrorDocumento) return;
            s = new(ResultadoVisita.Entregada, ReceptorNombre.Trim(), ReceptorDocumento.Trim(), null, null);
        }
        else
        {
            ErrorMotivo = MotivoIndice < 0 ? "Selecciona el motivo de no entrega." : null;
            ErrorDetalle = MotivoEsOtro && Detalle.Trim().Length < MinimoDetalle
                ? $"El detalle debe tener al menos {MinimoDetalle} caracteres." : null;
            if (HayErrorMotivo || HayErrorDetalle) return;
            s = new(ResultadoVisita.NoEntregada, null, null, ListaMotivos[MotivoIndice],
                string.IsNullOrWhiteSpace(Detalle) ? null : Detalle.Trim());
        }

        Error = null;
        Ocupado = true;
        string? mensaje = null;
        try
        {
            var r = await _api.RegistrarResultadoAsync(HojaId, TarjetaId, s);
            mensaje = r.Mensaje;
            _corrigiendo = false;
            _falloGuardar = false;
        }
        catch (ApiException e)
        {
            _falloGuardar = true;
            TituloError = "No se pudo guardar el resultado";
            Error = e.Message;
            SinConexion = e.SinConexion;
        }
        finally
        {
            Ocupado = false;
            OnPropertyChanged(nameof(ErrorServidor));
        }
        await CargarAsync();
        if (mensaje is not null) Exito = mensaje;
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
            TituloError = "No se pudo llamar";
            Error = "No se pudo abrir el marcador de llamadas.";
            OnPropertyChanged(nameof(ErrorServidor));
        }
    }

    private static string Normalizar(string? s) =>
        new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private void Notificar()
    {
        foreach (var p in new[] { nameof(Orden), nameof(Codigo), nameof(HayVisita), nameof(Titular), nameof(Documento),
                     nameof(Direccion), nameof(HayTelefono), nameof(Telefono), nameof(HayCita), nameof(SinCita), nameof(CitaFechaHora),
                     nameof(CitaLugar), nameof(HayObservaciones), nameof(Observaciones), nameof(HayResultado), nameof(MostrarResultado),
                     nameof(ResultadoEntregado), nameof(ResultadoNoEntregado), nameof(TextoResultadoActual), nameof(ChipResultado),
                     nameof(BordeResultado), nameof(Registrado), nameof(Receptor), nameof(DocumentoReceptor), nameof(MotivoActual),
                     nameof(HayDetalleActual), nameof(DetalleActual), nameof(Editable), nameof(Bloqueo), nameof(HayBloqueo),
                     nameof(MostrarFormulario), nameof(PuedeCorregir), nameof(Corrigiendo), nameof(PuedeReportar),
                     nameof(HaySuplantacion), nameof(Suplantacion), nameof(DocumentoDistinto) })
            OnPropertyChanged(p);
    }
}
