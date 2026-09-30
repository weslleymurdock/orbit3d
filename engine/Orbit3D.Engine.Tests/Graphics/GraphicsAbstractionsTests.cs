using System.Numerics;
using Orbit3D.Engine.Graphics;

namespace Orbit3D.Engine.Tests.Graphics;

public class GraphicsAbstractionsTests
{
    [Fact]
    public void BufferDescriptions_ExposeValidatedLayouts()
    {
        using var device = new FakeRenderDevice();
        var vertexDescription = new VertexBufferDescription(3, 20);
        var indexDescription = new IndexBufferDescription(6, IndexFormat.UInt16);

        Assert.Equal(60, vertexDescription.DataSizeInBytes);
        Assert.Equal(3, vertexDescription.VertexCount);
        Assert.Equal(20, vertexDescription.StrideInBytes);
        Assert.Equal(6, indexDescription.IndexCount);
        Assert.Equal(IndexFormat.UInt16, indexDescription.Format);
        Assert.Throws<ArgumentOutOfRangeException>(() => new VertexBufferDescription(0, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndexBufferDescription(3, (IndexFormat)99));
        Assert.Throws<OverflowException>(() => new VertexBufferDescription(int.MaxValue, 2));
        Assert.Throws<ArgumentException>(() => device.CreateVertexBuffer(default, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void TextureDescription_ReportsPackedDataSizeAndRejectsInvalidDimensions()
    {
        var redTexture = new TextureDescription(8, 4, TextureFormat.R8Unorm);
        var colorTexture = new TextureDescription(8, 4, TextureFormat.Rgba8Srgb);

        Assert.Equal(32, redTexture.DataSizeInBytes);
        Assert.Equal(128, colorTexture.DataSizeInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureDescription(0, 4, TextureFormat.Rgba8Unorm));
        Assert.Throws<OverflowException>(() => new TextureDescription(int.MaxValue, 2, TextureFormat.Rgba8Unorm));
        using var device = new FakeRenderDevice();
        Assert.Throws<ArgumentException>(() => device.CreateTexture(default, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void PipelineDescription_UsesOrbitCoordinateAndDepthDefaults()
    {
        using var device = new FakeRenderDevice();
        var shader = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit));
        var pipeline = new RenderPipelineDescription(shader);

        Assert.Equal(PrimitiveTopology.TriangleList, pipeline.Topology);
        Assert.Equal(CullMode.Back, pipeline.CullMode);
        Assert.Equal(FrontFaceWinding.CounterClockwise, pipeline.FrontFace);
        Assert.True(pipeline.DepthTestEnabled);
        Assert.Equal(DepthComparisonFunction.LessOrEqual, pipeline.DepthComparison);
        Assert.True(pipeline.DepthWriteEnabled);
        Assert.Equal(BlendMode.Opaque, pipeline.BlendMode);
    }

    [Fact]
    public void ShaderDescription_AcceptsCombinedLitAndTexturedRuntimeShader()
    {
        var description = new ShaderProgramDescription(ShaderProgramKind.BasicLitTextured, "lit-textured");

        Assert.Equal(ShaderProgramKind.BasicLitTextured, description.Kind);
        Assert.Equal("lit-textured", description.Name);
    }

    [Fact]
    public void Device_CreatesOwnedResourcesAndDisposesThemWithDevice()
    {
        var device = new FakeRenderDevice();
        var vertexDescription = new VertexBufferDescription(3, 12);
        var vertices = device.CreateVertexBuffer(vertexDescription, new byte[36]);
        var indices = device.CreateIndexBuffer(
            new IndexBufferDescription(3, IndexFormat.UInt32),
            new byte[12]);
        var textureDescription = new TextureDescription(2, 2, TextureFormat.Rgba8Unorm);
        var texture = device.CreateTexture(textureDescription, new byte[textureDescription.DataSizeInBytes]);
        var shaderDescription = new ShaderProgramDescription(ShaderProgramKind.UnlitTextured, "surface");
        var shader = device.CreateShaderProgram(shaderDescription);
        var pipelineDescription = new RenderPipelineDescription(shader);
        var pipeline = device.CreatePipeline(pipelineDescription);
        var targetDescription = new RenderTargetDescription(32, 24, TextureFormat.Rgba8Srgb);
        var target = device.CreateRenderTarget(targetDescription);

        Assert.Same(device, vertices.Owner);
        Assert.Same(device, indices.Owner);
        Assert.Same(device, texture.Owner);
        Assert.Same(device, shader.Owner);
        Assert.Same(device, pipeline.Owner);
        Assert.Same(device, target.Owner);
        Assert.Equal(vertexDescription, vertices.Description);
        Assert.Equal(textureDescription, texture.Description);
        Assert.Equal(shaderDescription, shader.Description);
        Assert.Equal(targetDescription, target.Description);

        device.Dispose();

        Assert.True(device.IsDisposed);
        Assert.All(new IGraphicsResource[] { vertices, indices, texture, shader, pipeline, target },
            resource => Assert.True(resource.IsDisposed));
    }

    [Fact]
    public void Device_ResourceDisposalIsIdempotentAndRejectsInvalidUsage()
    {
        using var device = new FakeRenderDevice();
        var buffer = device.CreateVertexBuffer(new VertexBufferDescription(1, 4), new byte[4]);

        buffer.Dispose();
        buffer.Dispose();

        Assert.True(buffer.IsDisposed);
        Assert.Equal(1, ((FakeResource)buffer).DisposeCount);
        Assert.Throws<ArgumentException>(() => device.CreateVertexBuffer(new VertexBufferDescription(2, 4), new byte[4]));
        Assert.Throws<ArgumentException>(() => device.CreateIndexBuffer(
            new IndexBufferDescription(2, IndexFormat.UInt16), new byte[2]));

        device.Dispose();
        Assert.Throws<ObjectDisposedException>(() => device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.Unlit)));
    }

    [Fact]
    public void Renderer_TracksViewportAndBorrowsDeviceLifecycle()
    {
        using var device = new FakeRenderDevice();
        var renderer = new FakeRenderer(device);

        renderer.Resize(640, 480);
        Assert.Equal(new Viewport(0, 0, 640, 480), renderer.Viewport);

        renderer.SetViewport(new Viewport(10, 20, 320, 200));
        Assert.Equal(new Viewport(10, 20, 320, 200), renderer.Viewport);

        renderer.Resize(0, 0);
        Assert.Equal(new Viewport(0, 0, 0, 0), renderer.Viewport);
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Resize(-1, 100));

        renderer.Dispose();
        renderer.Dispose();

        Assert.True(renderer.IsDisposed);
        Assert.False(device.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => renderer.BeginFrame(Vector4.Zero));
    }

    [Fact]
    public void Renderer_RejectsForeignAndDisposedResourcesForDrawing()
    {
        using var device = new FakeRenderDevice();
        using var otherDevice = new FakeRenderDevice();
        using var renderer = new FakeRenderer(device);
        var shader = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit));
        var pipeline = device.CreatePipeline(new RenderPipelineDescription(shader));
        var foreignShader = otherDevice.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.Unlit));
        var vertices = device.CreateVertexBuffer(new VertexBufferDescription(3, 12), new byte[36]);
        var indices = device.CreateIndexBuffer(
            new IndexBufferDescription(3, IndexFormat.UInt16),
            new byte[6]);
        var texture = device.CreateTexture(
            new TextureDescription(1, 1, TextureFormat.Rgba8Unorm), new byte[4]);

        renderer.SetPipeline(pipeline);
        renderer.BeginFrame(Vector4.Zero);
        renderer.SetCamera(Matrix4x4.Identity, Matrix4x4.Identity);
        renderer.SetWorldMatrix(Matrix4x4.Identity);
        renderer.BindMaterial(new Material3D());
        renderer.BindTexture(0, texture);
        renderer.SetLights([new Light3D()]);
        renderer.DrawIndexed(vertices, indices, 3);
        Assert.Throws<ArgumentException>(() => renderer.SetPipeline(otherDevice.CreatePipeline(new RenderPipelineDescription(foreignShader))));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.DrawIndexed(vertices, indices, 4));

        shader.Dispose();
        Assert.Throws<ObjectDisposedException>(() => renderer.DrawIndexed(vertices, indices, 3));
        vertices.Dispose();
        Assert.Throws<ObjectDisposedException>(() => renderer.DrawIndexed(vertices, indices, 3));
        renderer.EndFrame();
    }

    private sealed class FakeRenderDevice : IRenderDevice
    {
        private readonly List<FakeResource> resources = [];

        public bool IsDisposed { get; private set; }

        public IVertexBuffer CreateVertexBuffer(VertexBufferDescription description, ReadOnlySpan<byte> data)
        {
            EnsureActive();
            if (description.VertexCount <= 0 || description.StrideInBytes <= 0)
                throw new ArgumentException("Vertex buffer description is invalid.", nameof(description));
            ValidateDataLength(description.DataSizeInBytes, data.Length);
            return Add(new FakeVertexBuffer(this, description));
        }

        public IIndexBuffer CreateIndexBuffer(IndexBufferDescription description, ReadOnlySpan<byte> data)
        {
            EnsureActive();
            if (description.IndexCount <= 0 || !Enum.IsDefined(description.Format))
                throw new ArgumentException("Index buffer description is invalid.", nameof(description));
            var indexSize = description.Format == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint);
            ValidateDataLength(checked(description.IndexCount * indexSize), data.Length);
            return Add(new FakeIndexBuffer(this, description));
        }

        public ITextureResource CreateTexture(TextureDescription description, ReadOnlySpan<byte> data)
        {
            EnsureActive();
            if (description.Width <= 0 || description.Height <= 0 || !Enum.IsDefined(description.Format))
                throw new ArgumentException("Texture description is invalid.", nameof(description));
            ValidateDataLength(description.DataSizeInBytes, data.Length);
            return Add(new FakeTexture(this, description));
        }

        public IShaderProgram CreateShaderProgram(ShaderProgramDescription description)
        {
            EnsureActive();
            ArgumentNullException.ThrowIfNull(description);
            return Add(new FakeShader(this, description));
        }

        public IRenderPipeline CreatePipeline(RenderPipelineDescription description)
        {
            EnsureActive();
            ArgumentNullException.ThrowIfNull(description);
            ValidateOwned(description.ShaderProgram);
            return Add(new FakePipeline(this, description));
        }

        public IRenderTarget CreateRenderTarget(RenderTargetDescription description)
        {
            EnsureActive();
            if (description.Width <= 0 || description.Height <= 0)
                throw new ArgumentException("Render target description is invalid.", nameof(description));
            _ = new TextureDescription(description.Width, description.Height, description.ColorFormat);
            return Add(new FakeRenderTarget(this, description));
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            foreach (var resource in resources)
                resource.Dispose();
        }

        public void ValidateOwned(IGraphicsResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (!ReferenceEquals(resource.Owner, this))
                throw new ArgumentException("Resource belongs to another device.", nameof(resource));
            if (resource.IsDisposed)
                throw new ObjectDisposedException(resource.GetType().Name);
        }

        private T Add<T>(T resource) where T : FakeResource
        {
            resources.Add(resource);
            return resource;
        }

        private void EnsureActive()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }

        private static void ValidateDataLength(int expected, int actual)
        {
            if (expected != actual)
                throw new ArgumentException("Data length does not match the resource description.");
        }
    }

    private abstract class FakeResource : IGraphicsResource
    {
        protected FakeResource(FakeRenderDevice owner) => Owner = owner;

        public IRenderDevice Owner { get; }
        public bool IsDisposed { get; private set; }
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            DisposeCount++;
        }
    }

    private sealed class FakeVertexBuffer(FakeRenderDevice owner, VertexBufferDescription description)
        : FakeResource(owner), IVertexBuffer
    {
        public VertexBufferDescription Description { get; } = description;
    }

    private sealed class FakeIndexBuffer(FakeRenderDevice owner, IndexBufferDescription description)
        : FakeResource(owner), IIndexBuffer
    {
        public IndexBufferDescription Description { get; } = description;
    }

    private sealed class FakeTexture(FakeRenderDevice owner, TextureDescription description)
        : FakeResource(owner), ITextureResource
    {
        public TextureDescription Description { get; } = description;
    }

    private sealed class FakeShader(FakeRenderDevice owner, ShaderProgramDescription description)
        : FakeResource(owner), IShaderProgram
    {
        public ShaderProgramDescription Description { get; } = description;
    }

    private sealed class FakePipeline(FakeRenderDevice owner, RenderPipelineDescription description)
        : FakeResource(owner), IRenderPipeline
    {
        public RenderPipelineDescription Description { get; } = description;
    }

    private sealed class FakeRenderTarget(FakeRenderDevice owner, RenderTargetDescription description)
        : FakeResource(owner), IRenderTarget
    {
        public RenderTargetDescription Description { get; } = description;
    }

    private sealed class FakeRenderer : IRenderer3D
    {
        private readonly IRenderDevice device;
        private IRenderPipeline? currentPipeline;

        public FakeRenderer(IRenderDevice device)
        {
            this.device = device;
            Device = device;
        }

        public IRenderDevice Device { get; }
        public Viewport Viewport { get; private set; }
        public bool IsDisposed { get; private set; }
        private bool IsFrameActive { get; set; }

        public void Resize(int width, int height) => SetViewport(new Viewport(0, 0, width, height));

        public void SetViewport(Viewport viewport)
        {
            EnsureActive();
            Viewport = viewport;
        }

        public void BeginFrame(Vector4 clearColor, IRenderTarget? renderTarget = null)
        {
            EnsureActive();
            if (IsFrameActive)
                throw new InvalidOperationException("A frame is already active.");
            if (renderTarget is not null)
                ValidateOwned(renderTarget);
            IsFrameActive = true;
        }

        public void EndFrame()
        {
            EnsureActive();
            if (!IsFrameActive)
                throw new InvalidOperationException("No frame is active.");
            IsFrameActive = false;
        }

        public void SetCamera(Matrix4x4 view, Matrix4x4 projection) => EnsureActive();

        public void SetWorldMatrix(Matrix4x4 world) => EnsureActive();

        public void SetPipeline(IRenderPipeline pipeline)
        {
            EnsureActive();
            ValidateOwned(pipeline);
            currentPipeline = pipeline;
        }

        public void BindMaterial(Material3D material)
        {
            EnsureActive();
            ArgumentNullException.ThrowIfNull(material);
        }

        public void BindTexture(int slot, ITextureResource texture)
        {
            EnsureActive();
            ArgumentOutOfRangeException.ThrowIfNegative(slot);
            ValidateOwned(texture);
        }

        public void SetLights(ReadOnlySpan<Light3D> lights) => EnsureActive();

        public void DrawIndexed(IVertexBuffer vertexBuffer, IIndexBuffer indexBuffer, int indexCount, int startIndex = 0, int baseVertex = 0)
        {
            EnsureActive();
            if (!IsFrameActive)
                throw new InvalidOperationException("No frame is active.");
            if (currentPipeline is null)
                throw new InvalidOperationException("A render pipeline must be bound before drawing.");

            ValidateOwned(currentPipeline);
            ValidateOwned(currentPipeline.Description.ShaderProgram);
            ValidateOwned(vertexBuffer);
            ValidateOwned(indexBuffer);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(indexCount);
            ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
            if (startIndex > indexBuffer.Description.IndexCount - indexCount)
                throw new ArgumentOutOfRangeException(nameof(indexCount));
        }

        public void Dispose() => IsDisposed = true;

        private void ValidateOwned(IGraphicsResource resource)
        {
            if (device is FakeRenderDevice fakeDevice)
            {
                fakeDevice.ValidateOwned(resource);
                return;
            }

            ArgumentNullException.ThrowIfNull(resource);
            if (!ReferenceEquals(resource.Owner, device))
                throw new ArgumentException("Resource belongs to another device.", nameof(resource));
            if (resource.IsDisposed)
                throw new ObjectDisposedException(resource.GetType().Name);
        }

        private void EnsureActive()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ObjectDisposedException.ThrowIf(device.IsDisposed, device);
        }
    }
}