using ProsegurGDT.Movil.ViewModels;

namespace ProsegurGDT.Movil.Views;

// Ruta «visita?hoja=…&tarjeta=…» (registrada en AppShell).
public partial class VisitaPage : ContentPage, IQueryAttributable
{
	private readonly VisitaViewModel _vm;

	public VisitaPage(VisitaViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("hoja", out var h) && int.TryParse(h?.ToString(), out var hoja)) _vm.HojaId = hoja;
		if (query.TryGetValue("tarjeta", out var t) && int.TryParse(t?.ToString(), out var tarjeta)) _vm.TarjetaId = tarjeta;
	}

	// Se recarga al volver del reporte de suplantación.
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.CargarAsync();
	}
}
