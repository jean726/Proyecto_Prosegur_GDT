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
				// IBM Plex (licencia OFL, Licencias/IBM-Plex-OFL.txt): el peso va en la familia.
				fonts.AddFont("IBMPlexSans-Regular.ttf", "Plex");
				fonts.AddFont("IBMPlexSans-Medium.ttf", "PlexMedio");
				fonts.AddFont("IBMPlexSans-SemiBold.ttf", "PlexSemi");
				fonts.AddFont("IBMPlexSans-Bold.ttf", "PlexNegrita");
				fonts.AddFont("IBMPlexMono-Regular.ttf", "Mono");
				fonts.AddFont("IBMPlexMono-Medium.ttf", "MonoMedio");
				fonts.AddFont("IBMPlexMono-SemiBold.ttf", "MonoSemi");
			});

#if ANDROID
		// Los campos van dentro de una caja (Border) del diseño: sin la línea inferior nativa de Android.
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SinLinea", (h, _) =>
			h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
		Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("SinLinea", (h, _) =>
			h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

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
		builder.Services.AddTransient<VisitaViewModel>();
		builder.Services.AddTransient<VisitaPage>();
		builder.Services.AddTransient<SuplantacionViewModel>();
		builder.Services.AddTransient<SuplantacionPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
