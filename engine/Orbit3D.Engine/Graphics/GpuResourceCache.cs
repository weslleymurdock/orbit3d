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

    /// <summary>
    /// Gets or creates a mesh-backed runtime resource using the supplied factory.
    /// </summary>
    public MeshRenderResource GetOrCreateMesh(Mesh3D mesh, Func<Mesh3D, MeshRenderResource> factory)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(factory);

        if (meshCache.TryGetValue(mesh, out var resource))
            return resource;

        var created = factory(mesh);
        ArgumentNullException.ThrowIfNull(created);
        meshCache[mesh] = created;
        return created;
    }

    /// <summary>
    /// Gets or creates a texture-backed runtime resource using the supplied factory.
    /// </summary>
    public ITextureResource GetOrCreateTexture(Texture2D texture, Func<Texture2D, ITextureResource> factory)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(factory);

        if (textureCache.TryGetValue(texture, out var resource))
            return resource;

        var created = factory(texture);
        ArgumentNullException.ThrowIfNull(created);
        textureCache[texture] = created;
        return created;
    }

    /// <summary>
    /// Clears the cache and abandons all cached resources.
    /// </summary>
    public void Clear()
    {
        foreach (var resource in meshCache.Values)
        {
            resource.VertexBuffer.Dispose();
            resource.IndexBuffer.Dispose();
        }

        foreach (var resource in textureCache.Values)
            resource.Dispose();

        meshCache.Clear();
        textureCache.Clear();
    }
}
