using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Orbit3D.Engine;

namespace Orbit3D.Graphics.Silk;

/// <summary>Registers the native graphics-surface handler for supported MAUI platforms.</summary>
public static class SilkGraphicsMauiExtensions
{
    /// <summary>Registers the Android OpenGL ES 3 surface handler.</summary>
    public static MauiAppBuilder UseSilkGraphics(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
#if ANDROID
        builder.UseOrbit3DEngine();
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>());
        builder.ConfigureLifecycleEvents(events => events.AddAndroid(android =>
        {
            android.OnPause(_ => SilkGLSurfaceView.PauseAll());
            android.OnResume(_ => SilkGLSurfaceView.ResumeAll());
        }));
        return builder;
#else
        throw new PlatformNotSupportedException("The Silk MAUI surface currently has a native handler only for Android OpenGL ES 3.");
#endif
    }
}