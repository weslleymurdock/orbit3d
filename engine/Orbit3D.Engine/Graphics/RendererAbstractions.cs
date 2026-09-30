namespace Orbit3D.Engine.Graphics;

/// <summary>
/// Represents a generic backend graphics resource that requires explicit disposal.
/// </summary>
public interface IGraphicsResource : IDisposable
{
}

/// <summary>
/// Represents a hardware vertex buffer.
/// </summary>
public interface IVertexBuffer : IGraphicsResource
{
}

/// <summary>
/// Represents a hardware index buffer.
/// </summary>
public interface IIndexBuffer : IGraphicsResource
{
}

/// <summary>
/// Represents a compiled shader program on the GPU.
/// </summary>
public interface IShaderProgram : IGraphicsResource
{
}

/// <summary>
/// Represents a hardware texture resource.
/// </summary>
public interface ITextureResource : IGraphicsResource
{
}

/// <summary>
/// Represents a texture sampler state.
/// </summary>
public interface ISampler : IGraphicsResource
{
}

/// <summary>
/// Represents an off-screen render target or framebuffer.
/// </summary>
public interface IRenderTarget : IGraphicsResource
{
}

/// <summary>
/// Represents a depth/stencil buffer.
/// </summary>
public interface IDepthBuffer : IGraphicsResource
{
}

/// <summary>
/// Represents a GPU-uploaded mesh containing vertex and index buffers.
/// </summary>
public interface IMeshResource : IGraphicsResource
{
}

/// <summary>
/// Represents a command context for issuing rendering commands to the device.
/// </summary>
public interface IRenderContext : IDisposable
{
    /// <summary>Sets the render target.</summary>
    void SetRenderTarget(IRenderTarget? renderTarget);
    /// <summary>Sets the shader program.</summary>
    void SetShaderProgram(IShaderProgram shaderProgram);
    /// <summary>Sets the vertex buffer.</summary>
    void SetVertexBuffer(IVertexBuffer vertexBuffer);
    /// <summary>Sets the index buffer.</summary>
    void SetIndexBuffer(IIndexBuffer indexBuffer);
    /// <summary>Draws indexed primitives.</summary>
    void DrawIndexed(int indexCount, int startIndex = 0, int baseVertex = 0);
    /// <summary>Draws primitives.</summary>
    void Draw(int vertexCount, int startVertex = 0);
}

/// <summary>
/// Represents the graphics device abstraction capable of creating resources.
/// </summary>
public interface IRenderDevice : IDisposable
{
    /// <summary>Creates a vertex buffer.</summary>
    IVertexBuffer CreateVertexBuffer<T>(T[] data) where T : unmanaged;
    /// <summary>Creates a 16-bit index buffer.</summary>
    IIndexBuffer CreateIndexBuffer(ushort[] data);
    /// <summary>Creates a 32-bit index buffer.</summary>
    IIndexBuffer CreateIndexBuffer(uint[] data);
    /// <summary>Creates a texture resource.</summary>
    ITextureResource CreateTexture(Texture2D texture);
    /// <summary>Creates a shader program.</summary>
    IShaderProgram CreateShaderProgram(string vertexShader, string fragmentShader);
    /// <summary>Creates a mesh resource.</summary>
    IMeshResource CreateMeshResource(Mesh3D mesh);
    
    /// <summary>Begins a rendering context.</summary>
    IRenderContext BeginContext();
}

/// <summary>
/// The primary entry point for 3D rendering.
/// </summary>
public interface IRenderer3D : IDisposable
{
    /// <summary>Gets the render device.</summary>
    IRenderDevice Device { get; }
    
    /// <summary>Renders the scene.</summary>
    void RenderScene(Scene3D scene);
}
