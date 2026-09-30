using Microsoft.Maui.LifecycleEvents;

namespace Orbit3D.Engine;

/// <summary>Registers Orbit3D runtime services and platform initialization with a MAUI application.</summary>
public static class Orbit3DMauiAppBuilderExtensions
{
    /// <summary>Registers Orbit3D services and loads the Assimp native libraries during Android activity creation.</summary>
    public static MauiAppBuilder UseOrbit3DEngine(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
#if ANDROID || IOS || MACCATALYST || WINDOWS
        builder.Services
            .AddSingleton<AssimpModelImporter>()
            .AddSingleton<IModelImporter>(services => services.GetRequiredService<AssimpModelImporter>());
#endif

#if ANDROID
        builder.ConfigureLifecycleEvents(events => events.AddAndroid(android =>
            android.OnCreate((_, _) =>
            {
                Java.Lang.JavaSystem.LoadLibrary("c++_shared");
                Java.Lang.JavaSystem.LoadLibrary("assimp");
                Java.Lang.JavaSystem.LoadLibrary("assimpmaui");
            })));
#endif

        return builder;
    }
}