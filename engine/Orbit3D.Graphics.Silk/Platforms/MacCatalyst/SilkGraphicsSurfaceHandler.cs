#if MACCATALYST
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using ObjCRuntime;
using Silk.NET.OpenGL;
using UIKit;

#pragma warning disable CA1422

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Hosts the native Mac Catalyst OpenGL surface used by the Silk backend.
/// </summary>
public sealed class SilkGraphicsSurfaceHandler : ViewHandler<SilkGraphicsSurface, SilkMacSurfaceView>
{
    public static readonly IPropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler> Mapper =
        new PropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>(ViewHandler.ViewMapper);

    public SilkGraphicsSurfaceHandler() : base(Mapper)
    {
    }

    protected override SilkMacSurfaceView CreatePlatformView() => new(VirtualView);

    protected override void DisconnectHandler(SilkMacSurfaceView platformView)
    {
        platformView.ReleaseSurface();
        platformView.UntrackSurface();
        base.DisconnectHandler(platformView);
    }
}

/// <summary>
/// MAUI Mac Catalyst view that hosts a Core Animation <c>CAOpenGLLayer</c> backed by a desktop OpenGL 4.1 core-profile CGL context.
/// UIKit on Mac Catalyst exposes neither EAGL/OpenGL ES nor <c>NSOpenGLContext</c>, so the layer subclass is created with the
/// Objective-C runtime, its pixel format and draw callbacks are routed to this view, and the context the compositor makes current
/// is adopted by <see cref="SilkNativeContextLifetime"/> so the existing <see cref="SilkGraphicsSurface"/> events and the
/// backend-neutral renderer are reused unchanged. Frames are scheduled by a <c>CADisplayLink</c> on the main thread and the
/// compositor presents the layer after each draw callback returns.
/// </summary>
public sealed class SilkMacSurfaceView : UIView
{
    private const string OpenGlFrameworkPath = "/System/Library/Frameworks/OpenGL.framework/OpenGL";
    private const string ObjcLibraryPath = "/usr/lib/libobjc.dylib";
    private const string LayerClassName = "OrbitSilkOpenGLLayer";
    private const int PixelFormatDoubleBuffer = 5;
    private const int PixelFormatColorSize = 8;
    private const int PixelFormatAlphaSize = 11;
    private const int PixelFormatDepthSize = 12;
    private const int PixelFormatAccelerated = 73;
    private const int PixelFormatOpenGlProfile = 99;
    private const int OpenGlProfile41Core = 0x4100;

    private static readonly nint OpenGlLibrary = NativeLibrary.Load(OpenGlFrameworkPath);
    private static readonly List<WeakReference<SilkMacSurfaceView>> Instances = [];
    private static readonly ConcurrentDictionary<nint, SilkMacSurfaceView> ViewsByLayer = new();
    private static readonly CopyPixelFormatCallback CopyPixelFormatImplementation = CopyPixelFormatForDisplayMask;
    private static readonly CanDrawCallback CanDrawImplementation = CanDrawInContext;
    private static readonly DrawCallback DrawImplementation = DrawInContext;
    private static readonly string? CaptureDirectory = GetCaptureDirectory();
    private static readonly nint LayerClassHandle = RegisterLayerClass();

    private readonly SilkGraphicsSurface surface;
    private readonly object syncLock = new();
    private readonly SilkNativeContextLifetime contextLifetime;
    private CALayer? glLayer;
    private nint glLayerHandle;
    private CADisplayLink? displayLink;
    private int allocatedWidth;
    private int allocatedHeight;
    private bool isSuspended;
    private bool isDisposed;
    private bool hasReportedContextInfo;
    private long frameCounter;

