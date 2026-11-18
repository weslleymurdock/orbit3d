using Microsoft.Extensions.Logging;
#if ANDROID || IOS || MACCATALYST || WINDOWS
using Orbit3D.Graphics.Silk;
#endif

namespace AnimationSample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
#if ANDROID || IOS || MACCATALYST || WINDOWS
			.UseSilkGraphics()
#endif
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if ANDROID || IOS || MACCATALYST || WINDOWS
		builder.Services.AddTransient<AnimationSurfacePage>();
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
