namespace ProsegurGDT.Movil;

public partial class App : Application
{
	private readonly IServiceProvider _servicios;

	public App(IServiceProvider servicios)
	{
		InitializeComponent();
		// La Torre de control solo existe en oscuro.
		UserAppTheme = AppTheme.Dark;
		_servicios = servicios;
	}

	// El Shell se crea aquí y no en el constructor: sus estilos necesitan los recursos de App.xaml ya cargados.
	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_servicios.GetRequiredService<AppShell>());
	}
}
