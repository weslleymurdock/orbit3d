using System.Numerics;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Implements the backend-neutral renderer contract while keeping the actual graphics API behind the device abstraction.
/// </summary>
public sealed class SilkRenderer3D : IRenderer3D
{
    private readonly IRenderDevice _device;
    private readonly Dictionary<int, ITextureResource> _boundTextures = [];
    private Light3D[] _lights = [];
    private Material3D? _material;
    private IRenderPipeline? _currentPipeline;
    private Matrix4x4 _view = Matrix4x4.Identity;
    private Matrix4x4 _projection = Matrix4x4.Identity;
    private Matrix4x4 _world = Matrix4x4.Identity;
    private Viewport _viewport;
    private Vector4 _clearColor = Vector4.Zero;
    private IRenderTarget? _activeTarget;

    /// <summary>
    /// Initializes a renderer using the supplied device.
    /// </summary>
    public SilkRenderer3D(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _viewport = new Viewport(0, 0, 0, 0);
    }

    /// <inheritdoc />
    public IRenderDevice Device => _device;

    /// <inheritdoc />
    public Viewport Viewport => _viewport;

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport dimensions must be non-negative.");

        _viewport = new Viewport(0, 0, width, height);
    }

    /// <inheritdoc />
    public void SetViewport(Viewport viewport)
    {
        _viewport = viewport;
    }

    /// <inheritdoc />
    public void BeginFrame(Vector4 clearColor, IRenderTarget? renderTarget = null)
    {
        EnsureActive();
        _clearColor = clearColor;
        _activeTarget = renderTarget;
        if (renderTarget is not null)
            ValidateOwned(renderTarget);
    }

    /// <inheritdoc />
    public void EndFrame()
    {
        EnsureActive();
        _activeTarget = null;
    }

    /// <inheritdoc />
    public void SetCamera(Matrix4x4 view, Matrix4x4 projection)
    {
        _view = view;
        _projection = projection;
    }

    /// <inheritdoc />
    public void SetWorldMatrix(Matrix4x4 world)
    {
        _world = world;
    }

    /// <inheritdoc />
    public void SetPipeline(IRenderPipeline pipeline)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(pipeline);
        ValidateOwned(pipeline);
        _currentPipeline = pipeline;
    }

    /// <inheritdoc />
    public void BindMaterial(Material3D material)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(material);
        _material = material;
    }

    /// <inheritdoc />
    public void BindTexture(int slot, ITextureResource texture)
    {
        EnsureActive();
        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot));

        ArgumentNullException.ThrowIfNull(texture);
        ValidateOwned(texture);
        _boundTextures[slot] = texture;
    }

    /// <inheritdoc />
    public void SetLights(ReadOnlySpan<Light3D> lights)
    {
        EnsureActive();
        _lights = lights.ToArray();
    }

    /// <inheritdoc />
    public void DrawIndexed(IVertexBuffer vertexBuffer, IIndexBuffer indexBuffer, int indexCount, int startIndex = 0, int baseVertex = 0)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(vertexBuffer);
        ArgumentNullException.ThrowIfNull(indexBuffer);
        ValidateOwned(vertexBuffer);
        ValidateOwned(indexBuffer);

        if (_currentPipeline is null)
            throw new InvalidOperationException("No render pipeline has been bound.");

        if (indexCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(indexCount));

        if (startIndex < 0 || startIndex >= indexBuffer.Description.IndexCount)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        var maxCount = indexBuffer.Description.IndexCount - startIndex;
        if (indexCount > maxCount)
            throw new ArgumentOutOfRangeException(nameof(indexCount));

        if (baseVertex < 0 || baseVertex + vertexBuffer.Description.VertexCount < 0)
            throw new ArgumentOutOfRangeException(nameof(baseVertex));

        _ = _view;
        _ = _projection;
        _ = _world;
        _ = _material;
        _ = _lights;
        _ = _boundTextures;
        _ = _clearColor;
        _ = _activeTarget;
        _ = indexCount;
        _ = baseVertex;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        _currentPipeline = null;
        _boundTextures.Clear();
        _lights = [];
        _material = null;
        _activeTarget = null;
    }

    private void EnsureActive()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SilkRenderer3D));
    }

    private void ValidateOwned(IGraphicsResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!ReferenceEquals(resource.Owner, _device))
            throw new ArgumentException("Resource belongs to another device.", nameof(resource));
        if (resource.IsDisposed)
            throw new ObjectDisposedException(resource.GetType().Name);
    }
}
