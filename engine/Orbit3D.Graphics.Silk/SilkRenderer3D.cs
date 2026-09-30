using System.Numerics;
using System.Runtime.InteropServices;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;
using Silk.NET.OpenGL;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Implements the backend-neutral renderer contract while keeping the actual graphics API behind the device abstraction.
/// </summary>
public sealed class SilkRenderer3D : IRenderer3D
{
    private readonly Dictionary<int, ITextureResource> boundTextures = [];
    private readonly RenderMetricsTracker renderMetricsTracker = new();
    private Vector3 lightDirection = new(-0.3f, -1f, -1f);
    private Vector3 lightColor = Vector3.One;
    private Material3D? material;
    private IRenderPipeline? currentPipeline;
    private Matrix4x4 view = Matrix4x4.Identity;
    private Matrix4x4 projection = Matrix4x4.Identity;
    private Matrix4x4 world = Matrix4x4.Identity;
    private IRenderTarget? activeTarget;

    private bool frameActive;

    /// <summary>
    /// Initializes a renderer using the supplied device.
    /// </summary>
    public SilkRenderer3D(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Device = device;
        Viewport = new Viewport(0, 0, 0, 0);
    }

    /// <inheritdoc />
    public IRenderDevice Device { get; }

    /// <inheritdoc />
    public Viewport Viewport { get; private set; }

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the most recently collected frame metrics.
    /// </summary>
    public RenderMetrics Metrics => renderMetricsTracker.Current;

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        EnsureActive();
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport dimensions must be non-negative.");

