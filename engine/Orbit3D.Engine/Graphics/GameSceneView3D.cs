namespace Orbit3D.Engine.Graphics;

/// <summary>
/// Represents the MAUI surface metadata used by the 3D host without requiring a live UI container at unit-test time.
/// </summary>
public class GraphicsSurface
{
    /// <summary>
    /// Raised when the underlying surface size changes.
    /// </summary>
    public event EventHandler? SurfaceResized;

    /// <summary>
    /// Gets the current width of the surface in pixels.
    /// </summary>
    public int WidthInPixels { get; private set; }

    /// <summary>
    /// Gets the current height of the surface in pixels.
    /// </summary>
    public int HeightInPixels { get; private set; }

    /// <summary>
    /// Gets whether the surface has a usable size for rendering.
    /// </summary>
    public bool IsReady => WidthInPixels > 0 && HeightInPixels > 0;

    /// <summary>
    /// Updates the surface dimensions and raises the resize event when needed.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Surface dimensions must be non-negative.");

        if (width == WidthInPixels && height == HeightInPixels)
            return;

        WidthInPixels = width;
        HeightInPixels = height;
        SurfaceResized?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Hosts a 3D scene and its render surface while keeping the existing 2D runtime path intact.
/// </summary>
public class GameSceneView3D
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameSceneView3D"/> class.
    /// </summary>
    public GameSceneView3D()
    {
        Surface = new GraphicsSurface();
    }

    /// <summary>
    /// Gets the MAUI graphics surface associated with this view.
    /// </summary>
    public GraphicsSurface Surface { get; }

    /// <summary>
    /// Gets the scene currently associated with the view.
    /// </summary>
    public Scene3D? Scene { get; private set; }

    /// <summary>
    /// Gets the render device currently in use, if any.
    /// </summary>
    public IRenderDevice? Device { get; private set; }

    /// <summary>
    /// Gets the renderer currently in use, if any.
    /// </summary>
    public IRenderer3D? Renderer { get; private set; }

    /// <summary>
    /// Sets the active scene for this view.
    /// </summary>
    public void SetScene(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Scene = scene;
    }

    /// <summary>
    /// Assigns the render device and renderer for the view.
    /// </summary>
    public void Initialize(IRenderDevice device, IRenderer3D renderer)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(renderer);

        Device = device;
        Renderer = renderer;
        if (Surface.IsReady)
            Renderer.Resize(Surface.WidthInPixels, Surface.HeightInPixels);
    }

    /// <summary>
    /// Requests a renderer resize to match the current surface bounds.
    /// </summary>
    public void ResizeToSurface()
    {
        if (Renderer is not null)
            Renderer.Resize(Surface.WidthInPixels, Surface.HeightInPixels);
    }
}
