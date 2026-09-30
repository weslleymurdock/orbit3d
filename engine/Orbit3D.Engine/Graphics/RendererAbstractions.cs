using System.Numerics;

namespace Orbit3D.Engine.Graphics;

/// <summary>
/// Describes immutable vertex data uploaded to a vertex buffer.
/// </summary>
public readonly record struct VertexBufferDescription
{
    /// <summary>Creates a vertex-buffer description.</summary>
    /// <param name="vertexCount">Number of vertices in the buffer.</param>
    /// <param name="strideInBytes">Size of one vertex in bytes.</param>
    public VertexBufferDescription(int vertexCount, int strideInBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(vertexCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strideInBytes);
        _ = checked(vertexCount * strideInBytes);
        VertexCount = vertexCount;
        StrideInBytes = strideInBytes;
    }

    /// <summary>Gets the number of vertices.</summary>
    public int VertexCount { get; }

    /// <summary>Gets the byte size of each vertex.</summary>
    public int StrideInBytes { get; }

    /// <summary>Gets the expected byte size of the vertex data.</summary>
    public int DataSizeInBytes => checked(VertexCount * StrideInBytes);
}

/// <summary>
/// Describes immutable index data uploaded to an index buffer.
/// </summary>
public readonly record struct IndexBufferDescription
{
    /// <summary>Creates an index-buffer description.</summary>
    /// <param name="indexCount">Number of indices in the buffer.</param>
    /// <param name="format">Integer format used by the indices.</param>
    public IndexBufferDescription(int indexCount, IndexFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(indexCount);
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));

        IndexCount = indexCount;
        Format = format;
    }

    /// <summary>Gets the number of indices.</summary>
    public int IndexCount { get; }

    /// <summary>Gets the integer format of the indices.</summary>
    public IndexFormat Format { get; }
}

/// <summary>Specifies a backend-neutral pixel format.</summary>
public enum TextureFormat
{
    /// <summary>Single-channel, 8-bit normalized unsigned value.</summary>
    R8Unorm,
    /// <summary>Four-channel, 8-bit normalized unsigned values.</summary>
    Rgba8Unorm,
    /// <summary>Four-channel, 8-bit sRGB color.</summary>
    Rgba8Srgb
}

/// <summary>Describes a two-dimensional texture and its initial pixel data layout.</summary>
public readonly record struct TextureDescription
{
    /// <summary>Creates a texture description.</summary>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <param name="format">Backend-neutral pixel format.</param>
    public TextureDescription(int width, int height, TextureFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));

        _ = checked(width * height * BytesPerPixelFor(format));
        Width = width;
        Height = height;
        Format = format;
    }

    /// <summary>Gets the texture width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the texture height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the pixel format.</summary>
    public TextureFormat Format { get; }

    /// <summary>Gets the required byte count for tightly packed initial pixel data.</summary>
    public int DataSizeInBytes => checked(Width * Height * BytesPerPixelFor(Format));

    private static int BytesPerPixelFor(TextureFormat format) => format switch
    {
        TextureFormat.R8Unorm => 1,
        TextureFormat.Rgba8Unorm or TextureFormat.Rgba8Srgb => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}

/// <summary>Identifies a backend-provided shader program contract.</summary>
public enum ShaderProgramKind
{
    /// <summary>Basic vertex-color and directional-light shading.</summary>
    BasicLit,
    /// <summary>Unlit material-color shading.</summary>
    Unlit,
    /// <summary>Unlit material-color and texture shading.</summary>
    UnlitTextured
}

/// <summary>Describes a backend-neutral shader program contract without exposing shader language source.</summary>
public sealed record ShaderProgramDescription
{
    /// <summary>Creates a shader program description.</summary>
    /// <param name="kind">The shader behavior required by the renderer.</param>
    /// <param name="name">Optional diagnostic name.</param>
    public ShaderProgramDescription(ShaderProgramKind kind, string name = "")
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        ArgumentNullException.ThrowIfNull(name);
        Kind = kind;
        Name = name;
    }

    /// <summary>Gets the requested shader behavior.</summary>
    public ShaderProgramKind Kind { get; }

    /// <summary>Gets the optional diagnostic name.</summary>
    public string Name { get; }
}

