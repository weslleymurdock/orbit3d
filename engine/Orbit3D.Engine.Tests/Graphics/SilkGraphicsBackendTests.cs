using System.Numerics;
using Orbit3D.Engine.Graphics;
using Orbit3D.Graphics.Silk;

namespace Orbit3D.Engine.Tests.Graphics;

public class SilkGraphicsBackendTests
{
    [Fact]
    public void SilkGraphicsFactory_CreatesRendererAndResources()
    {
        using var device = SilkGraphicsFactory.CreateDevice();
        var vertices = device.CreateVertexBuffer(
            new VertexBufferDescription(3, 12),
            new byte[36]);
        var indices = device.CreateIndexBuffer(
            new IndexBufferDescription(3, IndexFormat.UInt16),
            new byte[6]);
        var texture = device.CreateTexture(
            new TextureDescription(2, 2, TextureFormat.Rgba8Unorm),
            new byte[16]);
        var shader = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit));
        var pipeline = device.CreatePipeline(new RenderPipelineDescription(shader));

        using var renderer = SilkGraphicsFactory.CreateRenderer(device);
        renderer.Resize(640, 480);
        renderer.SetPipeline(pipeline);
        renderer.SetCamera(Matrix4x4.Identity, Matrix4x4.Identity);
        renderer.SetWorldMatrix(Matrix4x4.Identity);
        renderer.BindMaterial(new Material3D());
        renderer.BindTexture(0, texture);
        renderer.SetLights([new Light3D()]);

        renderer.BeginFrame(Vector4.Zero);
        renderer.DrawIndexed(vertices, indices, 3);
        renderer.EndFrame();

        Assert.Equal(new Viewport(0, 0, 640, 480), renderer.Viewport);
        Assert.Equal(0, vertices.Description.VertexCount % 3);
    }

    [Fact]
    public void GameSceneView3D_ExposesSurfaceAndSceneOwner()
    {
        var view = new GameSceneView3D();

        Assert.NotNull(view.Surface);
        Assert.Null(view.Scene);
        Assert.Null(view.Device);
        Assert.Null(view.Renderer);
    }
}
