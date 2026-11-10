using System.Diagnostics;
using System.Globalization;
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
    private readonly Label diagnosticsLabel;
    private readonly object renderLock = new();
    private static readonly object LogFileLock = new();
    private static readonly AssetImportSettings DefaultImportSettings = new()
    {
        GenerateNormals = true,
        Triangulate = true,
        JoinIdenticalVertices = true,
        FlipUVs = true
    };
    private static readonly string? ValidationCommandFile = Environment.GetEnvironmentVariable("ORBIT3D_COMMAND_FILE");
    private static readonly bool LogDiagnosticsText = Environment.GetEnvironmentVariable("ORBIT3D_LOG_DIAGNOSTICS") == "1";
    private bool importStarted;
    private bool commandPumpStarted;
    private bool firstRenderedFrameLogged;
    private long lastMetricsUpdateTimestamp;
    private long totalRenderedFrames;
    private int contextCreationCount;
    private int contextLossCount;
    private int gpuMeshUploadCount;
    private int gpuTextureUploadCount;
    private Scene3D? scene;
    private SilkGraphicsContext? graphicsContext;
    private IRenderDevice? device;
    private IRenderer3D? renderer;
    private IRenderPipeline? pipeline;
    private int surfaceWidth;
    private int surfaceHeight;
    private int loadedMeshCount;
    private int loadedTextureWidth;
    private int loadedTextureHeight;

    public GraphicSurfacePage(IModelImporter modelImporter)
    {
        this.modelImporter = modelImporter;
        surface = new SilkGraphicsSurface
        {
            AutomationId = "SilkGraphicsSurface",
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
            AutomationId = "StatusLabel",
            Text = $"Importing {ModelAssetName}...",
            Margin = new Thickness(12, 6, 12, 2),
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Colors.White,
            Background = new SolidColorBrush(Color.FromArgb("#FF20242B"))
        };

        diagnosticsLabel = new Label
        {
            AutomationId = "DiagnosticsLabel",
            Text = "Phase=Importing;Context=Pending;Assets=Pending;DrawCalls=0;Frames=0",
            Margin = new Thickness(12, 0, 12, 6),
            FontSize = 11,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#FFA0AAB8"),
            Background = new SolidColorBrush(Color.FromArgb("#FF16181D"))
        };

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        Grid.SetRow(surface, 0);
        Grid.SetRow(statusLabel, 1);
        Grid.SetRow(diagnosticsLabel, 2);
        layout.Children.Add(surface);
        layout.Children.Add(statusLabel);
        layout.Children.Add(diagnosticsLabel);
        Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        surface.IsVisible = true;
        StartValidationCommandPump();
        if (importStarted)
            return;

        importStarted = true;
        SetDiagnosticsText("Phase=Importing;Context=Pending;Assets=Pending;DrawCalls=0;Frames=0");
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
                return assetCache.GetOrAdd(ModelAssetName, () => modelImporter.Import(stream, "glb"), DefaultImportSettings);
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

            bool contextReady;
            int currentWidth;
            int currentHeight;
            lock (renderLock)
            {
                scene = runtimeScene;
                loadedMeshCount = model.Meshes.Count;
                loadedTextureWidth = texture.Width;
                loadedTextureHeight = texture.Height;
                contextReady = graphicsContext is not null;
                currentWidth = surfaceWidth;
                currentHeight = surfaceHeight;
                Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
            }

            var renderQueue = runtimeScene.BuildRenderQueue(runtimeScene.MainCamera, currentWidth, currentHeight);
            LogDiagnostic($"Imported {ModelAssetName}: {model.Meshes.Count} mesh(es), {texture.Width}x{texture.Height} sidecar texture, {renderQueue.Count} queued item(s).");
            if (Interlocked.Read(ref totalRenderedFrames) == 0)
            {
                SetStatusText($"Loaded GLB + sidecar PNG | {renderQueue.Count} mesh(es)");
                SetDiagnosticsText($"Phase=AssetsLoaded;Context={(contextReady ? "Ready" : "Pending")};Assets=Loaded;Meshes={model.Meshes.Count};Texture={texture.Width}x{texture.Height};DrawCalls=0;Frames=0");
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[SilkSample] Import failed: {exception}");
            SetStatusText($"Import failed: {exception.Message}");
            SetDiagnosticsText($"Phase=Failed;Stage=AssetImport;Error={exception.GetType().Name}");
        }
    }

    /// <summary>
    /// Validation hook: applies <c>orbit &lt;yaw&gt; &lt;pitch&gt;</c>, <c>zoom &lt;delta&gt;</c>,
    /// <c>resize &lt;width&gt; &lt;height&gt;</c>, <c>pause</c>, <c>suspend</c>, <c>resume</c>, and <c>release</c> lines
    /// to the same camera controller, window, and surface that interactive controls use, then deletes the command file.
    /// </summary>
    private void StartValidationCommandPump()
    {
        if (commandPumpStarted || Dispatcher is null)
            return;

        var commandPath = !string.IsNullOrWhiteSpace(ValidationCommandFile)
            ? ValidationCommandFile
            : Path.Combine(FileSystem.CacheDirectory, "orbit3d-commands.txt");

        commandPumpStarted = true;
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) =>
        {
            if (!File.Exists(commandPath))
                return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(commandPath);
                File.Delete(commandPath);
            }
            catch (IOException exception)
            {
                LogDiagnostic($"Validation command file could not be consumed: {exception.Message}");
                return;
            }

            foreach (var line in lines)
            {
                try
                {
                    ApplyValidationCommand(line.Trim());
                }
                catch (Exception exception)
                {
                    var actual = exception is System.Reflection.TargetInvocationException tie && tie.InnerException is not null
                        ? tie.InnerException
                        : exception;
                    LogDiagnostic($"Validation command '{line}' threw {actual.GetType().Name}: {actual.Message} at {actual.StackTrace}");
                }
            }
        };
        timer.Start();
    }

    private void ApplyValidationCommand(string line)
    {
        var parts = line.Split([' ', ':', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var inv = CultureInfo.InvariantCulture;
        switch (parts[0].ToLowerInvariant())
        {
            case "orbit" when parts.Length == 3:
                cameraController.Orbit(float.Parse(parts[1], inv), float.Parse(parts[2], inv));
                Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
                break;
            case "zoom" when parts.Length == 2:
                cameraController.Zoom(float.Parse(parts[1], inv));
                Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
                break;
            case "resize" when parts.Length == 3:
                var targetWidth = double.Parse(parts[1], inv);
                var targetHeight = double.Parse(parts[2], inv);
                if (targetWidth <= 0 || targetHeight <= 0)
                {
                    surface.WidthRequest = -1;
                    surface.HeightRequest = -1;
                    surface.HorizontalOptions = LayoutOptions.Fill;
                    surface.VerticalOptions = LayoutOptions.Fill;
                }
                else
                {
#if WINDOWS || MACCATALYST
                    if (Window is not null)
                    {
                        Window.Width = targetWidth;
                        Window.Height = targetHeight;
                    }
#else
                    surface.HorizontalOptions = LayoutOptions.Center;
                    surface.VerticalOptions = LayoutOptions.Center;
                    surface.WidthRequest = targetWidth;
                    surface.HeightRequest = targetHeight;
#endif
                }
                Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
                break;
            case "resize" when parts.Length == 2 && string.Equals(parts[1], "reset", StringComparison.OrdinalIgnoreCase):
                surface.WidthRequest = -1;
                surface.HeightRequest = -1;
                surface.HorizontalOptions = LayoutOptions.Fill;
                surface.VerticalOptions = LayoutOptions.Fill;
                Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
                break;
            case "pause":
                InvokeSurfaceMethod("PauseRendering");
                break;
            case "suspend":
                InvokeSurfaceMethod("SuspendSurface");
                break;
            case "resume":
                InvokeSurfaceMethod("ResumeSurface");
                break;
            case "release":
                InvokeSurfaceMethod("ReleaseSurface");
                break;
            case "close":
            case "quit":
                Application.Current?.Quit();
                break;
            default:
                LogDiagnostic($"Unknown validation command '{line}'.");
                return;
        }

        LogDiagnostic($"Applied validation command '{line}'.");
    }

    private void InvokeSurfaceMethod(string methodName)
    {
        var platformView = surface.Handler?.PlatformView;
        var platformMethod = platformView?.GetType().GetMethod(methodName);
        if (platformMethod is not null)
        {
            platformMethod.Invoke(platformView, null);
            return;
        }

        var handler = surface.Handler;
        var handlerMethod = handler?.GetType().GetMethod(methodName);
        handlerMethod?.Invoke(handler, null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        surface.IsVisible = false;
    }

    private void OnContextCreated(object? sender, SilkGraphicsContextEventArgs args)
    {
        bool assetsLoaded;
        int creationNumber;
        bool cpuCached;
        lock (renderLock)
        {
            creationNumber = ++contextCreationCount;
            graphicsContext = args.Context;
            device = SilkGraphicsFactory.CreateDevice(args.Context);
            gpuResourceCache.SetDevice(device);
            renderer = SilkGraphicsFactory.CreateRenderer(device);
            var shaderProgram = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLitTextured, "RuntimeSceneShader"));
            pipeline = device.CreatePipeline(new RenderPipelineDescription(shaderProgram, cullMode: CullMode.Back));
            renderer.SetPipeline(pipeline);
            renderer.Resize(surfaceWidth, surfaceHeight);
            firstRenderedFrameLogged = false;
            Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
            assetsLoaded = scene is not null;
            cpuCached = assetCache.TryGet(ModelAssetName, DefaultImportSettings, out _);
        }

        LogDiagnostic($"Silk graphics context created (count={creationNumber}, cpuAssetCached={cpuCached}).");
        SetStatusText("Runtime renderer ready");
        SetDiagnosticsText($"Phase=ContextCreated;Context=Ready;Device=Silk;Assets={(assetsLoaded ? "Loaded" : "Pending")};DrawCalls=0;Frames=0");
    }

    private void OnContextLost(object? sender, SilkGraphicsContextEventArgs args)
    {
        int lossNumber;
        int cachedMeshesBefore;
        int cachedTexturesBefore;
        bool wasCurrent;
        lock (renderLock)
        {
            lossNumber = ++contextLossCount;
            cachedMeshesBefore = gpuResourceCache.CachedMeshCount;
            cachedTexturesBefore = gpuResourceCache.CachedTextureCount;
            wasCurrent = args.Context.IsCurrent;
            if (ReferenceEquals(graphicsContext, args.Context) && wasCurrent)
                gpuResourceCache.Clear();
            else
                gpuResourceCache.Invalidate();

            renderer?.Dispose();
            device?.Dispose();
            renderer = null;
            device = null;
            pipeline = null;
            graphicsContext = null;
            firstRenderedFrameLogged = false;
            Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
        }

        LogDiagnostic($"Silk graphics context lost; current={wasCurrent} (count={lossNumber}, cachedMeshes={cachedMeshesBefore}, cachedTextures={cachedTexturesBefore}).");
        SetStatusText("Graphics context lost; recreating GPU resources on next frame.");
        SetDiagnosticsText("Phase=ContextLost;Context=Lost;Assets=Loaded;DrawCalls=0;Frames=0");
    }

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args)
    {
        int width;
        int height;
        lock (renderLock)
        {
            surfaceWidth = Math.Max(args.Width, 1);
            surfaceHeight = Math.Max(args.Height, 1);
            width = surfaceWidth;
            height = surfaceHeight;
            renderer?.Resize(surfaceWidth, surfaceHeight);
            Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
        }

        LogDiagnostic($"Graphics surface resized to {width}x{height} pixels.");
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
        RenderMetrics metrics;
        bool logFirstFrame = false;
        lock (renderLock)
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
                        var uploadNumber = ++gpuMeshUploadCount;
                        LogDiagnostic($"Uploaded GPU mesh '{mesh.Name}' (upload #{uploadNumber}): {buffers.VertexBuffer.Description.VertexCount} vertices, {buffers.IndexBuffer.Description.IndexCount} indices.");
                        return new MeshRenderResource(buffers.VertexBuffer, buffers.IndexBuffer);
                    });

                    ITextureResource? textureResource = null;
                    if (material.BaseColorTexture is not null)
                    {
                        textureResource = gpuResourceCache.GetOrCreateTexture(material.BaseColorTexture, (renderDevice, texture) =>
                        {
                            if (!texture.HasPixelData)
                                throw new InvalidOperationException($"Texture '{texture.Name}' has no decoded pixel data.");
                            var createdTexture = renderDevice.CreateTexture(
                                new TextureDescription(texture.Width, texture.Height, texture.PixelFormat),
                                texture.Data!);
                            var uploadNumber = ++gpuTextureUploadCount;
                            LogDiagnostic($"Uploaded GPU texture '{texture.Name}' (upload #{uploadNumber}): {texture.Width}x{texture.Height} ({texture.PixelFormat}).");
                            return createdTexture;
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
                Interlocked.Increment(ref totalRenderedFrames);
                metrics = ((SilkRenderer3D)renderer).Metrics;
                if (metrics.DrawCalls > 0 && !firstRenderedFrameLogged)
                {
                    firstRenderedFrameLogged = true;
                    logFirstFrame = true;
                }
            }
        }

        if (logFirstFrame)
            LogDiagnostic($"First rendered frame: {metrics.DrawCalls} indexed draw(s), {metrics.TriangleCount} triangle(s).");

        UpdateMetricsStatus(metrics);
    }

    private void SetStatusText(string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.Equals(statusLabel.Text, text, StringComparison.Ordinal))
                statusLabel.Text = text;
        });
    }

    private void SetDiagnosticsText(string text)
    {
        if (LogDiagnosticsText)
            LogDiagnostic($"Diagnostics: {text}");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.Equals(diagnosticsLabel.Text, text, StringComparison.Ordinal))
                diagnosticsLabel.Text = text;
        });
    }

    private static void LogDiagnostic(string message)
    {
        var diagnostic = $"[SilkSample] {message}";
        Debug.WriteLine(diagnostic);
        Console.WriteLine(diagnostic);
#if ANDROID
        Android.Util.Log.Info("Orbit3D", diagnostic);
#endif
        try
        {
            var logPath = Path.Combine(FileSystem.CacheDirectory, "orbit3d-runtime.log");
            lock (LogFileLock)
            {
                File.AppendAllText(logPath, $"{DateTime.UtcNow:O} {diagnostic}{Environment.NewLine}");
            }
        }
        catch
        {
            // Ignore file logging errors if the cache directory is unavailable.
        }
    }

    private void UpdateMetricsStatus(RenderMetrics metrics)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var previousTimestamp = Interlocked.Read(ref lastMetricsUpdateTimestamp);
        if (timestamp - previousTimestamp < Stopwatch.Frequency / 4)
            return;

        Interlocked.Exchange(ref lastMetricsUpdateTimestamp, timestamp);
        var frames = Interlocked.Read(ref totalRenderedFrames);
        SetStatusText(string.Create(
            CultureInfo.InvariantCulture,
            $"FPS: {metrics.FramesPerSecond:0} | Frame: {metrics.FrameTime:0.0} ms | Draw calls: {metrics.DrawCalls} | Objects: {metrics.RenderedItems}"));
        SetDiagnosticsText(string.Create(
            CultureInfo.InvariantCulture,
            $"Phase=Rendering;Context=Ready;Device=Silk;Assets=Loaded;Meshes={loadedMeshCount};Texture={loadedTextureWidth}x{loadedTextureHeight};Items={metrics.RenderedItems};DrawCalls={metrics.DrawCalls};Triangles={metrics.TriangleCount};FPS={metrics.FramesPerSecond:0};Frames={frames};Viewport={surfaceWidth}x{surfaceHeight};Yaw={cameraController.Yaw:F2};Pitch={cameraController.Pitch:F2};Dist={cameraController.Distance:F2}"));
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

        public float Yaw => yaw;
        public float Pitch => pitch;
        public float Distance => distance;

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
