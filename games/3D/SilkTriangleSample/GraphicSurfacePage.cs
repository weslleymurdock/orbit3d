using System.Diagnostics;
using System.Numerics;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;
using Orbit3D.Graphics.Silk;

namespace SilkTriangleSample;

public sealed class GraphicSurfacePage : ContentPage
{
    private const string ModelAssetName = "toyota-gazoo-racing-wrt-gr-yaris-1-10.glb";
    private const string TextureAssetName = "toyota-gazoo-racing-wrt-gr-yaris-1-10.png";
    private readonly IModelImporter modelImporter;
    private readonly IAssetCache assetCache = new ModelAssetCache();
    private readonly GpuResourceCache gpuResourceCache = new();
    private readonly CameraOrbitController cameraController = new();
    private readonly SilkGraphicsSurface surface;
    private readonly Label statusLabel;
    private readonly object renderLock = new();
    private bool importStarted;
    private bool firstRenderedFrameLogged;
    private long lastMetricsUpdateTimestamp;
    private Scene3D? scene;
    private SilkGraphicsContext? graphicsContext;
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
        surface.ContextLost += OnContextLost;
        surface.SurfaceResized += OnSurfaceResized;
        surface.RenderFrame += OnRenderFrame;

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        surface.GestureRecognizers.Add(pan);
        surface.GestureRecognizers.Add(pinch);

        statusLabel = new Label
        {
            Text = $"Importing {ModelAssetName}...",
            Margin = new Thickness(12),
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Colors.White,
            Background = new SolidColorBrush(Color.FromArgb("#FF20242B"))
        };

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        Grid.SetRow(surface, 0);
        Grid.SetRow(statusLabel, 1);
        layout.Children.Add(surface);
        layout.Children.Add(statusLabel);
        Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        surface.IsVisible = true;
        if (importStarted)
            return;

