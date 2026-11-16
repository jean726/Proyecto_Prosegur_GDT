using ProsegurGDT.Movil.ViewModels;

namespace ProsegurGDT.Movil.Views;

public partial class HojasPage : ContentPage
{
	private readonly HojasViewModel _vm;

	public HojasPage(HojasViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	// Se recarga cada vez que se vuelve a la lista (p. ej. tras confirmar una hoja o registrar resultados).
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.CargarAsync();
	}

	// Atrás con el diálogo abierto: lo cierra.
	protected override bool OnBackButtonPressed()
	{
		if (!_vm.MostrarCerrarSesion) return base.OnBackButtonPressed();
		_vm.MostrarCerrarSesion = false;
		return true;
	}
}