/// <summary>Specifies the depth comparison performed by a render pipeline.</summary>
public enum DepthComparisonFunction
{
    /// <summary>Never passes.</summary>
    Never,
    /// <summary>Passes when the incoming depth is less.</summary>
    Less,
    /// <summary>Passes when the incoming depth is less than or equal.</summary>
    LessOrEqual,
    /// <summary>Passes when the incoming depth is equal.</summary>
    Equal,
    /// <summary>Passes when the incoming depth is greater than or equal.</summary>
    GreaterOrEqual,
    /// <summary>Passes when the incoming depth is greater.</summary>
    Greater,
    /// <summary>Always passes.</summary>
    Always
}

/// <summary>Specifies which polygon faces are culled.</summary>
public enum CullMode
{
    /// <summary>No faces are culled.</summary>
    None,
    /// <summary>Front-facing polygons are culled.</summary>
    Front,
    /// <summary>Back-facing polygons are culled.</summary>
    Back
}

/// <summary>Specifies the winding used to identify front-facing polygons.</summary>
public enum FrontFaceWinding
{
    /// <summary>Counter-clockwise vertices are front-facing.</summary>
    CounterClockwise,
    /// <summary>Clockwise vertices are front-facing.</summary>
    Clockwise
}

/// <summary>Describes fixed render state and the shader used by a pipeline.</summary>
public sealed record RenderPipelineDescription
{
    /// <summary>Creates a render-pipeline description.</summary>
    /// <param name="shaderProgram">Shader program used by this pipeline.</param>
    /// <param name="topology">Primitive assembly mode.</param>
    /// <param name="cullMode">Face culling mode.</param>
    /// <param name="frontFace">Front-face winding; Orbit3D uses counter-clockwise by default.</param>
    /// <param name="depthTestEnabled">Whether depth comparisons are enabled.</param>
    /// <param name="depthComparison">Depth comparison function.</param>
    /// <param name="depthWriteEnabled">Whether passing fragments write depth.</param>
    /// <param name="blendMode">Material blend mode.</param>
    public RenderPipelineDescription(
        IShaderProgram shaderProgram,
        PrimitiveTopology topology = PrimitiveTopology.TriangleList,
        CullMode cullMode = CullMode.Back,
        FrontFaceWinding frontFace = FrontFaceWinding.CounterClockwise,
        bool depthTestEnabled = true,
        DepthComparisonFunction depthComparison = DepthComparisonFunction.LessOrEqual,
        bool depthWriteEnabled = true,
        BlendMode blendMode = BlendMode.Opaque)
    {
        ArgumentNullException.ThrowIfNull(shaderProgram);
        if (!Enum.IsDefined(topology))
            throw new ArgumentOutOfRangeException(nameof(topology));
        if (!Enum.IsDefined(cullMode))
            throw new ArgumentOutOfRangeException(nameof(cullMode));
        if (!Enum.IsDefined(frontFace))
            throw new ArgumentOutOfRangeException(nameof(frontFace));
        if (!Enum.IsDefined(depthComparison))
            throw new ArgumentOutOfRangeException(nameof(depthComparison));
        if (!Enum.IsDefined(blendMode))
            throw new ArgumentOutOfRangeException(nameof(blendMode));

        ShaderProgram = shaderProgram;
        Topology = topology;
        CullMode = cullMode;
        FrontFace = frontFace;
        DepthTestEnabled = depthTestEnabled;
        DepthComparison = depthComparison;
        DepthWriteEnabled = depthWriteEnabled;
        BlendMode = blendMode;
    }

    /// <summary>Gets the shader program used by this pipeline.</summary>
    public IShaderProgram ShaderProgram { get; }

    /// <summary>Gets the primitive assembly mode.</summary>
    public PrimitiveTopology Topology { get; }

    /// <summary>Gets the face culling mode.</summary>
    public CullMode CullMode { get; }

    /// <summary>Gets the winding used for front faces.</summary>
    public FrontFaceWinding FrontFace { get; }

