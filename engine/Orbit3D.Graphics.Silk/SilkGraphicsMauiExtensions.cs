using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Orbit3D.Engine;

namespace Orbit3D.Graphics.Silk;

/// <summary>Registers the native graphics-surface handler for supported MAUI platforms.</summary>
public static class SilkGraphicsMauiExtensions
{
    /// <summary>Registers a native Silk graphics surface for Android, iOS or Windows.</summary>
    public static MauiAppBuilder UseSilkGraphics(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!SilkGraphicsPlatformSupport.IsCurrentPlatformSupported())
            throw new PlatformNotSupportedException("The Silk MAUI surface currently supports Android OpenGL ES 3, iOS OpenGL ES 3 and Windows OpenGL 3.3.");
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
#elif IOS
        builder.UseOrbit3DEngine();
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>());
        return builder;
#elif WINDOWS
        builder.UseOrbit3DEngine();
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>());
        return builder;
#else
        throw new PlatformNotSupportedException("The Silk MAUI surface currently supports Android OpenGL ES 3, iOS OpenGL ES 3 and Windows OpenGL 3.3.");
#endif
    }
}