        importStarted = true;
        try
        {
            byte[] assetData;
            await using (var asset = await FileSystem.OpenAppPackageFileAsync(ModelAssetName))
            using (var buffer = new MemoryStream())
            {
                await asset.CopyToAsync(buffer);
                assetData = buffer.ToArray();
            }

            var model = await Task.Run(() =>
            {
                using var stream = new MemoryStream(assetData, writable: false);
                var settings = new AssetImportSettings { GenerateNormals = true, Triangulate = true, JoinIdenticalVertices = true, FlipUVs = true };
                return assetCache.GetOrAdd(ModelAssetName, () => modelImporter.Import(stream, "glb"), settings);
            });

            byte[] textureData;
            await using (var asset = await FileSystem.OpenAppPackageFileAsync(TextureAssetName))
            using (var buffer = new MemoryStream())
            {
                await asset.CopyToAsync(buffer);
                textureData = buffer.ToArray();
            }

            var texture = await SampleTextureLoader.LoadRgbaAsync(textureData);
            var material = new Material3D
            {
                Name = "Toyota body sidecar texture",
                BaseColorTexture = texture,
                BaseColor = Vector4.One
            };
            model.Materials.Add(material);
            foreach (var mesh in model.Meshes)
            {
                mesh.MaterialIndex = model.Materials.Count - 1;
                if (mesh.TextureCoordinates is null)
                    mesh.TextureCoordinates = CreateSideProjectionUvs(mesh);
            }

            var runtimeScene = new Scene3D();
            model.RootNode.Name = ModelAssetName;
            model.RootNode.SetParent(runtimeScene.RootNode);
            runtimeScene.MainCamera = CreateCamera();
            cameraController.Attach(runtimeScene.MainCamera, target: Vector3.Zero, distance: 5.5f);
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
            LogDiagnostic($"Imported {ModelAssetName}: {model.Meshes.Count} mesh(es), {texture.Width}x{texture.Height} sidecar texture, {renderQueue.Count} queued item(s).");
            SetStatusText($"Loaded GLB + sidecar PNG | {renderQueue.Count} mesh(es)");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[SilkSample] Import failed: {exception}");
            SetStatusText($"Import failed: {exception.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        surface.IsVisible = false;
    }

    private void OnContextCreated(object? sender, SilkGraphicsContextEventArgs args)
    {
        graphicsContext = args.Context;
        LogDiagnostic("Silk graphics context created.");
        device = SilkGraphicsFactory.CreateDevice(args.Context);
        gpuResourceCache.SetDevice(device);
        renderer = SilkGraphicsFactory.CreateRenderer(device);
        var shaderProgram = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLitTextured, "RuntimeSceneShader"));
        pipeline = device.CreatePipeline(new RenderPipelineDescription(shaderProgram, cullMode: CullMode.Back));
        renderer.SetPipeline(pipeline);
        renderer.Resize(surfaceWidth, surfaceHeight);
        SetStatusText("Runtime renderer ready");
    }

    private void OnContextLost(object? sender, SilkGraphicsContextEventArgs args)
    {
        if (ReferenceEquals(graphicsContext, args.Context) && args.Context.IsCurrent)
            gpuResourceCache.Clear();
        else
            gpuResourceCache.Invalidate();
        LogDiagnostic($"Silk graphics context lost; current={args.Context.IsCurrent}.");
        renderer?.Dispose();
        device?.Dispose();
        renderer = null;
        device = null;
        pipeline = null;
        graphicsContext = null;
        SetStatusText("Graphics context lost; recreating GPU resources on next frame.");
    }

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args)
    {
        surfaceWidth = Math.Max(args.Width, 1);
        surfaceHeight = Math.Max(args.Height, 1);
        LogDiagnostic($"Graphics surface resized to {surfaceWidth}x{surfaceHeight} pixels.");
        if (renderer is not null)
            renderer.Resize(surfaceWidth, surfaceHeight);
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (scene?.MainCamera is null)
            return;

        if (e.StatusType == GestureStatus.Running)
            cameraController.Orbit((float)e.TotalX * 0.006f, (float)e.TotalY * 0.006f);
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (scene?.MainCamera is null)
            return;

        if (e.Status == GestureStatus.Running)
            cameraController.Zoom((float)((1d - e.Scale) * 8d));
    }

    private void OnRenderFrame(object? sender, EventArgs args)
    {
        if (scene is null || renderer is null || device is null || pipeline is null || surfaceWidth <= 0 || surfaceHeight <= 0)
            return;

        var activeScene = scene;
        var camera = activeScene.MainCamera ?? CreateCamera();
        camera.AspectRatio = surfaceWidth / (float)surfaceHeight;
        cameraController.Apply(camera, Vector3.Zero);
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
                var meshResource = gpuResourceCache.GetOrCreateMesh(item.Mesh, (renderDevice, mesh) =>
                {
                    var buffers = SilkMeshBuffers.Create(renderDevice, mesh);
                    return new MeshRenderResource(buffers.VertexBuffer, buffers.IndexBuffer);
                });

                ITextureResource? textureResource = null;
                if (material.BaseColorTexture is not null)
                {
                    textureResource = gpuResourceCache.GetOrCreateTexture(material.BaseColorTexture, (renderDevice, texture) =>
                    {
                        if (!texture.HasPixelData)
                            throw new InvalidOperationException($"Texture '{texture.Name}' has no decoded pixel data.");
                        return renderDevice.CreateTexture(
                            new TextureDescription(texture.Width, texture.Height, texture.PixelFormat),
                            texture.Data!);
                    });
                    renderer.BindTexture(0, textureResource);
                }
                else
                {
                    renderer.BindTexture(0, null!);
                }

                renderer.SetWorldMatrix(item.WorldMatrix);
                renderer.BindMaterial(material);
                renderer.SetPipeline(pipeline);
                renderer.DrawIndexed(meshResource.VertexBuffer, meshResource.IndexBuffer, meshResource.IndexBuffer.Description.IndexCount);
            }
        }
        finally
        {
            renderer.EndFrame();
            var metrics = ((SilkRenderer3D)renderer).Metrics;
            if (metrics.DrawCalls > 0 && !firstRenderedFrameLogged)
            {
                firstRenderedFrameLogged = true;
                LogDiagnostic($"First rendered frame: {metrics.DrawCalls} indexed draw(s), {metrics.TriangleCount} triangle(s).");
            }
            UpdateMetricsStatus(metrics);
        }
    }

    private void SetStatusText(string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.Equals(statusLabel.Text, text, StringComparison.Ordinal))
                statusLabel.Text = text;
        });
    }

    private static void LogDiagnostic(string message)
    {
        var diagnostic = $"[SilkSample] {message}";
        Debug.WriteLine(diagnostic);
        Console.WriteLine(diagnostic);
    }

    private void UpdateMetricsStatus(RenderMetrics metrics)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var previousTimestamp = Interlocked.Read(ref lastMetricsUpdateTimestamp);
        if (timestamp - previousTimestamp < Stopwatch.Frequency / 4)
            return;

        Interlocked.Exchange(ref lastMetricsUpdateTimestamp, timestamp);
        SetStatusText($"FPS: {metrics.FramesPerSecond:0} | Frame: {metrics.FrameTime:0.0} ms | Draw calls: {metrics.DrawCalls} | Objects: {metrics.RenderedItems}");
    }

    private static Camera3D CreateCamera() => new()
    {
        FieldOfView = MathF.PI / 3.25f,
        NearClip = 0.05f,
        FarClip = 50f,
        AspectRatio = 1.7777778f
    };

    private static Vector2[] CreateSideProjectionUvs(Mesh3D mesh)
    {
        var positions = mesh.Positions ?? throw new InvalidOperationException($"Mesh '{mesh.Name}' has no positions.");
        if (positions.Length == 0)
            throw new InvalidOperationException($"Mesh '{mesh.Name}' contains no positions.");

        var minimumY = positions[0].Y;
        var maximumY = positions[0].Y;
        var minimumZ = positions[0].Z;
        var maximumZ = positions[0].Z;
        foreach (var position in positions)
        {
            minimumY = Math.Min(minimumY, position.Y);
            maximumY = Math.Max(maximumY, position.Y);
            minimumZ = Math.Min(minimumZ, position.Z);
            maximumZ = Math.Max(maximumZ, position.Z);
        }

        var yRange = Math.Max(maximumY - minimumY, float.Epsilon);
        var zRange = Math.Max(maximumZ - minimumZ, float.Epsilon);
        var uvs = new Vector2[positions.Length];
        for (var i = 0; i < positions.Length; i++)
            uvs[i] = new Vector2((positions[i].Z - minimumZ) / zRange, 1f - (positions[i].Y - minimumY) / yRange);
        return uvs;
    }

    private sealed class CameraOrbitController
    {
        private float yaw;
        private float pitch;
        private float distance = 5.5f;
        private Vector3 target = Vector3.Zero;

        public void Attach(Camera3D camera, Vector3 target, float distance)
        {
            this.target = target;
            this.distance = distance;
            Apply(camera, target);
        }

        public void Orbit(float yawDelta, float pitchDelta)
        {
            yaw += yawDelta;
            pitch = Math.Clamp(pitch + pitchDelta, -1.2f, 1.2f);
        }

        public void Zoom(float delta)
        {
            distance = Math.Clamp(distance + delta, 1.5f, 18f);
        }

        public void Apply(Camera3D camera, Vector3 focus)
        {
            target = focus;
            var rotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f);
            var offset = Vector3.Transform(new Vector3(0f, 0f, distance), rotation);
            camera.Transform.Position = target + offset;
            camera.Transform.Rotation = rotation;
        }
    }
}