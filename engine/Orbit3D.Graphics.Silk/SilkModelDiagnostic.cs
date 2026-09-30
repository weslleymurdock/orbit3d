using System.Numerics;
#if ANDROID
using Android.Util;
#endif
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
    private readonly object lifecycleLock = new();
    private SilkRenderDevice? device;
    private IRenderer3D? renderer;
    private SilkMeshBuffers[] gpuMeshes = [];
    private Model3D? currentModel;
    private Model3D? latestModel;
    private Model3D? pendingModel;
    private Model3D? uploadedModel;
    private int surfaceWidth;
    private int surfaceHeight;
    private Matrix4x4 viewMatrix = Matrix4x4.Identity;
    private Matrix4x4 projectionMatrix = Matrix4x4.Identity;
    private bool disposed;
    private bool firstDrawReported;
    private DiagnosticRenderState state = DiagnosticRenderState.NoContext;

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
    public bool IsInitialized => device is not null && renderer is not null &&
        state is DiagnosticRenderState.ResourcesReady or DiagnosticRenderState.Rendering;

    /// <summary>Raised with render diagnostics from the thread that produced the status.</summary>
    public event Action<string>? StatusChanged;

    internal DiagnosticRenderState State => state;

    /// <summary>Queues an imported Orbit model for upload on the graphics-context thread.</summary>
    public void SetModel(Model3D model)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(model);
        if (model.Meshes.Count == 0)
            throw new ArgumentException("The model contains no meshes.", nameof(model));
        if (model.Meshes.Any(mesh => !mesh.IsValid() || mesh.Topology != PrimitiveTopology.TriangleList))
            throw new ArgumentException("The model must contain valid triangle-list meshes.", nameof(model));

        lock (lifecycleLock)
        {
            currentModel = model;
            latestModel = model;
            pendingModel = model;
            state = state == DiagnosticRenderState.Disposed ? DiagnosticRenderState.Disposed : DiagnosticRenderState.ContextReady;
            ReportStatus($"Model queued: meshes={model.Meshes.Count}; {string.Join("; ", model.Meshes.Select(mesh => $"vertices={mesh.Positions?.Length ?? 0}, indices={mesh.Indices16?.Length ?? mesh.Indices32?.Length ?? 0}, normals={mesh.Normals?.Length ?? 0}"))}");
        }
    }

    /// <summary>Detaches from the surface and releases resources associated with the active context.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        state = DiagnosticRenderState.Disposed;
        surface.ContextCreated -= OnContextCreated;
        surface.ContextLost -= OnContextLost;
        surface.SurfaceResized -= OnSurfaceResized;
        surface.RenderFrame -= OnRenderFrame;
        ReleaseGpuResources();
    }

    private void OnContextCreated(object? sender, SilkGraphicsContextEventArgs args)
    {
        lock (lifecycleLock)
        {
            state = DiagnosticRenderState.ContextReady;
            ReleaseGpuResources();
            device = (SilkRenderDevice)SilkGraphicsFactory.CreateDevice(args.Context);
            renderer = (SilkRenderer3D)SilkGraphicsFactory.CreateRenderer(device);
            var shader = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit, "Silk imported mesh diagnostic"));
            var pipeline = device.CreatePipeline(new RenderPipelineDescription(shader, cullMode: CullMode.None));
            renderer.SetPipeline(pipeline);
            renderer.BindMaterial(new Material3D { BaseColor = new Vector4(0.16f, 0.78f, 0.66f, 1f) });
            renderer.SetWorldMatrix(DiagnosticWorld);
            UpdateCameraProjection();
            state = DiagnosticRenderState.ContextReady;
            if (surfaceWidth > 0 && surfaceHeight > 0)
                state = DiagnosticRenderState.SurfaceReady;
            if (latestModel is not null)
                pendingModel = latestModel;
            firstDrawReported = false;
            ReportStatus($"Context ready: GLES={args.Context.IsOpenGles}; modelPending={pendingModel is not null}");
        }
    }

    private void OnContextLost(object? sender, SilkGraphicsContextEventArgs args)
    {
        lock (lifecycleLock)
        {
            state = DiagnosticRenderState.ContextLost;
            ReleaseGpuResources(abandonResources: true);
        }
    }

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args)
    {
        surfaceWidth = Math.Max(args.Width, 1);
        surfaceHeight = Math.Max(args.Height, 1);

        lock (lifecycleLock)
        {
            ReportStatus($"Surface resized: {surfaceWidth}x{surfaceHeight}; rendererReady={renderer is not null}");
            state = device is not null && renderer is not null
                ? DiagnosticRenderState.SurfaceReady
                : DiagnosticRenderState.ContextReady;
            if (renderer is not null)
                renderer.Resize(surfaceWidth, surfaceHeight);
            UpdateCameraProjection();
        }
    }

    private void OnRenderFrame(object? sender, EventArgs args)
    {
        lock (lifecycleLock)
        {
            if (disposed || state == DiagnosticRenderState.Faulted || device is null || renderer is null || surfaceWidth <= 0 || surfaceHeight <= 0)
                return;

            var model = pendingModel ?? latestModel;
            if (model is not null && !ReferenceEquals(model, uploadedModel))
                UploadModel(model);

            if (gpuMeshes.Length == 0)
                return;

            try
            {
                state = DiagnosticRenderState.Rendering;
                renderer.SetWorldMatrix(DiagnosticWorld);
                renderer.BeginFrame(new Vector4(0.035f, 0.055f, 0.075f, 1f));
                try
                {
                    var drawCalls = 0;
                    var indexCount = 0;
                    foreach (var mesh in gpuMeshes)
                    {
                        var meshIndexCount = mesh.IndexBuffer.Description.IndexCount;
                        renderer.DrawIndexed(mesh.VertexBuffer, mesh.IndexBuffer, meshIndexCount);
                        drawCalls++;
                        indexCount += meshIndexCount;
                    }
                    if (!firstDrawReported)
                    {
                        var glError = device.Context.Api.GetError();
                        ReportStatus($"First frame submitted: draws={drawCalls}; indices={indexCount}; viewport={surfaceWidth}x{surfaceHeight}; GL={glError}; {GetClipBounds(latestModel)}");
                        firstDrawReported = true;
                    }
                }
                finally
                {
                    renderer.EndFrame();
                }
                state = DiagnosticRenderState.ResourcesReady;
                pendingModel = null;
            }
            catch (Exception exception)
            {
                state = DiagnosticRenderState.Faulted;
                ReportStatus($"Render failed: {exception.GetType().Name}: {exception.Message}");
                LogError($"Render failed: {exception}");
            }
        }
    }

    private void UpdateCameraProjection()
    {
        if (renderer is null)
            return;

        var width = surfaceWidth > 0 ? surfaceWidth : 1280;
        var height = surfaceHeight > 0 ? surfaceHeight : 720;
        var aspectRatio = width / (float)height;
        var cameraPosition = aspectRatio < 1f
            ? new Vector3(4.3f, -2.2f, -0.4f)
            : new Vector3(2f, 1.5f, 2.4f);
        var target = Vector3.Zero;
        viewMatrix = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
        projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.5f, aspectRatio, 0.05f, 50f);
        renderer.SetCamera(viewMatrix, projectionMatrix);
        renderer.SetLights([new Light3D
        {
            Direction = Vector3.Normalize(-cameraPosition),
            Color = Vector3.One,
            Intensity = 1f
        }]);
        renderer.SetWorldMatrix(DiagnosticWorld);
    }

    private string GetClipBounds(Model3D? model)
    {
        if (model is null)
            return "clip=unavailable";

        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        var verticesInFront = 0;
        var transform = DiagnosticWorld * viewMatrix * projectionMatrix;
        foreach (var mesh in model.Meshes)
        {
            if (mesh.Positions is null)
                continue;

            foreach (var position in mesh.Positions)
            {
                var clip = Vector4.Transform(new Vector4(position, 1f), transform);
                if (clip.W <= 0f)
                    continue;

                var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
                minimum = Vector3.Min(minimum, ndc);
                maximum = Vector3.Max(maximum, ndc);
                verticesInFront++;
            }
        }

        return verticesInFront == 0
            ? "clip=behind-camera"
            : $"ndc=({minimum.X:F2},{minimum.Y:F2},{minimum.Z:F2})..({maximum.X:F2},{maximum.Y:F2},{maximum.Z:F2})";
    }

    private void UploadModel(Model3D model)
    {
        if (device is null)
            return;

        var resources = new List<SilkMeshBuffers>(model.Meshes.Count);
        try
        {
            foreach (var mesh in model.Meshes)
                resources.Add(SilkMeshBuffers.Create(device, mesh));
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
        state = DiagnosticRenderState.ResourcesReady;
        ReportStatus($"GPU upload complete: meshes={gpuMeshes.Length}; vertices={gpuMeshes.Sum(mesh => mesh.VertexBuffer.Description.VertexCount)}; indices={gpuMeshes.Sum(mesh => mesh.IndexBuffer.Description.IndexCount)}");
    }

    private void ReleaseGpuResources(bool abandonResources = false)
    {
        var oldRenderer = renderer;
        var oldDevice = device;

        gpuMeshes = [];
        oldRenderer?.Dispose();
        if (abandonResources)
            oldDevice?.AbandonResources();
        else
            oldDevice?.Dispose();

        renderer = null;
        device = null;
        uploadedModel = null;
        state = DiagnosticRenderState.NoContext;
    }

    private static void LogInfo(string message)
    {
#if ANDROID
        Log.Info("Orbit3D.Render", message);
#else
        System.Diagnostics.Debug.WriteLine($"[Orbit3D.Render] {message}");
#endif
    }

    private void ReportStatus(string message)
    {
        LogInfo(message);
        StatusChanged?.Invoke(message);
    }

    private static void LogError(string message)
    {
#if ANDROID
        Log.Error("Orbit3D.Render", message);
#else
        System.Diagnostics.Debug.WriteLine($"[Orbit3D.Render] {message}");
#endif
    }

    internal enum DiagnosticRenderState
    {
        NoContext,
        ContextReady,
        SurfaceReady,
        ResourcesReady,
        Rendering,
        Faulted,
        ContextLost,
        Disposed
    }
}