    /// <summary>Gets whether depth comparison is enabled.</summary>
    public bool DepthTestEnabled { get; }

    /// <summary>Gets the depth comparison function.</summary>
    public DepthComparisonFunction DepthComparison { get; }

    /// <summary>Gets whether depth writes are enabled.</summary>
    public bool DepthWriteEnabled { get; }

    /// <summary>Gets the material blend mode.</summary>
    public BlendMode BlendMode { get; }
}

/// <summary>Describes an off-screen color target and optional depth attachment.</summary>
public readonly record struct RenderTargetDescription
{
    /// <summary>Creates a render-target description.</summary>
    /// <param name="width">Target width in pixels.</param>
    /// <param name="height">Target height in pixels.</param>
    /// <param name="colorFormat">Color attachment format.</param>
    /// <param name="hasDepthBuffer">Whether the target includes a depth attachment.</param>
    public RenderTargetDescription(int width, int height, TextureFormat colorFormat, bool hasDepthBuffer = true)
    {
        _ = new TextureDescription(width, height, colorFormat);
        Width = width;
        Height = height;
        ColorFormat = colorFormat;
        HasDepthBuffer = hasDepthBuffer;
    }

    /// <summary>Gets the target width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the target height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the color attachment format.</summary>
    public TextureFormat ColorFormat { get; }

    /// <summary>Gets whether the target has a depth attachment.</summary>
    public bool HasDepthBuffer { get; }
}

/// <summary>Defines the pixel rectangle rendered by a renderer.</summary>
public readonly record struct Viewport
{
    /// <summary>Creates a viewport. Zero dimensions represent a suspended or not-yet-sized surface.</summary>
    /// <param name="x">Horizontal origin in pixels.</param>
    /// <param name="y">Vertical origin in pixels.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public Viewport(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the horizontal origin in pixels.</summary>
    public int X { get; }

    /// <summary>Gets the vertical origin in pixels.</summary>
    public int Y { get; }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }
}

/// <summary>
/// A device-dependent resource. It is owned by the device that created it; disposing it is deterministic and idempotent.
/// The owner disposes any resources still alive when the device itself is disposed.
/// </summary>
public interface IGraphicsResource : IDisposable
{
    /// <summary>Gets the device that created and owns this resource.</summary>
    IRenderDevice Owner { get; }

    /// <summary>Gets whether this resource has been disposed.</summary>
    bool IsDisposed { get; }
}

/// <summary>Represents a device-owned vertex buffer.</summary>
public interface IVertexBuffer : IGraphicsResource
{
    /// <summary>Gets the immutable buffer description.</summary>
    VertexBufferDescription Description { get; }
}

/// <summary>Represents a device-owned index buffer.</summary>
public interface IIndexBuffer : IGraphicsResource
{
    /// <summary>Gets the immutable buffer description.</summary>
    IndexBufferDescription Description { get; }
}

/// <summary>Represents a device-owned shader program selected by a backend-neutral behavior.</summary>
public interface IShaderProgram : IGraphicsResource
{
    /// <summary>Gets the immutable shader description.</summary>
    ShaderProgramDescription Description { get; }
}

/// <summary>Represents a device-owned two-dimensional texture.</summary>
public interface ITextureResource : IGraphicsResource
{
    /// <summary>Gets the immutable texture description.</summary>
    TextureDescription Description { get; }
}

/// <summary>Represents a device-owned render pipeline and its fixed render state.</summary>
public interface IRenderPipeline : IGraphicsResource
{
    /// <summary>Gets the immutable pipeline description.</summary>
    RenderPipelineDescription Description { get; }
}

/// <summary>Represents a device-owned off-screen color target and optional depth attachment.</summary>
public interface IRenderTarget : IGraphicsResource
{
    /// <summary>Gets the immutable render-target description.</summary>
    RenderTargetDescription Description { get; }
}