    public SilkMacSurfaceView(SilkGraphicsSurface surface)
    {
        this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
        contextLifetime = new SilkNativeContextLifetime(
            surface,
            GetProcAddress,
            CGLGetCurrentContext,
            handle => CGLSetCurrentContext(handle),
            handle => CGLRetainContext(handle),
            handle => CGLReleaseContext(handle),
            isOpenGles: false);
        Opaque = true;
        ClipsToBounds = true;
        UserInteractionEnabled = true;
        MultipleTouchEnabled = true;
        BackgroundColor = UIColor.FromRGB(10, 15, 23);
        Track(this);
    }

    /// <summary>Gets whether this surface currently owns a live native graphics context (diagnostics only).</summary>
    public bool HasGraphicsContext
    {
        get
        {
            lock (syncLock)
                return contextLifetime.Context is not null;
        }
    }

    public override void MovedToWindow()
    {
        base.MovedToWindow();
        lock (syncLock)
        {
            if (Window is null)
            {
                if (displayLink is not null)
                    displayLink.Paused = true;
                return;
            }

            if (!isSuspended && !isDisposed)
                StartRendering();
        }
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        lock (syncLock)
            ApplyLayerGeometry();
    }

    /// <summary>Stops frame scheduling and finishes outstanding GPU work while keeping the native context alive.</summary>
    public void PauseRendering()
    {
        lock (syncLock)
        {
            Console.WriteLine("[SilkMacCatalyst] Rendering paused (application resigned active).");
            if (displayLink is not null)
                displayLink.Paused = true;

            var context = contextLifetime.Context;
            if (context is not null && !context.IsApiDisposed)
            {
                CGLSetCurrentContext(contextLifetime.NativeContext);
                context.Api.Finish();
                CGLSetCurrentContext(0);
            }
        }
    }

    /// <summary>Destroys the native context and layer; the surface is recreated by <see cref="ResumeSurface"/>.</summary>
    public void SuspendSurface()
    {
        lock (syncLock)
        {
            Console.WriteLine("[SilkMacCatalyst] Surface suspended; releasing native context and layer.");
            isSuspended = true;
            ReleaseSurfaceCore();
        }
    }

    /// <summary>Recreates the native layer and context after <see cref="SuspendSurface"/>.</summary>
    public void ResumeSurface()
    {
        lock (syncLock)
        {
            if (isDisposed)
                return;

            isSuspended = false;
            if (Window is null)
                return;

            Console.WriteLine($"[SilkMacCatalyst] Surface resumed; hasContext={contextLifetime.Context is not null}.");
            StartRendering();
        }
    }

    /// <summary>Releases the native context, layer and frame scheduler. Safe to call repeatedly.</summary>
    public void ReleaseSurface()
    {
        lock (syncLock)
            ReleaseSurfaceCore();
    }

    public void UntrackSurface()
    {
        lock (Instances)
        {
            Instances.RemoveAll(reference => !reference.TryGetTarget(out var view) || ReferenceEquals(view, this));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (syncLock)
            {
                isDisposed = true;
                ReleaseSurfaceCore();
            }

            UntrackSurface();
        }

        base.Dispose(disposing);
    }

    internal static void PauseAll() => ForEach(static view => view.PauseRendering());

    internal static void SuspendAll() => ForEach(static view => view.SuspendSurface());

    internal static void ResumeAll() => ForEach(static view => view.ResumeSurface());

    internal static void ReleaseAll() => ForEach(static view => view.ReleaseSurface());

    private static void Track(SilkMacSurfaceView view)
    {
        lock (Instances)
        {
            Instances.RemoveAll(reference => !reference.TryGetTarget(out _));
            Instances.Add(new WeakReference<SilkMacSurfaceView>(view));
        }
    }

    private static void ForEach(Action<SilkMacSurfaceView> action)
    {
        SilkMacSurfaceView[] views;
        lock (Instances)
        {
            views = Instances
                .Select(reference => reference.TryGetTarget(out var view) ? view : null)
                .Where(view => view is not null)
                .Cast<SilkMacSurfaceView>()
                .ToArray();
            Instances.RemoveAll(reference => !reference.TryGetTarget(out _));
        }

        foreach (var view in views)
            action(view);
    }

