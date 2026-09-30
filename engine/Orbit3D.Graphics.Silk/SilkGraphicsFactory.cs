using Orbit3D.Engine.Graphics;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Creates the concrete Silk-backed render device and renderer used by the Orbit3D runtime.
/// </summary>
public static class SilkGraphicsFactory
{
    /// <summary>
    /// Gets the pinned Silk.NET package version used by the backend implementation.
    /// </summary>
    public const string SilkVersion = "2.23.0";

    /// <summary>
    /// Creates a render device bound to an already-current native graphics context.
    /// </summary>
    /// <param name="context">Current platform-owned OpenGL or OpenGLES context.</param>
    public static IRenderDevice CreateDevice(SilkGraphicsContext context) => new SilkRenderDevice(context);

    /// <summary>
    /// Creates a renderer instance bound to the supplied device.
    /// </summary>
    /// <param name="device">The render device that owns all created resources.</param>
    public static IRenderer3D CreateRenderer(IRenderDevice device) => new SilkRenderer3D(device);
}
