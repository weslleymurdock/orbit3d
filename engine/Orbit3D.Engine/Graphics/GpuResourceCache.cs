namespace Orbit3D.Engine.Graphics;

/// <summary>
/// Represents a device-owned mesh resource used for the engine's runtime drawing path.
/// </summary>
public sealed class MeshRenderResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeshRenderResource"/> class.
    /// </summary>
    public MeshRenderResource(IVertexBuffer vertexBuffer, IIndexBuffer indexBuffer)
    {
        ArgumentNullException.ThrowIfNull(vertexBuffer);
        ArgumentNullException.ThrowIfNull(indexBuffer);
        VertexBuffer = vertexBuffer;
        IndexBuffer = indexBuffer;
    }

    /// <summary>
    /// Gets the device-owned vertex buffer.
    /// </summary>
    public IVertexBuffer VertexBuffer { get; }

    /// <summary>
    /// Gets the device-owned index buffer.
    /// </summary>
    public IIndexBuffer IndexBuffer { get; }
}

/// <summary>
/// Caches backend-neutral GPU resources created for a current graphics device.
/// </summary>
public sealed class GpuResourceCache
{
    private readonly Dictionary<Mesh3D, MeshRenderResource> meshCache = new();
    private readonly Dictionary<Texture2D, ITextureResource> textureCache = new();
    private IRenderDevice? currentDevice;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuResourceCache"/> class.
    /// </summary>
    public GpuResourceCache()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuResourceCache"/> class for the supplied device.
    /// </summary>
    public GpuResourceCache(IRenderDevice device)
    {
        SetDevice(device);
    }

    /// <summary>
    /// Gets the device currently associated with the cache.
    /// </summary>
    public IRenderDevice? Device => currentDevice;

    /// <summary>
    /// Associates the cache with a new device and invalidates stale resources from any prior device.
    /// </summary>
    public void SetDevice(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (currentDevice is not null && !ReferenceEquals(currentDevice, device))
        {
            meshCache.Clear();
            textureCache.Clear();
        }

        currentDevice = device;
    }

    /// <summary>
    /// Invalidates all cached resources without disposing them through an incompatible or lost context.
    /// </summary>
    public void Invalidate()
    {
        currentDevice = null;
        meshCache.Clear();
        textureCache.Clear();
    }

    /// <summary>
    /// Attempts to get a cached mesh resource for the active device.
    /// </summary>
    public bool TryGetMesh(Mesh3D mesh, out MeshRenderResource resource)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        resource = null!;

        if (meshCache.TryGetValue(mesh, out var cached) && cached is not null && IsCompatible(cached.VertexBuffer, cached.IndexBuffer))
        {
            resource = cached;
            return true;
        }

        meshCache.Remove(mesh);
        return false;
    }

    /// <summary>
    /// Attempts to get a cached texture resource for the active device.
    /// </summary>
    public bool TryGetTexture(Texture2D texture, out ITextureResource resource)
    {
        ArgumentNullException.ThrowIfNull(texture);
        resource = null!;

        if (textureCache.TryGetValue(texture, out var cached) && cached is not null && IsCompatible(cached))
        {
            resource = cached;
            return true;
        }

        textureCache.Remove(texture);
        return false;
    }

    /// <summary>
    /// Gets or creates a mesh-backed runtime resource using the supplied factory.
    /// </summary>
    public MeshRenderResource GetOrCreateMesh(Mesh3D mesh, Func<Mesh3D, MeshRenderResource> factory)
        => GetOrCreateMesh(mesh, (_, m) => factory(m));

    /// <summary>
    /// Gets or creates a mesh-backed runtime resource using the supplied factory.
    /// </summary>
    public MeshRenderResource GetOrCreateMesh(Mesh3D mesh, Func<IRenderDevice, Mesh3D, MeshRenderResource> factory)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(factory);

        var device = EnsureDevice();
        if (TryGetMesh(mesh, out var resource))
            return resource;

        var created = factory(device, mesh);
        ArgumentNullException.ThrowIfNull(created);
        if (!IsCompatible(created.VertexBuffer, created.IndexBuffer))
            throw new InvalidOperationException("The mesh resource was created for a different graphics device than the active cache.");

        meshCache[mesh] = created;
        return created;
    }

    /// <summary>
    /// Gets or creates a texture-backed runtime resource using the supplied factory.
    /// </summary>
    public ITextureResource GetOrCreateTexture(Texture2D texture, Func<Texture2D, ITextureResource> factory)
        => GetOrCreateTexture(texture, (_, t) => factory(t));

    /// <summary>
    /// Gets or creates a texture-backed runtime resource using the supplied factory.
    /// </summary>
    public ITextureResource GetOrCreateTexture(Texture2D texture, Func<IRenderDevice, Texture2D, ITextureResource> factory)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(factory);

        var device = EnsureDevice();
        if (TryGetTexture(texture, out var resource))
            return resource;

        var created = factory(device, texture);
        ArgumentNullException.ThrowIfNull(created);
        if (!IsCompatible(created))
            throw new InvalidOperationException("The texture resource was created for a different graphics device than the active cache.");

        textureCache[texture] = created;
        return created;
    }

    /// <summary>
    /// Clears and disposes cached resources owned by the active device.
    /// </summary>
    public void Clear()
    {
        foreach (var resource in meshCache.Values)
        {
            if (ReferenceEquals(resource.VertexBuffer.Owner, currentDevice) && !resource.VertexBuffer.IsDisposed)
                resource.VertexBuffer.Dispose();
            if (ReferenceEquals(resource.IndexBuffer.Owner, currentDevice) && !resource.IndexBuffer.IsDisposed)
                resource.IndexBuffer.Dispose();
        }

        foreach (var resource in textureCache.Values)
        {
            if (ReferenceEquals(resource.Owner, currentDevice) && !resource.IsDisposed)
                resource.Dispose();
        }

        meshCache.Clear();
        textureCache.Clear();
    }

    private IRenderDevice EnsureDevice()
    {
        if (currentDevice is null || currentDevice.IsDisposed)
            throw new InvalidOperationException("A render device must be assigned to the GPU resource cache before creating resources.");

        return currentDevice;
    }

    private bool IsCompatible(IVertexBuffer vertexBuffer, IIndexBuffer indexBuffer)
    {
        if (currentDevice is null || currentDevice.IsDisposed)
            return false;

        return !vertexBuffer.IsDisposed && !indexBuffer.IsDisposed &&
               ReferenceEquals(vertexBuffer.Owner, currentDevice) &&
               ReferenceEquals(indexBuffer.Owner, currentDevice);
    }

    private bool IsCompatible(ITextureResource texture)
    {
        if (currentDevice is null || currentDevice.IsDisposed)
            return false;

        return !texture.IsDisposed && ReferenceEquals(texture.Owner, currentDevice);
    }
}