        SetViewport(new Viewport(0, 0, width, height));
    }

    /// <inheritdoc />
    public void SetViewport(Viewport viewport)
    {
        EnsureActive();
        Viewport = viewport;
        var device = GetSilkDevice();
        device.EnsureContextCurrent();
        if (viewport.Width > 0 && viewport.Height > 0)
            device.Context.Api.Viewport(viewport.X, viewport.Y, (uint)viewport.Width, (uint)viewport.Height);
    }

    /// <inheritdoc />
    public void BeginFrame(Vector4 clearColor, IRenderTarget? renderTarget = null)
    {
        EnsureActive();
        if (frameActive)
            throw new InvalidOperationException("A frame is already active.");

        renderMetricsTracker.BeginFrame();
        activeTarget = renderTarget;
        if (renderTarget is not null)
            ValidateOwned(renderTarget);

        var device = GetSilkDevice();
        device.EnsureContextCurrent();
        var gl = device.Context.Api;
        var target = renderTarget as SilkRenderDevice.SilkRenderTarget;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, target?.Framebuffer ?? 0);
        var activeViewport = target is null
            ? Viewport
            : new Viewport(0, 0, target.Description.Width, target.Description.Height);
        if (activeViewport.Width > 0 && activeViewport.Height > 0)
            gl.Viewport(activeViewport.X, activeViewport.Y, (uint)activeViewport.Width, (uint)activeViewport.Height);
        gl.DepthMask(true);
        gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        frameActive = true;
    }

    /// <inheritdoc />
    public void EndFrame()
    {
        EnsureActive();
        if (!frameActive)
            throw new InvalidOperationException("No frame is active.");

        var device = GetSilkDevice();
        device.EnsureContextCurrent();
        var shouldPresent = activeTarget is null;
        activeTarget = null;
        frameActive = false;
        device.Context.Api.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        renderMetricsTracker.CompleteFrame();
        if (shouldPresent)
            device.Context.Present();
    }

    /// <inheritdoc />
    public void SetCamera(Matrix4x4 view, Matrix4x4 projection)
    {
        EnsureActive();
        this.view = view;
        this.projection = projection;
    }

    /// <inheritdoc />
    public void SetWorldMatrix(Matrix4x4 world)
    {
        EnsureActive();
        this.world = world;
    }

    /// <inheritdoc />
    public void SetPipeline(IRenderPipeline pipeline)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(pipeline);
        ValidateOwned(pipeline);
        currentPipeline = pipeline;
    }

    /// <inheritdoc />
    public void BindMaterial(Material3D material)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(material);
        this.material = material;
    }

    /// <inheritdoc />
    public void BindTexture(int slot, ITextureResource texture)
    {
        EnsureActive();
        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot));

        if (texture is null)
        {
            boundTextures.Remove(slot);
            return;
        }

        ValidateOwned(texture);
        boundTextures[slot] = texture;
    }

    /// <inheritdoc />
    public void SetLights(ReadOnlySpan<Light3D> lights)
    {
        EnsureActive();
        foreach (var light in lights)
        {
            if (light.Type != LightType.Directional)
                continue;

            lightDirection = light.Direction;
            if (lightDirection.LengthSquared() > 0f)
                lightDirection = Vector3.Normalize(lightDirection);
            lightColor = light.Color * light.Intensity;
            return;
        }

        lightDirection = new Vector3(-0.3f, -1f, -1f);
        lightColor = Vector3.One;
    }

    /// <inheritdoc />
    public void DrawIndexed(IVertexBuffer vertexBuffer, IIndexBuffer indexBuffer, int indexCount, int startIndex = 0, int baseVertex = 0)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(vertexBuffer);
        ArgumentNullException.ThrowIfNull(indexBuffer);
        ValidateOwned(vertexBuffer);
        ValidateOwned(indexBuffer);

        if (currentPipeline is null)
            throw new InvalidOperationException("No render pipeline has been bound.");
        if (!frameActive)
            throw new InvalidOperationException("DrawIndexed must be called between BeginFrame and EndFrame.");

        if (indexCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(indexCount));

        if (startIndex < 0 || startIndex >= indexBuffer.Description.IndexCount)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        var maxCount = indexBuffer.Description.IndexCount - startIndex;
        if (indexCount > maxCount)
            throw new ArgumentOutOfRangeException(nameof(indexCount));

        if (baseVertex < 0 || baseVertex >= vertexBuffer.Description.VertexCount)
            throw new ArgumentOutOfRangeException(nameof(baseVertex));

        var device = GetSilkDevice();
        device.EnsureContextCurrent();
        var gl = device.Context.Api;
        var vertices = (SilkRenderDevice.SilkVertexBuffer)vertexBuffer;
        var indices = (SilkRenderDevice.SilkIndexBuffer)indexBuffer;
        var pipeline = (SilkRenderDevice.SilkRenderPipeline)currentPipeline;
        ValidateOwned(pipeline);
        ValidateOwned(pipeline.Description.ShaderProgram);
        var shader = (SilkRenderDevice.SilkShaderProgram)pipeline.Description.ShaderProgram;

        ApplyPipelineState(gl, pipeline.Description);
        gl.UseProgram(shader.Handle);
        SetMatrix(gl, shader.Handle, "uModel", world);
        SetMatrix(gl, shader.Handle, "uView", view);
        SetMatrix(gl, shader.Handle, "uProjection", projection);

        var materialColor = material?.BaseColor ?? Vector4.One;
        if (material is not null)
            materialColor.W *= material.Opacity;
        SetVector4(gl, shader.Handle, "uBaseColor", materialColor);

        SetVector3(gl, shader.Handle, "uLightDirection", lightDirection);
        SetVector3(gl, shader.Handle, "uLightColor", lightColor);

        var boundTexture = boundTextures.GetValueOrDefault(0);
        if (boundTexture is not null)
            ValidateOwned(boundTexture);
        var texture = boundTexture as SilkRenderDevice.SilkTextureResource;
        var textureLocation = gl.GetUniformLocation(shader.Handle, "uBaseTexture");
        if (textureLocation >= 0)
        {
            gl.Uniform1(textureLocation, 0);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, texture?.Handle ?? 0);
        }
        var useTextureLocation = gl.GetUniformLocation(shader.Handle, "uUseTexture");
        if (useTextureLocation >= 0)
            gl.Uniform1(useTextureLocation, texture is null ? 0 : 1);

        gl.BindVertexArray(vertices.VertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertices.Handle);
        SilkRenderDevice.ConfigureVertexLayout(gl, vertices.Description.StrideInBytes, baseVertex);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indices.Handle);
        var indexOffset = checked((nint)(startIndex * (indices.Description.Format == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint))));
        // Pass the element-buffer byte offset as a native pointer value, not by reference as managed index data.
        device.DrawElements(
            ToPrimitiveType(pipeline.Description.Topology),
            (uint)indexCount,
            indices.Description.Format == IndexFormat.UInt16 ? DrawElementsType.UnsignedShort : DrawElementsType.UnsignedInt,
            indexOffset);
        renderMetricsTracker.RecordDraw(indexCount, indexCount / 3);
        gl.BindVertexArray(0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        currentPipeline = null;
        boundTextures.Clear();
        material = null;
        activeTarget = null;
    }

    private void EnsureActive()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SilkRenderer3D));
    }

    private void ValidateOwned(IGraphicsResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!ReferenceEquals(resource.Owner, Device))
            throw new ArgumentException("Resource belongs to another device.", nameof(resource));
        if (resource.IsDisposed)
            throw new ObjectDisposedException(resource.GetType().Name);
    }

    private SilkRenderDevice GetSilkDevice() => Device as SilkRenderDevice
        ?? throw new InvalidOperationException("SilkRenderer3D requires a SilkRenderDevice.");

    private static void ApplyPipelineState(GL gl, RenderPipelineDescription description)
    {
        SetCapability(gl, EnableCap.DepthTest, description.DepthTestEnabled);
        gl.DepthFunc(description.DepthComparison switch
        {
            DepthComparisonFunction.Never => DepthFunction.Never,
            DepthComparisonFunction.Less => DepthFunction.Less,
            DepthComparisonFunction.LessOrEqual => DepthFunction.Lequal,
            DepthComparisonFunction.Equal => DepthFunction.Equal,
            DepthComparisonFunction.GreaterOrEqual => DepthFunction.Gequal,
            DepthComparisonFunction.Greater => DepthFunction.Greater,
            DepthComparisonFunction.Always => DepthFunction.Always,
            _ => throw new ArgumentOutOfRangeException(nameof(description))
        });
        gl.DepthMask(description.DepthWriteEnabled);

        if (description.CullMode == CullMode.None)
            SetCapability(gl, EnableCap.CullFace, false);
        else
        {
            SetCapability(gl, EnableCap.CullFace, true);
            gl.CullFace(description.CullMode == CullMode.Back ? TriangleFace.Back : TriangleFace.Front);
        }
        gl.FrontFace(description.FrontFace == FrontFaceWinding.CounterClockwise
            ? FrontFaceDirection.Ccw
            : FrontFaceDirection.CW);

        var blend = description.BlendMode == Orbit3D.Engine.BlendMode.Blend;
        SetCapability(gl, EnableCap.Blend, blend);
        if (blend)
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private static void SetCapability(GL gl, EnableCap capability, bool enabled)
    {
        if (enabled)
            gl.Enable(capability);
        else
            gl.Disable(capability);
    }

    private static PrimitiveType ToPrimitiveType(PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.PointList => PrimitiveType.Points,
        PrimitiveTopology.LineList => PrimitiveType.Lines,
        PrimitiveTopology.LineStrip => PrimitiveType.LineStrip,
        PrimitiveTopology.TriangleList => PrimitiveType.Triangles,
        PrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
        _ => throw new ArgumentOutOfRangeException(nameof(topology))
    };

    private static void SetMatrix(GL gl, uint program, string name, Matrix4x4 matrix)
    {
        var location = gl.GetUniformLocation(program, name);
        if (location < 0)
            return;

        Span<float> values = stackalloc float[16];
        values[0] = matrix.M11; values[1] = matrix.M12; values[2] = matrix.M13; values[3] = matrix.M14;
        values[4] = matrix.M21; values[5] = matrix.M22; values[6] = matrix.M23; values[7] = matrix.M24;
        values[8] = matrix.M31; values[9] = matrix.M32; values[10] = matrix.M33; values[11] = matrix.M34;
        values[12] = matrix.M41; values[13] = matrix.M42; values[14] = matrix.M43; values[15] = matrix.M44;
        gl.UniformMatrix4(location, 1, false, values);
    }

    private static void SetVector3(GL gl, uint program, string name, Vector3 value)
    {
        var location = gl.GetUniformLocation(program, name);
        if (location >= 0)
            gl.Uniform3(location, value.X, value.Y, value.Z);
    }

    private static void SetVector4(GL gl, uint program, string name, Vector4 value)
    {
        var location = gl.GetUniformLocation(program, name);
        if (location >= 0)
            gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }
}
