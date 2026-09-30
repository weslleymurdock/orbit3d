using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// A minimal, backend-owned device that implements the Orbit3D abstractions and manages resource lifetime.
/// </summary>
public sealed class SilkRenderDevice : IRenderDevice
{
    private readonly List<IGraphicsResource> _resources = [];

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public IVertexBuffer CreateVertexBuffer(VertexBufferDescription description, ReadOnlySpan<byte> data)
    {
        EnsureActive();
        ValidateDescription(description, data.Length, nameof(description));

        return Register(new SilkVertexBuffer(this, description));
    }

    /// <inheritdoc />
    public IIndexBuffer CreateIndexBuffer(IndexBufferDescription description, ReadOnlySpan<byte> data)
    {
        EnsureActive();
        if (description.IndexCount <= 0 || !Enum.IsDefined(description.Format))
            throw new ArgumentException("Index buffer description is invalid.", nameof(description));

        var indexSize = description.Format == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint);
        var expected = checked(description.IndexCount * indexSize);
        ValidateDataLength(expected, data.Length);

        return Register(new SilkIndexBuffer(this, description));
    }

    /// <inheritdoc />
    public ITextureResource CreateTexture(TextureDescription description, ReadOnlySpan<byte> data)
    {
        EnsureActive();
        if (description.Width <= 0 || description.Height <= 0 || !Enum.IsDefined(description.Format))
            throw new ArgumentException("Texture description is invalid.", nameof(description));

        ValidateDataLength(description.DataSizeInBytes, data.Length);
        return Register(new SilkTextureResource(this, description));
    }

    /// <inheritdoc />
    public IShaderProgram CreateShaderProgram(ShaderProgramDescription description)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(description);
        return Register(new SilkShaderProgram(this, description));
    }

    /// <inheritdoc />
    public IRenderPipeline CreatePipeline(RenderPipelineDescription description)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(description);
        ValidateOwned(description.ShaderProgram);
        return Register(new SilkRenderPipeline(this, description));
    }

    /// <inheritdoc />
    public IRenderTarget CreateRenderTarget(RenderTargetDescription description)
    {
        EnsureActive();
        if (description.Width <= 0 || description.Height <= 0)
            throw new ArgumentException("Render target description is invalid.", nameof(description));
        _ = new TextureDescription(description.Width, description.Height, description.ColorFormat);
        return Register(new SilkRenderTarget(this, description));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        foreach (var resource in _resources.ToArray())
            resource.Dispose();
    }

    internal void ValidateOwned(IGraphicsResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!ReferenceEquals(resource.Owner, this))
            throw new ArgumentException("Resource belongs to another device.", nameof(resource));
        if (resource.IsDisposed)
            throw new ObjectDisposedException(resource.GetType().Name);
    }

    private static void ValidateDescription(VertexBufferDescription description, int actualLength, string paramName)
    {
        if (description.VertexCount <= 0 || description.StrideInBytes <= 0)
            throw new ArgumentException("Vertex buffer description is invalid.", paramName);

        ValidateDataLength(description.DataSizeInBytes, actualLength);
    }

    private static void ValidateDataLength(int expected, int actual)
    {
        if (expected != actual)
            throw new ArgumentException("Data length does not match the resource description.");
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    private T Register<T>(T resource) where T : IGraphicsResource
    {
        _resources.Add(resource);
        return resource;
    }

    private abstract class SilkResource : IGraphicsResource
    {
        protected SilkResource(SilkRenderDevice owner)
        {
            Owner = owner;
        }

        public IRenderDevice Owner { get; }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            OnDispose();
        }

        protected virtual void OnDispose()
        {
        }
    }

    private sealed class SilkVertexBuffer(SilkRenderDevice owner, VertexBufferDescription description) : SilkResource(owner), IVertexBuffer
    {
        public VertexBufferDescription Description { get; } = description;
    }

    private sealed class SilkIndexBuffer(SilkRenderDevice owner, IndexBufferDescription description) : SilkResource(owner), IIndexBuffer
    {
        public IndexBufferDescription Description { get; } = description;
    }

    private sealed class SilkTextureResource(SilkRenderDevice owner, TextureDescription description) : SilkResource(owner), ITextureResource
    {
        public TextureDescription Description { get; } = description;
    }

    private sealed class SilkShaderProgram(SilkRenderDevice owner, ShaderProgramDescription description) : SilkResource(owner), IShaderProgram
    {
        public ShaderProgramDescription Description { get; } = description;
    }

    private sealed class SilkRenderPipeline(SilkRenderDevice owner, RenderPipelineDescription description) : SilkResource(owner), IRenderPipeline
    {
        public RenderPipelineDescription Description { get; } = description;
    }

    private sealed class SilkRenderTarget(SilkRenderDevice owner, RenderTargetDescription description) : SilkResource(owner), IRenderTarget
    {
        public RenderTargetDescription Description { get; } = description;
    }
}