    private static string? GetCaptureDirectory()
    {
        var directory = Environment.GetEnvironmentVariable("ORBIT3D_SILK_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static nint GetProcAddress(string name) =>
        NativeLibrary.TryGetExport(OpenGlLibrary, name, out var export) ? export : 0;

    private void StartRendering()
    {
        EnsureLayer();
        if (displayLink is null)
        {
            displayLink = CADisplayLink.Create(RenderFrameTick);
            displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }

        displayLink.Paused = false;
    }

    private void EnsureLayer()
    {
        if (glLayer is not null)
            return;

        var handle = objc_msgSend(objc_msgSend(LayerClassHandle, sel_registerName("alloc")), sel_registerName("init"));
        if (handle == 0)
            throw new InvalidOperationException("Failed to create the Mac Catalyst CAOpenGLLayer.");

        var layer = Runtime.GetNSObject<CALayer>(handle)
            ?? throw new InvalidOperationException("Failed to wrap the Mac Catalyst CAOpenGLLayer.");
        objc_msgSend(handle, sel_registerName("release"));

        glLayer = layer;
        glLayerHandle = handle;
        ViewsByLayer[handle] = this;
        layer.Opaque = true;
        layer.NeedsDisplayOnBoundsChange = true;
        layer.MasksToBounds = true;
        Layer.AddSublayer(layer);
        ApplyLayerGeometry();
    }

    private void ApplyLayerGeometry()
    {
        if (glLayer is null)
            return;

        var scale = Window?.Screen?.Scale ?? UIScreen.MainScreen.Scale;
        if (scale <= 0)
            scale = 1f;

        CATransaction.Begin();
        CATransaction.DisableActions = true;
        glLayer.Frame = Bounds;
        glLayer.ContentsScale = scale;
        CATransaction.Commit();
    }

    private void RenderFrameTick()
    {
        lock (syncLock)
        {
            if (isSuspended || isDisposed || glLayer is null || !surface.IsVisible)
                return;
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;

            glLayer.SetNeedsDisplay();
        }
    }

    private void OnDrawInContext(nint nativeContext)
    {
        lock (syncLock)
        {
            if (isSuspended || isDisposed || glLayer is null)
                return;

            try
            {
                DrawFrame(nativeContext);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"[SilkMacCatalyst] Draw callback failed: {exception}");
            }
        }
    }

    private void DrawFrame(nint nativeContext)
    {
        var created = contextLifetime.Adopt(nativeContext);
        var context = contextLifetime.Context!;
        var gl = context.Api;
        if (created)
            allocatedWidth = allocatedHeight = 0;

        UpdateDrawableState(context, gl, created);
        if (!surface.IsVisible || allocatedWidth <= 0 || allocatedHeight <= 0)
            return;

        surface.RaiseRenderFrame();
        frameCounter++;
        if (CaptureDirectory is not null)
            TryCaptureRequestedFrame(gl, allocatedWidth, allocatedHeight);
        gl.Flush();
    }

    /// <summary>
    /// Validation hook, active only when <c>ORBIT3D_SILK_CAPTURE_DIR</c> is set: when the file <c>capture.request</c> exists in that
    /// directory, the just-rendered frame is read back once and saved as <c>&lt;request text&gt;.png</c>. Normal rendering never reads back.
    /// </summary>
    private static void TryCaptureRequestedFrame(GL gl, int width, int height)
    {
        var requestPath = Path.Combine(CaptureDirectory!, "capture.request");
        if (!File.Exists(requestPath))
            return;

        try
        {
            var name = File.ReadAllText(requestPath).Trim();
            File.Delete(requestPath);
            if (string.IsNullOrEmpty(name))
                name = $"frame-{DateTime.Now:yyyyMMdd_HHmmss}";

            var rowBytes = width * 4;
            var pixels = new byte[rowBytes * height];
            gl.Finish();
            gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
            gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.AsSpan());

            var flipped = new byte[pixels.Length];
            for (var row = 0; row < height; row++)
                System.Buffer.BlockCopy(pixels, row * rowBytes, flipped, (height - 1 - row) * rowBytes, rowBytes);

            using var colorSpace = CGColorSpace.CreateDeviceRGB();
            using var bitmap = new CGBitmapContext(flipped, width, height, 8, rowBytes, colorSpace, CGImageAlphaInfo.NoneSkipLast);
            using var image = bitmap.ToImage();
            using var png = UIImage.FromImage(image!).AsPNG();
            png!.Save(Path.Combine(CaptureDirectory!, name + ".png"), true);
            Console.WriteLine($"[SilkMacCatalyst] Captured {width}x{height} frame to {name}.png");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[SilkMacCatalyst] Frame capture failed: {exception}");
            try
            {
                File.Delete(requestPath);
            }
            catch (IOException)
            {
                // The request file cannot be consumed; the next request will report the failure again.
            }
        }
    }

