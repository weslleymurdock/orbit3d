using System.Diagnostics;
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
    private readonly CameraOrbitController cameraController = new();
    private readonly SilkGraphicsSurface surface;
    private readonly Label statusLabel;
    private readonly object renderLock = new();
    private bool importStarted;
    private long lastMetricsUpdateTimestamp;
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

            foreach (var material in model.Materials)
            {
                if (material.BaseColorTexture is null)
                    material.BaseColorTexture = CreateSampleTexture();
            }

            var runtimeScene = new Scene3D();
            var rootNode = new Node3D { Name = "poly.obj", Model = model };
            rootNode.SetParent(runtimeScene.RootNode);
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
            SetStatusText($"Runtime scene ready: {renderQueue.Count} queued meshes");
        }
        catch (Exception exception)
        {
            SetStatusText($"Import failed: {exception.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        gpuResourceCache.Invalidate();
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
        gpuResourceCache.SetDevice(device);
        renderer = SilkGraphicsFactory.CreateRenderer(device);
        var shaderProgram = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.UnlitTextured, "RuntimeSceneShader"));
        pipeline = device.CreatePipeline(new RenderPipelineDescription(shaderProgram, cullMode: CullMode.Back));
        renderer.SetPipeline(pipeline);
        renderer.Resize(surfaceWidth, surfaceHeight);
        SetStatusText("Runtime renderer ready");
    }

    private void OnContextLost(object? sender, SilkGraphicsContextEventArgs args)
    {
        gpuResourceCache.Invalidate();
        renderer = null;
        device = null;
        pipeline = null;
        SetStatusText("Graphics context lost; recreating GPU resources on next frame.");
    }

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args)
    {
        surfaceWidth = Math.Max(args.Width, 1);
        surfaceHeight = Math.Max(args.Height, 1);
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
                        var width = texture.Width > 0 ? texture.Width : 4;
                        var height = texture.Height > 0 ? texture.Height : 4;
                        var pixels = texture.Data ?? CreateSamplePixels(width, height);
                        return renderDevice.CreateTexture(new TextureDescription(width, height, texture.PixelFormat), pixels);
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

    private static Texture2D CreateSampleTexture()
    {
        const int size = 4;
        return new Texture2D
        {
            Name = "runtime-checker",
            Usage = TextureUsage.BaseColor,
            Width = size,
            Height = size,
            PixelFormat = TextureFormat.Rgba8Unorm,
            Data = CreateSamplePixels(size, size)
        };
    }

    private static byte[] CreateSamplePixels(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                var isDark = ((x + y) % 2) == 0;
                pixels[index] = isDark ? (byte)128 : (byte)255;
                pixels[index + 1] = isDark ? (byte)64 : (byte)220;
                pixels[index + 2] = (byte)180;
                pixels[index + 3] = (byte)255;
            }
        }

        return pixels;
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