/// <summary>
/// Creates and owns GPU resources. Disposing the device deterministically disposes every resource it created;
/// disposing an individual resource more than once has no additional effect.
/// Methods must reject use after disposal and data lengths that do not match their descriptions.
/// </summary>
public interface IRenderDevice : IDisposable
{
    /// <summary>Gets whether the device has been disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Creates an immutable vertex buffer from tightly packed data.</summary>
    /// <param name="description">Vertex count and stride.</param>
    /// <param name="data">Vertex bytes; length must equal the described data size.</param>
    IVertexBuffer CreateVertexBuffer(VertexBufferDescription description, ReadOnlySpan<byte> data);

    /// <summary>Creates an immutable index buffer from tightly packed data.</summary>
    /// <param name="description">Index count and integer format.</param>
    /// <param name="data">Index bytes; length must match the described format and count.</param>
    IIndexBuffer CreateIndexBuffer(IndexBufferDescription description, ReadOnlySpan<byte> data);

    /// <summary>Creates a texture from tightly packed initial pixel data.</summary>
    /// <param name="description">Texture dimensions and pixel format.</param>
    /// <param name="data">Pixel bytes; length must equal the described data size.</param>
    ITextureResource CreateTexture(TextureDescription description, ReadOnlySpan<byte> data);

    /// <summary>Creates a backend implementation of the requested shader behavior.</summary>
    IShaderProgram CreateShaderProgram(ShaderProgramDescription description);

    /// <summary>Creates a pipeline. The pipeline and shader program must belong to this device.</summary>
    IRenderPipeline CreatePipeline(RenderPipelineDescription description);

    /// <summary>Creates an off-screen render target.</summary>
    IRenderTarget CreateRenderTarget(RenderTargetDescription description);
}

/// <summary>
/// Issues backend-neutral 3D rendering operations. The renderer borrows its device and never disposes it.
/// All bound resources must be alive and owned by <see cref="Device"/>.
/// </summary>
public interface IRenderer3D : IDisposable
{
    /// <summary>Gets the device borrowed by this renderer.</summary>
    IRenderDevice Device { get; }

    /// <summary>Gets the current viewport.</summary>
    Viewport Viewport { get; }

    /// <summary>Gets whether the renderer has been disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Updates the viewport to the origin and supplied surface dimensions.</summary>
    /// <param name="width">Surface width in pixels; zero suspends rendering.</param>
    /// <param name="height">Surface height in pixels; zero suspends rendering.</param>
    void Resize(int width, int height);

    /// <summary>Sets the current viewport.</summary>
    void SetViewport(Viewport viewport);

    /// <summary>Begins a frame and optionally selects an off-screen target.</summary>
    /// <param name="clearColor">Clear color in linear RGBA components.</param>
    /// <param name="renderTarget">Target to render to, or null for the surface target.</param>
    void BeginFrame(Vector4 clearColor, IRenderTarget? renderTarget = null);

    /// <summary>Completes the current frame.</summary>
    void EndFrame();

    /// <summary>Sets the camera view and projection matrices.</summary>
    void SetCamera(Matrix4x4 view, Matrix4x4 projection);

    /// <summary>Sets the world transform for subsequent draws.</summary>
    void SetWorldMatrix(Matrix4x4 world);

    /// <summary>Binds a render pipeline.</summary>
    void SetPipeline(IRenderPipeline pipeline);

    /// <summary>Binds CPU-side material values for subsequent draws.</summary>
    void BindMaterial(Material3D material);

    /// <summary>Binds a device texture to a non-negative material texture slot.</summary>
    void BindTexture(int slot, ITextureResource texture);

    /// <summary>Sets the lights used by subsequent draws.</summary>
    void SetLights(ReadOnlySpan<Light3D> lights);

    /// <summary>Draws indexed primitives using the topology and state of the current pipeline.</summary>
    /// <param name="vertexBuffer">Vertex buffer owned by <see cref="Device"/>.</param>
    /// <param name="indexBuffer">Index buffer owned by <see cref="Device"/>.</param>
    /// <param name="indexCount">Number of indices to draw.</param>
    /// <param name="startIndex">First index to draw.</param>
    /// <param name="baseVertex">Signed vertex offset applied to the indices.</param>
    void DrawIndexed(IVertexBuffer vertexBuffer, IIndexBuffer indexBuffer, int indexCount, int startIndex = 0, int baseVertex = 0);
}