    private void UpdateDrawableState(SilkGraphicsContext context, GL gl, bool created)
    {
        gl.GetInteger(GLEnum.FramebufferBinding, out var framebuffer);
        context.DefaultFramebuffer = (uint)framebuffer;

        Span<int> viewport = stackalloc int[4];
        gl.GetInteger(GLEnum.Viewport, viewport);
        var width = viewport[2];
        var height = viewport[3];
        if (created && !hasReportedContextInfo)
        {
            hasReportedContextInfo = true;
            ReportContextInfo(gl, framebuffer, width, height);
        }

        if (width <= 0 || height <= 0 || (width == allocatedWidth && height == allocatedHeight))
            return;

        allocatedWidth = width;
        allocatedHeight = height;
        surface.RaiseSurfaceResized(width, height);
    }

    private void ReportContextInfo(GL gl, int framebuffer, int width, int height)
    {
        var depthBits = 0;
        gl.GetFramebufferAttachmentParameter(
            FramebufferTarget.Framebuffer,
            framebuffer == 0 ? (FramebufferAttachment)GLEnum.Depth : FramebufferAttachment.DepthAttachment,
            FramebufferAttachmentParameterName.DepthSize,
            out depthBits);
        var scale = Window?.Screen?.Scale ?? UIScreen.MainScreen.Scale;
        Console.WriteLine(
            $"[SilkMacCatalyst] GL context: version='{gl.GetStringS(StringName.Version)}' renderer='{gl.GetStringS(StringName.Renderer)}' " +
            $"glsl='{gl.GetStringS(StringName.ShadingLanguageVersion)}' framebuffer={framebuffer} depthBits={depthBits} " +
            $"viewport={width}x{height} bounds={Bounds.Width:0.#}x{Bounds.Height:0.#} scale={scale:0.##}");
    }

    private void ReleaseSurfaceCore()
    {
        if (displayLink is not null)
        {
            displayLink.Paused = true;
            displayLink.Invalidate();
            displayLink = null;
        }

        try
        {
            contextLifetime.Release();
        }
        finally
        {
            allocatedWidth = 0;
            allocatedHeight = 0;
            hasReportedContextInfo = false;
            DetachLayer();
        }
    }

    private void DetachLayer()
    {
        var layer = glLayer;
        if (layer is null)
            return;

        ViewsByLayer.TryRemove(glLayerHandle, out _);
        glLayer = null;
        glLayerHandle = 0;
        layer.RemoveFromSuperLayer();
        layer.Dispose();
    }

