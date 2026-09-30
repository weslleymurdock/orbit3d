using System.Numerics;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Small indexed-triangle diagnostic that exercises the Silk surface lifecycle and GPU draw path.
/// Attach it to a <see cref="SilkGraphicsSurface"/> in a MAUI sample or host.
/// </summary>
public sealed class SilkTriangleDiagnostic : IDisposable
{
    private readonly SilkGraphicsSurface _surface;
    private SilkRenderDevice? _device;
    private IRenderer3D? _renderer;
    private SilkMeshBuffers? _mesh;
    private bool _disposed;

    /// <summary>Creates a diagnostic renderer attached to the supplied native surface.</summary>
    public SilkTriangleDiagnostic(SilkGraphicsSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        surface.ContextCreated += OnContextCreated;
        surface.ContextLost += OnContextLost;
        surface.SurfaceResized += OnSurfaceResized;
        surface.RenderFrame += OnRenderFrame;
    }

    /// <summary>Gets whether GPU resources are initialized for the current surface context.</summary>
    public bool IsInitialized => _device is not null;

    /// <summary>Detaches from the surface and releases resources when their context is still current.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _surface.ContextCreated -= OnContextCreated;
        _surface.ContextLost -= OnContextLost;
        _surface.SurfaceResized -= OnSurfaceResized;
        _surface.RenderFrame -= OnRenderFrame;
        ReleaseResources();
    }

    private void OnContextCreated(object? sender, SilkGraphicsContextEventArgs args)
    {
        ReleaseResources();
        _device = (SilkRenderDevice)SilkGraphicsFactory.CreateDevice(args.Context);
        _renderer = (SilkRenderer3D)SilkGraphicsFactory.CreateRenderer(_device);

        var mesh = new Mesh3D
        {
            Positions =
            [
                new Vector3(-0.65f, -0.55f, 0f),
                new Vector3(0.65f, -0.55f, 0f),
                new Vector3(0f, 0.65f, 0f)
            ],
            Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
            Indices16 = [0, 1, 2],
            IndexFormat = IndexFormat.UInt16,
            Topology = PrimitiveTopology.TriangleList
        };
        _mesh = SilkMeshBuffers.Create(_device, mesh);
        var shader = _device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit, "Silk triangle diagnostic"));
        var pipeline = _device.CreatePipeline(new RenderPipelineDescription(shader, cullMode: CullMode.None));
        _renderer.SetPipeline(pipeline);
        _renderer.SetCamera(Matrix4x4.Identity, Matrix4x4.Identity);
        _renderer.BindMaterial(new Material3D { BaseColor = new Vector4(0.16f, 0.78f, 0.66f, 1f) });
        _renderer.SetLights([new Light3D { Direction = -Vector3.UnitZ }]);
        if (_surface.Width > 0 && _surface.Height > 0)
            _renderer.Resize((int)Math.Round(_surface.Width), (int)Math.Round(_surface.Height));
    }

    private void OnContextLost(object? sender, SilkGraphicsContextEventArgs args) => ReleaseResources();

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args) =>
        _renderer?.Resize(args.Width, args.Height);

    private void OnRenderFrame(object? sender, EventArgs args)
    {
        if (_renderer is null || _mesh is null)
            return;

        _renderer.BeginFrame(new Vector4(0.035f, 0.055f, 0.075f, 1f));
        _renderer.DrawIndexed(_mesh.VertexBuffer, _mesh.IndexBuffer, 3);
        _renderer.EndFrame();
    }

    private void ReleaseResources()
    {
        _renderer?.Dispose();
        _device?.Dispose();
        _mesh?.Dispose();
        _mesh = null;
        _renderer = null;
        _device = null;
    }
}
