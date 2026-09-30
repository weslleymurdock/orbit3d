using Microsoft.Maui.Controls;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// MAUI view for GPU rendering through a platform-owned Silk graphics context.
/// The Android handler currently provides an OpenGL ES 3 surface.
/// </summary>
public sealed class SilkGraphicsSurface : View
{
    /// <summary>Raised on the graphics thread after a native context becomes current.</summary>
    public event EventHandler<SilkGraphicsContextEventArgs>? ContextCreated;

    /// <summary>Raised on the graphics thread when its native context is lost or replaced.</summary>
    public event EventHandler<SilkGraphicsContextEventArgs>? ContextLost;

    /// <summary>Raised on the graphics thread when the drawable surface changes size.</summary>
    public event EventHandler<SilkGraphicsSurfaceResizedEventArgs>? SurfaceResized;

    /// <summary>Raised on the graphics thread for each frame; return from this callback presents on Android.</summary>
    public event EventHandler? RenderFrame;

    internal void RaiseContextCreated(SilkGraphicsContext context) =>
        ContextCreated?.Invoke(this, new SilkGraphicsContextEventArgs(context));

    internal void RaiseContextLost(SilkGraphicsContext context)
    {
        try
        {
            ContextLost?.Invoke(this, new SilkGraphicsContextEventArgs(context));
        }
        finally
        {
            context.DisposeApi();
        }
    }

    internal void RaiseSurfaceResized(int width, int height) =>
        SurfaceResized?.Invoke(this, new SilkGraphicsSurfaceResizedEventArgs(width, height));

    internal void RaiseRenderFrame() => RenderFrame?.Invoke(this, EventArgs.Empty);
}

/// <summary>Provides the graphics context associated with a surface lifecycle event.</summary>
public sealed class SilkGraphicsContextEventArgs(SilkGraphicsContext context) : EventArgs
{
    /// <summary>Gets the platform-owned Silk graphics context.</summary>
    public SilkGraphicsContext Context { get; } = context;
}

/// <summary>Provides the pixel dimensions of a graphics surface.</summary>
public sealed class SilkGraphicsSurfaceResizedEventArgs(int width, int height) : EventArgs
{
    /// <summary>Gets the surface width in pixels.</summary>
    public int Width { get; } = width;

    /// <summary>Gets the surface height in pixels.</summary>
    public int Height { get; } = height;
}