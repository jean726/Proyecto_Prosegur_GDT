using ProsegurGDT.Movil.ViewModels;

namespace ProsegurGDT.Movil.Views;

// Ruta «hoja?id=…» (registrada en AppShell).
public partial class HojaPage : ContentPage, IQueryAttributable
{
	private readonly HojaViewModel _vm;

	public HojaPage(HojaViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("id", out var id) && int.TryParse(id?.ToString(), out var n))
			_vm.Id = n;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.CargarAsync();
	}
}
