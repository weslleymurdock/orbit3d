using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;
using Orbit3D.Graphics.Silk;

namespace AnimationSample;

public sealed class AnimationSurfacePage : ContentPage
{
    private const string ModelAssetName = "Idle.fbx";
    private readonly IModelImporter modelImporter;
    private readonly IAssetCache assetCache = new ModelAssetCache();
    private readonly GpuResourceCache gpuResourceCache = new();
    private readonly SilkGraphicsSurface surface;
    private readonly Label statusLabel;
    private readonly Label diagnosticsLabel;
    private readonly Button playButton;
    private readonly Button pauseButton;
    private readonly Button resumeButton;
    private readonly Button stopButton;
    private readonly Button loopButton;
    private readonly Button speedButton;
    private readonly Button recreateContextButton;
    private readonly Picker clipPicker;
    private readonly object renderLock = new();
    private static readonly AssetImportSettings DefaultImportSettings = new()
    {
        GenerateNormals = true,
        Triangulate = true,
        JoinIdenticalVertices = true,
        FlipUVs = true
    };
    private static readonly float[] SpeedPresets = [0.5f, 1.0f, 2.0f];

    private bool importStarted;
    private int speedPresetIndex = 1;
    private long lastFrameTimestamp;
    private long lastMetricsUpdateTimestamp;
    private long totalRenderedFrames;
    private int contextCreationCount;
    private int contextLossCount;
    private int surfaceWidth;
    private int surfaceHeight;

    private Scene3D? scene;
    private Model3D? loadedModel;
    private AnimationEvaluator? evaluator;
    private SilkGraphicsContext? graphicsContext;
    private IRenderDevice? device;
    private IRenderer3D? renderer;
    private IRenderPipeline? skinnedPipeline;
    private IRenderPipeline? staticPipeline;

    private Vector3 cameraTarget = new(0f, 85f, 0f);
    private float cameraDistance = 260f;
    private float cameraYaw;
    private float cameraPitch = 0.1f;

    public AnimationSurfacePage(IModelImporter modelImporter)
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

        playButton = CreateControlButton("PlayBtn", "Play", OnPlayClicked);
        pauseButton = CreateControlButton("PauseBtn", "Pause", OnPauseClicked);
        resumeButton = CreateControlButton("ResumeBtn", "Resume", OnResumeClicked);
        stopButton = CreateControlButton("StopBtn", "Stop", OnStopClicked);
        loopButton = CreateControlButton("LoopBtn", "Loop: On", OnLoopClicked);
        speedButton = CreateControlButton("SpeedBtn", "Speed: 1.0x", OnSpeedClicked);
        recreateContextButton = CreateControlButton("RecreateContextBtn", "RecreateCtx", OnRecreateContextClicked);

        clipPicker = new Picker
        {
            AutomationId = "ClipPicker",
            Title = "Clip",
            FontSize = 12,
            TextColor = Colors.White,
            TitleColor = Color.FromArgb("#FFA0AAB8"),
            BackgroundColor = Color.FromArgb("#FF2A303B"),
            MinimumWidthRequest = 110,
            HeightRequest = 34
        };
        clipPicker.SelectedIndexChanged += OnClipSelectionChanged;

