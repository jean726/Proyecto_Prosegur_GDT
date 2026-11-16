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

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_vm.Refrescar();
	}
}
