using Microsoft.Extensions.Logging;
using ProsegurGDT.Movil.Services;
using ProsegurGDT.Movil.ViewModels;
using ProsegurGDT.Movil.Views;

namespace ProsegurGDT.Movil;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Sesión y cliente de la API: una instancia para toda la app.
		builder.Services.AddSingleton<SesionService>();
		builder.Services.AddSingleton<ApiClient>();
		builder.Services.AddSingleton<AppShell>();

		// Pantallas y ViewModels (Shell las crea desde el contenedor).
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<HojasViewModel>();
		builder.Services.AddTransient<HojasPage>();
		builder.Services.AddTransient<HojaViewModel>();
		builder.Services.AddTransient<HojaPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