        var controlsBar = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center,
            Padding = new Thickness(8, 4),
            Background = new SolidColorBrush(Color.FromArgb("#FF1B1F27")),
            Children =
            {
                playButton,
                pauseButton,
                resumeButton,
                stopButton,
                loopButton,
                speedButton,
                recreateContextButton,
                clipPicker
            }
        };

        statusLabel = new Label
        {
            AutomationId = "StatusLabel",
            Text = $"Importing {ModelAssetName}...",
            Margin = new Thickness(12, 4, 12, 2),
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Colors.White,
            Background = new SolidColorBrush(Color.FromArgb("#FF20242B"))
        };

        diagnosticsLabel = new Label
        {
            AutomationId = "DiagnosticsLabel",
            Text = "Phase=Importing;Context=Pending;Assets=Pending;Skeleton=Pending;Bones=0;Clips=0;State=Stopped;Time=0.000;DrawCalls=0;Frames=0",
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
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        Grid.SetRow(surface, 0);
        Grid.SetRow(controlsBar, 1);
        Grid.SetRow(statusLabel, 2);
        Grid.SetRow(diagnosticsLabel, 3);
        layout.Children.Add(surface);
        layout.Children.Add(controlsBar);
        layout.Children.Add(statusLabel);
        layout.Children.Add(diagnosticsLabel);
        Content = layout;
    }

    private static Button CreateControlButton(string automationId, string text, EventHandler onClicked)
    {
        var button = new Button
        {
            AutomationId = automationId,
            Text = text,
            FontSize = 12,
            Padding = new Thickness(10, 4),
            Margin = new Thickness(2),
            HeightRequest = 34,
            BackgroundColor = Color.FromArgb("#FF2D3542"),
            TextColor = Colors.White
        };
        button.Clicked += onClicked;
        return button;
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
                return assetCache.GetOrAdd(ModelAssetName, () => modelImporter.Import(stream, "fbx"), DefaultImportSettings);
            });

            if (model.Skeleton is null || model.Skeleton.BoneCount == 0)
                throw new InvalidOperationException("Imported FBX model does not contain a skeleton.");
            if (model.Animations.Count == 0)
                throw new InvalidOperationException("Imported FBX model does not contain any animation clips.");

            // Assign distinct subtle shading per mesh so body/brows/eyes/mouth read clearly even without external textures.
            var palette = new[]
            {
                new Vector4(0.82f, 0.85f, 0.90f, 1.0f),
                new Vector4(0.25f, 0.22f, 0.20f, 1.0f),
                new Vector4(0.95f, 0.95f, 0.98f, 1.0f),
                new Vector4(0.75f, 0.35f, 0.35f, 1.0f)
            };
            for (var i = 0; i < model.Meshes.Count; i++)
            {
                var matIdx = model.Meshes[i].MaterialIndex;
                if (matIdx >= 0 && matIdx < model.Materials.Count)
                    model.Materials[matIdx].BaseColor = palette[i % palette.Length];
            }

            ComputeModelBounds(model, out var center, out var radius);
            var runtimeScene = new Scene3D();
            model.RootNode.Name = ModelAssetName;
            model.RootNode.SetParent(runtimeScene.RootNode);
            runtimeScene.MainCamera = new Camera3D
            {
                FieldOfView = MathF.PI / 4f,
                NearClip = Math.Max(0.05f, radius * 0.01f),
                FarClip = Math.Max(500f, radius * 25f)
            };
            runtimeScene.Lights.Add(new Light3D
            {
                Type = LightType.Directional,
                Direction = Vector3.Normalize(new Vector3(-0.4f, -0.7f, -1.0f)),
                Color = Vector3.One,
                Intensity = 1.25f
            });

            var animEvaluator = new AnimationEvaluator(model.Skeleton, model.Animations)
            {
                IsLooping = true,
                PlaybackSpeed = SpeedPresets[speedPresetIndex]
            };
            animEvaluator.Play();

            lock (renderLock)
            {
                loadedModel = model;
                scene = runtimeScene;
                evaluator = animEvaluator;
                cameraTarget = center;
                cameraDistance = Math.Max(2.5f, radius * 2.4f);
                lastFrameTimestamp = Stopwatch.GetTimestamp();
                Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
            }

            var clipNames = model.Animations.Select(c => c.Name).ToList();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                clipPicker.ItemsSource = clipNames;
                if (clipNames.Count > 0)
                    clipPicker.SelectedIndex = 0;
            });

            SetStatusText($"Loaded {ModelAssetName} | Bones={model.Skeleton.BoneCount} | Clip={animEvaluator.SelectedClip?.Name}");
            UpdateDiagnosticsSnapshot(null, force: true);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[AnimationSample] Import failed: {exception}");
            SetStatusText($"Import failed: {exception.Message}");
            SetDiagnosticsText($"Phase=Failed;Stage=AssetImport;Error={exception.GetType().Name}");
        }
    }

    private static void ComputeModelBounds(Model3D model, out Vector3 center, out float radius)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var hasVerts = false;
        foreach (var mesh in model.Meshes)
        {
            if (mesh.Positions is null)
                continue;
            foreach (var p in mesh.Positions)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                hasVerts = true;
            }
        }

        if (!hasVerts)
        {
            center = Vector3.Zero;
            radius = 100f;
            return;
        }

        center = (min + max) * 0.5f;
        radius = Math.Max(1.0f, (max - min).Length() * 0.5f);
    }

    private void OnContextCreated(object? sender, SilkGraphicsContextEventArgs args)
    {
        int creationNumber;
        lock (renderLock)
        {
            creationNumber = ++contextCreationCount;
            graphicsContext = args.Context;
            device = SilkGraphicsFactory.CreateDevice(args.Context);
            gpuResourceCache.SetDevice(device);
            renderer = SilkGraphicsFactory.CreateRenderer(device);

            var skinnedShader = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLitSkinned, "SkinnedRuntimeShader"));
            skinnedPipeline = device.CreatePipeline(new RenderPipelineDescription(skinnedShader, cullMode: CullMode.Back));

            var staticShader = device.CreateShaderProgram(new ShaderProgramDescription(ShaderProgramKind.BasicLit, "StaticFallbackShader"));
            staticPipeline = device.CreatePipeline(new RenderPipelineDescription(staticShader, cullMode: CullMode.Back));

            renderer.SetPipeline(skinnedPipeline);
            renderer.Resize(surfaceWidth, surfaceHeight);
            lastFrameTimestamp = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
        }

        Console.WriteLine($"[AnimationSample] Context created (count={creationNumber})");
        SetStatusText("Skinned renderer ready");
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnContextLost(object? sender, SilkGraphicsContextEventArgs args)
    {
        int lossNumber;
        lock (renderLock)
        {
            lossNumber = ++contextLossCount;
            if (ReferenceEquals(graphicsContext, args.Context) && args.Context.IsCurrent)
                gpuResourceCache.Clear();
            else
                gpuResourceCache.Invalidate();

            renderer?.Dispose();
            device?.Dispose();
            renderer = null;
            device = null;
            skinnedPipeline = null;
            staticPipeline = null;
            graphicsContext = null;
            Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
        }

        Console.WriteLine($"[AnimationSample] Context lost (count={lossNumber})");
        SetStatusText("Graphics context lost; recreating on resume...");
        SetDiagnosticsText($"Phase=ContextLost;Context=Lost;CtxCreations={contextCreationCount};CtxLosses={lossNumber}");
    }

    private void OnSurfaceResized(object? sender, SilkGraphicsSurfaceResizedEventArgs args)
    {
        lock (renderLock)
        {
            surfaceWidth = Math.Max(args.Width, 1);
            surfaceHeight = Math.Max(args.Height, 1);
            renderer?.Resize(surfaceWidth, surfaceHeight);
            Interlocked.Exchange(ref lastMetricsUpdateTimestamp, 0);
        }
    }

    private void OnRenderFrame(object? sender, EventArgs args)
    {
        RenderMetrics metrics;
        lock (renderLock)
        {
            if (scene is null || renderer is null || device is null || skinnedPipeline is null || surfaceWidth <= 0 || surfaceHeight <= 0)
                return;

            var now = Stopwatch.GetTimestamp();
            var dt = lastFrameTimestamp > 0
                ? Math.Clamp((now - lastFrameTimestamp) / (double)Stopwatch.Frequency, 0.0, 0.1)
                : 0.0;
            lastFrameTimestamp = now;

            evaluator?.Update(dt);

            var camera = scene.MainCamera!;
            camera.AspectRatio = surfaceWidth / (float)surfaceHeight;
            UpdateCameraTransform(camera);

            var queue = scene.BuildRenderQueue(camera, surfaceWidth, surfaceHeight);
            renderer.SetCamera(camera.ViewMatrix, camera.ProjectionMatrix);
            var lights = scene.Lights.Count > 0 ? scene.Lights.ToArray() : [];
            renderer.SetLights(lights);
            if (evaluator is not null)
                renderer.SetBoneMatrices(evaluator.BoneMatrices);

            renderer.BeginFrame(new Vector4(0.06f, 0.08f, 0.12f, 1f));
            try
            {
                foreach (var item in queue)
                {
                    if (item.Mesh is null)
                        continue;

                    var meshResource = gpuResourceCache.GetOrCreateMesh(item.Mesh, (renderDevice, mesh) =>
                    {
                        var buffers = SilkMeshBuffers.Create(renderDevice, mesh);
                        return new MeshRenderResource(buffers.VertexBuffer, buffers.IndexBuffer);
                    });

                    var useSkinned = item.Mesh.Skinning is not null;
                    renderer.SetPipeline(useSkinned ? skinnedPipeline : (staticPipeline ?? skinnedPipeline));
                    renderer.SetWorldMatrix(Matrix4x4.Identity);
                    renderer.BindMaterial(item.Material ?? new Material3D { BaseColor = new Vector4(0.85f, 0.88f, 0.95f, 1f) });

                    var indexCount = meshResource.IndexBuffer.Description.IndexCount;
                    renderer.DrawIndexed(meshResource.VertexBuffer, meshResource.IndexBuffer, indexCount);
                }
            }
            finally
            {
                renderer.EndFrame();
            }

            metrics = ((SilkRenderer3D)renderer).Metrics;
            Interlocked.Increment(ref totalRenderedFrames);
        }

        UpdateDiagnosticsSnapshot(metrics, force: false);
    }

    private void UpdateCameraTransform(Camera3D camera)
    {
        var cosPitch = MathF.Cos(cameraPitch);
        var offset = new Vector3(
            MathF.Sin(cameraYaw) * cosPitch * cameraDistance,
            MathF.Sin(cameraPitch) * cameraDistance,
            MathF.Cos(cameraYaw) * cosPitch * cameraDistance);
        var eye = cameraTarget + offset;
        camera.Transform.Position = eye;
        var view = Matrix4x4.CreateLookAt(eye, cameraTarget, Vector3.UnitY);
        if (Matrix4x4.Invert(view, out var world))
            camera.Transform.Rotation = Quaternion.CreateFromRotationMatrix(world);
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Running)
        {
            cameraYaw -= (float)e.TotalX * 0.005f;
            cameraPitch = Math.Clamp(cameraPitch + (float)e.TotalY * 0.005f, -1.2f, 1.2f);
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Running)
            cameraDistance = Math.Clamp(cameraDistance * (float)(2.0 - e.Scale), 20f, 1000f);
    }

    private void OnPlayClicked(object? sender, EventArgs e)
    {
        lock (renderLock) { evaluator?.Play(); }
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnPauseClicked(object? sender, EventArgs e)
    {
        lock (renderLock) { evaluator?.Pause(); }
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnResumeClicked(object? sender, EventArgs e)
    {
        lock (renderLock) { evaluator?.Resume(); }
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        lock (renderLock) { evaluator?.Stop(); }
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnLoopClicked(object? sender, EventArgs e)
    {
        bool looping;
        lock (renderLock)
        {
            if (evaluator is null) return;
            evaluator.IsLooping = !evaluator.IsLooping;
            looping = evaluator.IsLooping;
        }
        loopButton.Text = looping ? "Loop: On" : "Loop: Off";
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnSpeedClicked(object? sender, EventArgs e)
    {
        speedPresetIndex = (speedPresetIndex + 1) % SpeedPresets.Length;
        var speed = SpeedPresets[speedPresetIndex];
        lock (renderLock)
        {
            if (evaluator is not null)
                evaluator.PlaybackSpeed = speed;
        }
        speedButton.Text = string.Format(CultureInfo.InvariantCulture, "Speed: {0:0.0}x", speed);
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnClipSelectionChanged(object? sender, EventArgs e)
    {
        var idx = clipPicker.SelectedIndex;
        if (idx < 0) return;
        lock (renderLock)
        {
            if (evaluator is not null && idx < evaluator.Clips.Count)
                evaluator.SelectClip(idx);
        }
        UpdateDiagnosticsSnapshot(null, force: true);
    }

    private void OnRecreateContextClicked(object? sender, EventArgs e)
    {
        InvokeSurfaceMethod("SuspendSurface");
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () =>
        {
            InvokeSurfaceMethod("ResumeSurface");
        });
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

    private void UpdateDiagnosticsSnapshot(RenderMetrics? metrics, bool force)
    {
        var now = Stopwatch.GetTimestamp();
        if (!force)
        {
            var prev = Interlocked.Read(ref lastMetricsUpdateTimestamp);
            if (prev != 0 && (now - prev) * 1000.0 / Stopwatch.Frequency < 600.0)
                return;
        }
        Interlocked.Exchange(ref lastMetricsUpdateTimestamp, now);

        string text;
        lock (renderLock)
        {
            var inv = CultureInfo.InvariantCulture;
            var frames = Interlocked.Read(ref totalRenderedFrames);
            var hasContext = graphicsContext is not null;
            var hasAssets = loadedModel is not null;
            var boneCount = loadedModel?.Skeleton?.BoneCount ?? 0;
            var nodeCount = loadedModel?.Skeleton?.Nodes.Count ?? 0;
            var clipCount = loadedModel?.Animations.Count ?? 0;
            var clipName = evaluator?.SelectedClip?.Name ?? "None";
            var state = evaluator?.State.ToString() ?? "Stopped";
            var time = evaluator?.CurrentTimeInSeconds ?? 0.0;
            var duration = evaluator?.DurationInSeconds ?? 0.0;
            var speed = evaluator?.PlaybackSpeed ?? 1.0f;
            var loop = evaluator?.IsLooping ?? true;
            var poseHash = evaluator is not null ? ComputePoseHash(evaluator.BoneMatrices) : 0u;
            var drawCalls = metrics?.DrawCalls ?? 0;
            var triangles = metrics?.TriangleCount ?? 0;
            var fps = metrics?.FramesPerSecond ?? 0.0;
            var phase = hasContext && hasAssets && frames > 0 ? "Rendering" : (hasContext ? "ContextCreated" : (hasAssets ? "AssetsLoaded" : "Importing"));

            text = string.Format(
                inv,
                "Phase={0};Context={1};Device=Silk;Assets={2};Skeleton={3};Bones={4};Nodes={5};Clips={6};SelectedClip={7};State={8};Time={9:F3};Duration={10:F3};Speed={11:0.0};Loop={12};PoseHash={13:X8};DrawCalls={14};Triangles={15};FPS={16:F1};Frames={17};CtxCreations={18};CtxLosses={19};Viewport={20}x{21}",
                phase,
                hasContext ? "Ready" : "Pending",
                hasAssets ? "Loaded" : "Pending",
                boneCount > 0 ? "Ready" : "None",
                boneCount,
                nodeCount,
                clipCount,
                clipName,
                state,
                time,
                duration,
                speed,
                loop,
                poseHash,
                drawCalls,
                triangles,
                fps,
                frames,
                contextCreationCount,
                contextLossCount,
                surfaceWidth,
                surfaceHeight);
        }

        SetDiagnosticsText(text);
    }

    private static uint ComputePoseHash(ReadOnlySpan<Matrix4x4> matrices)
    {
        uint hash = 2166136261u;
        for (var i = 0; i < matrices.Length; i++)
        {
            var m = matrices[i];
            hash = (hash ^ (uint)Math.Round(m.M41 * 1000f)) * 16777619u;
            hash = (hash ^ (uint)Math.Round(m.M42 * 1000f)) * 16777619u;
            hash = (hash ^ (uint)Math.Round(m.M43 * 1000f)) * 16777619u;
            hash = (hash ^ (uint)Math.Round(m.M12 * 1000f)) * 16777619u;
        }
        return hash;
    }

    private void SetStatusText(string text)
    {
        MainThread.BeginInvokeOnMainThread(() => statusLabel.Text = text);
    }

    private void SetDiagnosticsText(string text)
    {
        MainThread.BeginInvokeOnMainThread(() => diagnosticsLabel.Text = text);
    }
}
