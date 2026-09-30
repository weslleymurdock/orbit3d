using System.Numerics;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Uploads an Orbit model once per graphics context and draws its triangle-list meshes on a Silk surface.
/// </summary>
public sealed class SilkModelDiagnostic : IDisposable
{
    private const float DiagnosticScale = 1.6f;
    private static readonly Matrix4x4 DiagnosticWorld =
        Matrix4x4.CreateFromYawPitchRoll(MathF.PI / 5f, -MathF.PI / 12f, 0f) *
        Matrix4x4.CreateScale(DiagnosticScale);

    private readonly SilkGraphicsSurface surface;
    private SilkRenderDevice? device;
    private IRenderer3D? renderer;
    private SilkMeshBuffers[] gpuMeshes = [];
    private Model3D? latestModel;
    private Model3D? pendingModel;
    private Model3D? uploadedModel;
    private int surfaceWidth;
    private int surfaceHeight;
    private bool disposed;

    /// <summary>Creates a model renderer attached to the supplied native graphics surface.</summary>
    public SilkModelDiagnostic(SilkGraphicsSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        this.surface = surface;
        surface.ContextCreated += OnContextCreated;
        surface.ContextLost += OnContextLost;
        surface.SurfaceResized += OnSurfaceResized;
        surface.RenderFrame += OnRenderFrame;
    }

    /// <summary>Gets whether GPU resources are initialized for the current surface context.</summary>
    public bool IsInitialized => device is not null;

    /// <summary>Queues an imported Orbit model for upload on the graphics-context thread.</summary>
    public void SetModel(Model3D model)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(model);
        if (model.Meshes.Count == 0)
            throw new ArgumentException("The model contains no meshes.", nameof(model));
        if (model.Meshes.Any(mesh => !mesh.IsValid() || mesh.Topology != PrimitiveTopology.TriangleList))
            throw new ArgumentException("The model must contain valid triangle-list meshes.", nameof(model));

        Volatile.Write(ref latestModel, model);
        Interlocked.Exchange(ref pendingModel, model);
    }

    /// <summary>Detaches from the surface and releases resources associated with the active context.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        surface.ContextCreated -= OnContextCreated;
        surface.ContextLost -= OnContextLost;
        surface.SurfaceResized -= OnSurfaceResized;
        surface.RenderFrame -= OnRenderFrame;
        ReleaseGpuResources();
    }

    private void OnContextCreated(object? sender, SilkGraphicsContextEventArgs args)
    {
        ReleaseGpuResources();
        device = (SilkRenderDevice)SilkGraphicsFactory.CreateDevice(args.Context);
        renderer = (SilkRenderer3D)SilkGraphicsFactory.CreateRenderer(device);
        var shader = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit, "Silk imported mesh diagnostic"));
        var pipeline = device.CreatePipeline(new RenderPipelineDescription(shader, cullMode: CullMode.None));
        renderer.SetPipeline(pipeline);
        renderer.BindMaterial(new Material3D { BaseColor = new Vector4(0.16f, 0.78f, 0.66f, 1f) });
        renderer.SetLights([new Light3D { Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, -1f)), Color = Vector3.One, Intensity = 1f }]);
        renderer.SetWorldMatrix(DiagnosticWorld);
        UpdateCameraProjection();
        Interlocked.Exchange(ref pendingModel, Volatile.Read(ref latestModel));
    }

    private void OnContextLost(object? sender, SilkGraphicsContextEventArgs args) => ReleaseGpuResources();

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args)
    {
        surfaceWidth = Math.Max(args.Width, 1);
        surfaceHeight = Math.Max(args.Height, 1);
        renderer?.Resize(args.Width, args.Height);
        UpdateCameraProjection();
    }

    private void OnRenderFrame(object? sender, EventArgs args)
    {
        if (renderer is null)
            return;

        var model = Interlocked.Exchange(ref pendingModel, null);
        if (model is not null && !ReferenceEquals(model, uploadedModel))
            UploadModel(model);

        renderer.SetWorldMatrix(DiagnosticWorld);
        renderer.BeginFrame(new Vector4(0.035f, 0.055f, 0.075f, 1f));
        foreach (var mesh in gpuMeshes)
            renderer.DrawIndexed(mesh.VertexBuffer, mesh.IndexBuffer, mesh.IndexBuffer.Description.IndexCount);
        renderer.EndFrame();
    }

    private void UpdateCameraProjection()
    {
        if (renderer is null)
            return;

        var width = surfaceWidth > 0 ? surfaceWidth : 1280;
        var height = surfaceHeight > 0 ? surfaceHeight : 720;
        var aspectRatio = width / (float)height;
        var cameraPosition = new Vector3(0f, 0.5f, 2.4f);
        var target = Vector3.Zero;
        var view = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.5f, aspectRatio, 0.05f, 50f);
        renderer.SetCamera(view, projection);
        renderer.SetWorldMatrix(DiagnosticWorld);
    }

    private void UploadModel(Model3D model)
    {
        var resources = new List<SilkMeshBuffers>(model.Meshes.Count);
        try
        {
            foreach (var mesh in model.Meshes)
                resources.Add(SilkMeshBuffers.Create(device!, mesh));
        }
        catch
        {
            foreach (var resource in resources)
                resource.Dispose();
            throw;
        }

        foreach (var resource in gpuMeshes)
            resource.Dispose();
        gpuMeshes = resources.ToArray();
        uploadedModel = model;
    }

    private void ReleaseGpuResources()
    {
        renderer?.Dispose();
        device?.Dispose();
        foreach (var mesh in gpuMeshes)
            mesh.Dispose();
        gpuMeshes = [];
        renderer = null;
        device = null;
        uploadedModel = null;
    }
}