    [MonoPInvokeCallback(typeof(CopyPixelFormatCallback))]
    private static nint CopyPixelFormatForDisplayMask(nint self, nint selector, uint displayMask)
    {
        int[] attributes =
        [
            PixelFormatOpenGlProfile, OpenGlProfile41Core,
            PixelFormatDoubleBuffer,
            PixelFormatColorSize, 24,
            PixelFormatAlphaSize, 8,
            PixelFormatDepthSize, 24,
            PixelFormatAccelerated,
            0
        ];
        var error = CGLChoosePixelFormat(attributes, out var pixelFormat, out var count);
        if (error != 0 || count <= 0 || pixelFormat == 0)
        {
            Console.Error.WriteLine($"[SilkMacCatalyst] CGLChoosePixelFormat(OpenGL 4.1 core) failed with CGLError {error}.");
            return 0;
        }

        return pixelFormat;
    }

    [MonoPInvokeCallback(typeof(CanDrawCallback))]
    private static byte CanDrawInContext(nint self, nint selector, nint context, nint pixelFormat, double layerTime, nint displayTime) =>
        ViewsByLayer.ContainsKey(self) ? (byte)1 : (byte)0;

    [MonoPInvokeCallback(typeof(DrawCallback))]
    private static void DrawInContext(nint self, nint selector, nint context, nint pixelFormat, double layerTime, nint displayTime)
    {
        if (ViewsByLayer.TryGetValue(self, out var view))
            view.OnDrawInContext(context);
    }

    private static nint RegisterLayerClass()
    {
        var superclass = objc_getClass("CAOpenGLLayer");
        if (superclass == 0)
            throw new PlatformNotSupportedException("CAOpenGLLayer is not available in this Mac Catalyst runtime.");

        var layerClass = objc_allocateClassPair(superclass, LayerClassName, 0);
        if (layerClass == 0)
            layerClass = objc_getClass(LayerClassName);
        else
        {
            AddMethod(layerClass, "copyCGLPixelFormatForDisplayMask:", CopyPixelFormatImplementation, "^v@:I");
            AddMethod(layerClass, "canDrawInCGLContext:pixelFormat:forLayerTime:displayTime:", CanDrawImplementation, "c@:^v^vd^v");
            AddMethod(layerClass, "drawInCGLContext:pixelFormat:forLayerTime:displayTime:", DrawImplementation, "v@:^v^vd^v");
            objc_registerClassPair(layerClass);
        }

        return layerClass;
    }

    private static void AddMethod(nint layerClass, string selector, Delegate implementation, string typeEncoding)
    {
        if (class_addMethod(layerClass, sel_registerName(selector), Marshal.GetFunctionPointerForDelegate(implementation), typeEncoding) == 0)
            throw new InvalidOperationException($"Failed to add Objective-C method '{selector}' to {LayerClassName}.");
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint CopyPixelFormatCallback(nint self, nint selector, uint displayMask);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte CanDrawCallback(nint self, nint selector, nint context, nint pixelFormat, double layerTime, nint displayTime);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DrawCallback(nint self, nint selector, nint context, nint pixelFormat, double layerTime, nint displayTime);

    [DllImport(ObjcLibraryPath)]
    private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjcLibraryPath)]
    private static extern nint objc_allocateClassPair(nint superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);

    [DllImport(ObjcLibraryPath)]
    private static extern void objc_registerClassPair(nint cls);

    [DllImport(ObjcLibraryPath)]
    private static extern byte class_addMethod(nint cls, nint name, nint implementation, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(ObjcLibraryPath)]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjcLibraryPath)]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(OpenGlFrameworkPath)]
    private static extern int CGLChoosePixelFormat(int[] attributes, out nint pixelFormat, out int count);

    [DllImport(OpenGlFrameworkPath)]
    private static extern int CGLSetCurrentContext(nint context);

    [DllImport(OpenGlFrameworkPath)]
    private static extern nint CGLGetCurrentContext();

    [DllImport(OpenGlFrameworkPath)]
    private static extern nint CGLRetainContext(nint context);

    [DllImport(OpenGlFrameworkPath)]
    private static extern void CGLReleaseContext(nint context);
}

#pragma warning restore CA1422
#endif
