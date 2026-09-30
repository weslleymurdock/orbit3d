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
    /// Creates a new render device backed by the selected Silk.NET graphics backend.
    /// </summary>
    public static IRenderDevice CreateDevice() => new SilkRenderDevice();

    /// <summary>
    /// Creates a renderer instance bound to the supplied device.
    /// </summary>
    /// <param name="device">The render device that owns all created resources.</param>
    public static IRenderer3D CreateRenderer(IRenderDevice device) => new SilkRenderer3D(device);
}
