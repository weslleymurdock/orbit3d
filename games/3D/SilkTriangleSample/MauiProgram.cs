using Microsoft.Extensions.Logging;
#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif
#if ANDROID
using Orbit3D.Graphics.Silk;
#endif

namespace SilkTriangleSample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
#if ANDROID
			.UseSilkGraphics()
			.ConfigureMauiHandlers(handlers => handlers.AddHandler<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>())
#endif
#if MAUI_DEVFLOW
			.AddMauiDevFlowAgent()
#endif
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if ANDROID
        builder.Services.AddTransient<GraphicSurfacePage>();
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
