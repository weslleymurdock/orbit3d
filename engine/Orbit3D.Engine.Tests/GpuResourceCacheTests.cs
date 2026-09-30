using System.Numerics;
using Orbit3D.Engine.Graphics;

namespace Orbit3D.Engine.Tests;

public class GpuResourceCacheTests
{
    [Fact]
    public void Cache_ReusesResources_ForTheSameDevice_AndInvalidatesAcrossDevices()
    {
        var deviceA = new FakeRenderDevice();
        var deviceB = new FakeRenderDevice();
        var cache = new GpuResourceCache(deviceA);
        var mesh = CreateMesh();

        var first = cache.GetOrCreateMesh(mesh, (device, _) => CreateMeshResource(device));
        var second = cache.GetOrCreateMesh(mesh, (device, _) => CreateMeshResource(device));
        Assert.Same(first, second);

        cache.SetDevice(deviceB);
        var third = cache.GetOrCreateMesh(mesh, (device, _) => CreateMeshResource(device));
        Assert.NotSame(first, third);
        Assert.Same(deviceB, third.VertexBuffer.Owner);
        Assert.Same(deviceB, third.IndexBuffer.Owner);
    }

    [Fact]
    public void Cache_Invalidate_RemovesStableEntries()
    {
        var device = new FakeRenderDevice();
        var cache = new GpuResourceCache(device);
        var mesh = CreateMesh();
        var texture = new Texture2D
        {
            Name = "test",
            Width = 2,
            Height = 2,
            PixelFormat = TextureFormat.Rgba8Unorm,
            Data = new byte[16]
        };

        var meshResource = cache.GetOrCreateMesh(mesh, (renderDevice, _) => CreateMeshResource(renderDevice));
        var textureResource = cache.GetOrCreateTexture(texture, (renderDevice, _) => renderDevice.CreateTexture(new TextureDescription(2, 2, TextureFormat.Rgba8Unorm), new byte[16]));

        Assert.True(cache.TryGetMesh(mesh, out _));
        Assert.True(cache.TryGetTexture(texture, out _));

        cache.Invalidate();

        Assert.False(cache.TryGetMesh(mesh, out _));
        Assert.False(cache.TryGetTexture(texture, out _));
        Assert.Equal(meshResource.VertexBuffer.Owner, device);
        Assert.Equal(textureResource.Owner, device);
    }

    private static Mesh3D CreateMesh()
    {
        return new Mesh3D
        {
            Name = "triangle",
            Positions = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
            TextureCoordinates = [Vector2.Zero, Vector2.UnitX, Vector2.UnitY],
            Indices32 = [0u, 1u, 2u],
            IndexFormat = IndexFormat.UInt32,
            Topology = PrimitiveTopology.TriangleList
        };
    }

    private static MeshRenderResource CreateMeshResource(IRenderDevice device)
    {
        var description = new VertexBufferDescription(3, 32);
        var vertices = device.CreateVertexBuffer(description, new byte[description.DataSizeInBytes]);
        var indexDescription = new IndexBufferDescription(3, IndexFormat.UInt32);
        var indices = device.CreateIndexBuffer(indexDescription, new byte[indexDescription.IndexCount * sizeof(uint)]);
        return new MeshRenderResource(vertices, indices);
    }

    private sealed class FakeRenderDevice : IRenderDevice
    {
        public bool IsDisposed { get; private set; }

        public IVertexBuffer CreateVertexBuffer(VertexBufferDescription description, ReadOnlySpan<byte> data)
            => new FakeVertexBuffer(this, description, data.ToArray());

        public IIndexBuffer CreateIndexBuffer(IndexBufferDescription description, ReadOnlySpan<byte> data)
            => new FakeIndexBuffer(this, description, data.ToArray());

        public ITextureResource CreateTexture(TextureDescription description, ReadOnlySpan<byte> data)
            => new FakeTextureResource(this, description, data.ToArray());

        public IShaderProgram CreateShaderProgram(ShaderProgramDescription description)
            => new FakeShaderProgram(this, description);

        public IRenderPipeline CreatePipeline(RenderPipelineDescription description)
            => new FakeRenderPipeline(this, description);

        public IRenderTarget CreateRenderTarget(RenderTargetDescription description)
            => new FakeRenderTarget(this, description);

        public void Dispose() => IsDisposed = true;
    }

    private abstract class FakeResource(IRenderDevice owner) : IGraphicsResource
    {
        public IRenderDevice Owner { get; } = owner;
        public bool IsDisposed { get; protected set; }

        public void Dispose()
        {
            if (!IsDisposed)
                IsDisposed = true;
        }
    }

    private sealed class FakeVertexBuffer(IRenderDevice owner, VertexBufferDescription description, byte[] data)
        : FakeResource(owner), IVertexBuffer
    {
        public VertexBufferDescription Description { get; } = description;
        public byte[] Data { get; } = data;
    }

    private sealed class FakeIndexBuffer(IRenderDevice owner, IndexBufferDescription description, byte[] data)
        : FakeResource(owner), IIndexBuffer
    {
        public IndexBufferDescription Description { get; } = description;
        public byte[] Data { get; } = data;
    }

    private sealed class FakeTextureResource(IRenderDevice owner, TextureDescription description, byte[] data)
        : FakeResource(owner), ITextureResource
    {
        public TextureDescription Description { get; } = description;
        public byte[] Data { get; } = data;
    }

    private sealed class FakeShaderProgram(IRenderDevice owner, ShaderProgramDescription description)
        : FakeResource(owner), IShaderProgram
    {
        public ShaderProgramDescription Description { get; } = description;
    }

    private sealed class FakeRenderPipeline(IRenderDevice owner, RenderPipelineDescription description)
        : FakeResource(owner), IRenderPipeline
    {
        public RenderPipelineDescription Description { get; } = description;
    }

    private sealed class FakeRenderTarget(IRenderDevice owner, RenderTargetDescription description)
        : FakeResource(owner), IRenderTarget
    {
        public RenderTargetDescription Description { get; } = description;
    }
}
