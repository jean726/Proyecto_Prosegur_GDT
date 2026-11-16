using ProsegurGDT.Movil.Services;

namespace ProsegurGDT.Movil;

public partial class AppShell : Shell
{
	private readonly SesionService _sesion;
	private bool _iniciado;

	public AppShell(SesionService sesion, ApiClient api)
	{
		InitializeComponent();
		Routing.RegisterRoute("hoja", typeof(Views.HojaPage));
		Routing.RegisterRoute("visita", typeof(Views.VisitaPage));
		Routing.RegisterRoute("suplantacion", typeof(Views.SuplantacionPage));
		_sesion = sesion;
		// 401 en cualquier petición autenticada → vuelta al inicio de sesión con el mensaje del servidor.
		api.SesionExpirada += (_, mensaje) => MainThread.BeginInvokeOnMainThread(async () =>
			await GoToAsync($"//login?aviso={Uri.EscapeDataString(mensaje)}"));
	}

	// Al abrir la app: si hay una sesión guardada y vigente, se entra directamente a las hojas de ruta.
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_iniciado) return;
		_iniciado = true;
		if (await _sesion.CargarAsync())
			await GoToAsync("//hojas");
	}
}
