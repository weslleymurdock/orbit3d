using Orbit3D.Graphics.Silk;

namespace SilkTriangleSample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .UseSilkGraphics()
            .Build();
}