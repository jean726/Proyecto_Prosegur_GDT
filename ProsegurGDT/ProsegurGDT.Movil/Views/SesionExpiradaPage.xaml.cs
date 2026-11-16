namespace ProsegurGDT.Movil.Views;

// Modal transparente: el velo deja ver la pantalla en la que caducó la sesión. No se cierra con «Atrás».
public partial class SesionExpiradaPage : ContentPage
{
	public SesionExpiradaPage()
	{
		InitializeComponent();
	}

	private async void IrAlInicio(object? sender, EventArgs e)
	{
		await Navigation.PopModalAsync(false);
		await Shell.Current.GoToAsync("//login");
	}

	protected override bool OnBackButtonPressed() => true;
}
