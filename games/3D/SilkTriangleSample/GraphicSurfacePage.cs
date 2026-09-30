using System.Numerics;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;
using Orbit3D.Graphics.Silk;

namespace SilkTriangleSample;

public sealed class GraphicSurfacePage : ContentPage
{
    private readonly IModelImporter modelImporter;
    private readonly IAssetCache assetCache = new ModelAssetCache();
    private readonly GpuResourceCache gpuResourceCache = new();
    private readonly SilkGraphicsSurface surface;
    private readonly Label statusLabel;
    private readonly object renderLock = new();
    private bool importStarted;
    private Scene3D? scene;
    private IRenderDevice? device;
    private IRenderer3D? renderer;
    private IRenderPipeline? pipeline;
    private int surfaceWidth;
    private int surfaceHeight;

    public GraphicSurfacePage(IModelImporter modelImporter)
    {
        this.modelImporter = modelImporter;
        surface = new SilkGraphicsSurface
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        surface.ContextCreated += OnContextCreated;
        surface.SurfaceResized += OnSurfaceResized;
        surface.RenderFrame += OnRenderFrame;

        statusLabel = new Label
        {
            Text = "Importing poly.obj...",
            Margin = new Thickness(12),
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            TextColor = Colors.White,
            Background = new SolidColorBrush(Color.FromArgb("#C0000000"))
        };

        var layout = new Grid();
        layout.Children.Add(surface);
        layout.Children.Add(statusLabel);
        Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (importStarted)
            return;

        importStarted = true;
        try
        {
            byte[] assetData;
            await using (var asset = await FileSystem.OpenAppPackageFileAsync("poly.obj"))
            using (var buffer = new MemoryStream())
            {
                await asset.CopyToAsync(buffer);
                assetData = buffer.ToArray();
            }

            var model = await Task.Run(() =>
            {
                using var stream = new MemoryStream(assetData, writable: false);
                var settings = new AssetImportSettings { GenerateNormals = true, Triangulate = true, JoinIdenticalVertices = true, FlipUVs = true };
                return assetCache.GetOrAdd("poly.obj", () => modelImporter.Import(stream, "obj"), settings);
            });

            var runtimeScene = new Scene3D();
            var rootNode = new Node3D { Name = "poly.obj", Model = model };
            rootNode.SetParent(runtimeScene.RootNode);
            runtimeScene.MainCamera = CreateCamera();
            runtimeScene.MainCamera!.Transform.Position = new Vector3(0f, 1f, 4.5f);
            runtimeScene.MainCamera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.2f, -0.15f, 0f);
            runtimeScene.Lights.Add(new Light3D
            {
                Type = LightType.Directional,
                Direction = Vector3.Normalize(new Vector3(-1f, -1f, -1f)),
                Color = Vector3.One,
                Intensity = 1.3f
            });

            lock (renderLock)
            {
                scene = runtimeScene;
            }

            var renderQueue = runtimeScene.BuildRenderQueue(runtimeScene.MainCamera, surfaceWidth, surfaceHeight);
            statusLabel.Text = $"Runtime scene ready: {renderQueue.Count} queued meshes";
        }
        catch (Exception exception)
        {
            statusLabel.Text = $"Import failed: {exception.Message}";
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (renderer is not null)
            renderer.Dispose();
        if (device is not null)
            device.Dispose();
        renderer = null;
        device = null;
        pipeline = null;
    }

    private void OnContextCreated(object? sender, SilkGraphicsContextEventArgs args)
    {
        device = SilkGraphicsFactory.CreateDevice(args.Context);
        renderer = SilkGraphicsFactory.CreateRenderer(device);
        var shaderProgram = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit, "RuntimeSceneShader"));
        pipeline = device.CreatePipeline(new RenderPipelineDescription(shaderProgram, cullMode: CullMode.None));
        renderer.SetPipeline(pipeline);
        renderer.Resize(surfaceWidth, surfaceHeight);
        statusLabel.Text = "Runtime renderer ready";
    }

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args)
    {
        surfaceWidth = Math.Max(args.Width, 1);
        surfaceHeight = Math.Max(args.Height, 1);
        if (renderer is not null)
            renderer.Resize(surfaceWidth, surfaceHeight);
    }

    private void OnRenderFrame(object? sender, EventArgs args)
    {
        if (scene is null || renderer is null || device is null || pipeline is null || surfaceWidth <= 0 || surfaceHeight <= 0)
            return;

        var activeScene = scene;
        var camera = activeScene.MainCamera ?? CreateCamera();
        camera.AspectRatio = surfaceWidth / (float)surfaceHeight;
        var queue = activeScene.BuildRenderQueue(camera, surfaceWidth, surfaceHeight);
        renderer.SetCamera(camera.ViewMatrix, camera.ProjectionMatrix);
        var lights = activeScene.Lights.Count > 0 ? activeScene.Lights.ToArray() : [];
        renderer.SetLights(lights);
        renderer.BeginFrame(new Vector4(0.04f, 0.06f, 0.09f, 1f));

        try
        {
            foreach (var item in queue)
            {
                if (item.Mesh is null)
                    continue;

                var material = item.Material ?? new Material3D { BaseColor = new Vector4(0.78f, 0.84f, 1f, 1f) };
                var meshResource = gpuResourceCache.GetOrCreateMesh(item.Mesh, mesh =>
                {
                    var buffers = SilkMeshBuffers.Create(device, mesh);
                    return new MeshRenderResource(buffers.VertexBuffer, buffers.IndexBuffer);
                });

                renderer.SetWorldMatrix(item.WorldMatrix);
                renderer.BindMaterial(material);
                renderer.SetPipeline(pipeline);
                renderer.DrawIndexed(meshResource.VertexBuffer, meshResource.IndexBuffer, meshResource.IndexBuffer.Description.IndexCount);
            }
        }
        finally
        {
            renderer.EndFrame();
        }
    }

    private static Camera3D CreateCamera() => new()
    {
        FieldOfView = MathF.PI / 3.25f,
        NearClip = 0.05f,
        FarClip = 50f,
        AspectRatio = 1.7777778f
    };
}