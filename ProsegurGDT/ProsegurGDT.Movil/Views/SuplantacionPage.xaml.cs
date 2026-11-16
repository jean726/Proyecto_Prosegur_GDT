using ProsegurGDT.Movil.ViewModels;

namespace ProsegurGDT.Movil.Views;

// Ruta «suplantacion?hoja=…&tarjeta=…» (registrada en AppShell).
public partial class SuplantacionPage : ContentPage, IQueryAttributable
{
	private readonly SuplantacionViewModel _vm;

	public SuplantacionPage(SuplantacionViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("hoja", out var h) && int.TryParse(h?.ToString(), out var hoja)) _vm.HojaId = hoja;
		if (query.TryGetValue("tarjeta", out var t) && int.TryParse(t?.ToString(), out var tarjeta)) _vm.TarjetaId = tarjeta;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.CargarAsync();
	}
}
