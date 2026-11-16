using ProsegurGDT.Movil.ViewModels;

namespace ProsegurGDT.Movil.Views;

// Recibe «aviso» por la ruta (//login?aviso=…) al cerrar sesión o cuando la sesión expira.
public partial class LoginPage : ContentPage, IQueryAttributable
{
	private readonly LoginViewModel _vm;

	public LoginPage(LoginViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("aviso", out var a))
		{
			_vm.Error = null;
			_vm.Aviso = Uri.UnescapeDataString(a?.ToString() ?? "");
		}
	}
}
