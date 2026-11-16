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
		// 401 en cualquier petición autenticada → aviso «Tu sesión expiró» (T3) sobre la pantalla actual;
		// su botón lleva al inicio de sesión. Varias peticiones a la vez solo abren un aviso.
		api.SesionExpirada += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await AvisarSesionExpiradaAsync());
	}

	private async Task AvisarSesionExpiradaAsync()
	{
		if (Navigation.ModalStack.OfType<Views.SesionExpiradaPage>().Any()) return;
		if (CurrentState.Location.OriginalString.Contains("login")) return;
		await Navigation.PushModalAsync(new Views.SesionExpiradaPage(), false);
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
