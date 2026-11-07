#if IOS
using System.Runtime.InteropServices;
using CoreAnimation;
using Foundation;
using Microsoft.Maui.Handlers;
using ObjCRuntime;
using OpenGLES;
using Silk.NET.OpenGL;
using UIKit;

#pragma warning disable CA1422

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Hosts the native iOS OpenGL ES surface used by the Silk backend.
/// </summary>
public sealed class SilkGraphicsSurfaceHandler : ViewHandler<SilkGraphicsSurface, SilkIosSurfaceView>
{
    public static readonly IPropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler> Mapper =
        new PropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>(ViewHandler.ViewMapper);

    public SilkGraphicsSurfaceHandler() : base(Mapper)
    {
    }

    protected override SilkIosSurfaceView CreatePlatformView() => new(VirtualView);

    protected override void DisconnectHandler(SilkIosSurfaceView platformView)
    {
        platformView.ReleaseSurface();
        platformView.UntrackSurface();
        base.DisconnectHandler(platformView);
    }
}

/// <summary>
/// MAUI iOS view that owns a CAEAGLLayer-backed GLES 3 context and coordinates render callbacks with the existing runtime scene path.
/// </summary>
public sealed class SilkIosSurfaceView : UIView
{
    private const string OpenGlesFrameworkPath = "/System/Library/Frameworks/OpenGLES.framework/OpenGLES";
    private static readonly nint OpenGlesLibrary = NativeLibrary.Load(OpenGlesFrameworkPath);
    private static readonly List<WeakReference<SilkIosSurfaceView>> Instances = [];

    [Export("layerClass")]
    public static ObjCRuntime.Class LayerClass() => new(typeof(CAEAGLLayer));

    private readonly SilkGraphicsSurface surface;
    private readonly object syncLock = new();
    private EAGLContext? nativeContext;
    private CAEAGLLayer? nativeLayer;
    private CADisplayLink? displayLink;
    private SilkGraphicsContext? graphicsContext;
    private uint framebuffer;
    private uint colorRenderbuffer;
    private uint depthRenderbuffer;
    private int pixelWidth;
    private int pixelHeight;
    private int allocatedWidth;
    private int allocatedHeight;
    private bool isSuspended;

    public SilkIosSurfaceView(SilkGraphicsSurface surface)
    {
        this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
        Opaque = true;
        ClipsToBounds = true;
        UserInteractionEnabled = true;
        MultipleTouchEnabled = true;
        ContentScaleFactor = UIScreen.MainScreen.Scale;
        BackgroundColor = UIColor.Clear;
        Layer.MasksToBounds = true;
        SetupNativeLayer();
        Track(this);
        InitializeGraphicsContext();
    }

    public override void MovedToWindow()
    {
        base.MovedToWindow();
        if (Window is null)
        {
            if (displayLink is not null)
                displayLink.Paused = true;
            return;
        }

        if (!isSuspended)
        {
            InitializeGraphicsContext();
            if (displayLink is not null)
                displayLink.Paused = false;
            UpdateSurfaceDimensions();
        }
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        if (nativeLayer is null)
            return;

        nativeLayer.Frame = Bounds;
        var scale = ContentScaleFactor > 0 ? ContentScaleFactor : UIScreen.MainScreen.Scale;
        if (scale <= 0)
            scale = 1f;

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var width = Math.Max(1, (int)Math.Round(Bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Round(Bounds.Height * scale));
        if (width != pixelWidth || height != pixelHeight || framebuffer == 0)
        {
            pixelWidth = width;
            pixelHeight = height;
            UpdateSurfaceDimensions();
        }
    }

    public void PauseRendering()
    {
        lock (syncLock)
        {
            if (displayLink is not null)
                displayLink.Paused = true;

            if (nativeContext is not null && graphicsContext is not null && !graphicsContext.IsApiDisposed)
            {
                if (EAGLContext.CurrentContext != nativeContext)
                    EAGLContext.SetCurrentContext(nativeContext);
                graphicsContext.Api.Finish();
            }
        }
    }

    public void SuspendSurface()
    {
        lock (syncLock)
        {
            isSuspended = true;
            ReleaseSurfaceCore();
        }
    }

    public void ResumeSurface()
    {
        lock (syncLock)
        {
            isSuspended = false;
            if (Window is null)
                return;

            InitializeGraphicsContext();
            UpdateSurfaceDimensions(force: true);
            if (displayLink is not null)
                displayLink.Paused = false;
        }
    }

    public void ReleaseSurface()
    {
        lock (syncLock)
        {
            ReleaseSurfaceCore();
        }
    }

    public void UntrackSurface()
    {
        lock (Instances)
        {
            Instances.RemoveAll(reference => !reference.TryGetTarget(out var view) || ReferenceEquals(view, this));
        }
    }

    internal static void PauseAll() => ForEach(static view => view.PauseRendering());

    internal static void SuspendAll() => ForEach(static view => view.SuspendSurface());

    internal static void ResumeAll() => ForEach(static view => view.ResumeSurface());

    private static void Track(SilkIosSurfaceView view)
    {
        lock (Instances)
        {
            Instances.RemoveAll(reference => !reference.TryGetTarget(out _));
            Instances.Add(new WeakReference<SilkIosSurfaceView>(view));
        }
    }

    private static void ForEach(Action<SilkIosSurfaceView> action)
    {
        SilkIosSurfaceView[] views;
        lock (Instances)
        {
            views = Instances
                .Select(reference => reference.TryGetTarget(out var view) ? view : null)
                .Where(view => view is not null)
                .Cast<SilkIosSurfaceView>()
                .ToArray();
            Instances.RemoveAll(reference => !reference.TryGetTarget(out _));
        }

        foreach (var view in views)
            action(view);
    }

    private void SetupNativeLayer()
    {
        nativeLayer = (CAEAGLLayer)Layer;
        nativeLayer.Opaque = true;
        nativeLayer.ContentsScale = UIScreen.MainScreen.Scale;
        nativeLayer.DrawableProperties = new NSDictionary(
            EAGLDrawableProperty.RetainedBacking, NSNumber.FromBoolean(false),
            EAGLDrawableProperty.ColorFormat, EAGLColorFormat.RGBA8);
    }

    private void InitializeGraphicsContext()
    {
        lock (syncLock)
        {
            if (isSuspended || nativeContext is not null)
                return;

            var context = new EAGLContext(EAGLRenderingAPI.OpenGLES3);
            if (context is null)
                throw new InvalidOperationException("The iOS device does not expose a supported OpenGL ES 3 context.");

            nativeContext = context;
            EAGLContext.SetCurrentContext(context);
            graphicsContext = new SilkGraphicsContext(
                GetProcAddress,
                () => EAGLContext.CurrentContext == context,
                () =>
                {
                    if (colorRenderbuffer != 0 && graphicsContext is not null && !graphicsContext.IsApiDisposed)
                        graphicsContext.Api.BindRenderbuffer(RenderbufferTarget.Renderbuffer, colorRenderbuffer);
                    context.PresentRenderBuffer((uint)RenderbufferTarget.Renderbuffer);
                },
                isOpenGles: true);

            surface.RaiseContextCreated(graphicsContext);
            if (Bounds.Width > 0 && Bounds.Height > 0)
                UpdateSurfaceDimensions(force: true);

            displayLink = CADisplayLink.Create(RenderFrameTick);
            displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }
    }

    private static nint GetProcAddress(string name) =>
        NativeLibrary.TryGetExport(OpenGlesLibrary, name, out var export) ? export : 0;

    private void RenderFrameTick()
    {
        lock (syncLock)
        {
            if (isSuspended || nativeContext is null || graphicsContext is null || !surface.IsVisible)
                return;

            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;

            if (EAGLContext.CurrentContext != nativeContext)
                EAGLContext.SetCurrentContext(nativeContext);

            UpdateSurfaceDimensions();
            if (framebuffer == 0 || allocatedWidth <= 0 || allocatedHeight <= 0)
                return;

            graphicsContext.DefaultFramebuffer = framebuffer;
            surface.RaiseRenderFrame();
        }
    }

    private void UpdateSurfaceDimensions(bool force = false)
    {
        if (isSuspended || nativeContext is null || graphicsContext is null || nativeLayer is null)
            return;

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var scale = ContentScaleFactor > 0 ? ContentScaleFactor : UIScreen.MainScreen.Scale;
        if (scale <= 0)
            scale = 1f;

        var requestedWidth = Math.Max(1, (int)Math.Round(Bounds.Width * scale));
        var requestedHeight = Math.Max(1, (int)Math.Round(Bounds.Height * scale));
        pixelWidth = requestedWidth;
        pixelHeight = requestedHeight;

        if (!force && framebuffer != 0 && requestedWidth == allocatedWidth && requestedHeight == allocatedHeight)
            return;

        if (EAGLContext.CurrentContext != nativeContext)
            EAGLContext.SetCurrentContext(nativeContext);

        nativeLayer.Frame = Bounds;
        nativeLayer.ContentsScale = scale;

        var gl = graphicsContext.Api;
        if (framebuffer == 0)
            framebuffer = gl.GenFramebuffer();
        if (colorRenderbuffer == 0)
            colorRenderbuffer = gl.GenRenderbuffer();
        if (depthRenderbuffer == 0)
            depthRenderbuffer = gl.GenRenderbuffer();

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, colorRenderbuffer);
        if (!nativeContext.RenderBufferStorage((uint)RenderbufferTarget.Renderbuffer, nativeLayer))
            throw new InvalidOperationException("Failed to allocate CAEAGLLayer color renderbuffer storage on iOS.");

        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            RenderbufferTarget.Renderbuffer,
            colorRenderbuffer);

        gl.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, RenderbufferParameterName.Width, out var backingWidth);
        gl.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, RenderbufferParameterName.Height, out var backingHeight);
        var actualWidth = backingWidth > 0 ? backingWidth : requestedWidth;
        var actualHeight = backingHeight > 0 ? backingHeight : requestedHeight;

        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRenderbuffer);
        gl.RenderbufferStorage(
            RenderbufferTarget.Renderbuffer,
            InternalFormat.DepthComponent24,
            (uint)actualWidth,
            (uint)actualHeight);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer,
            depthRenderbuffer);

        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, colorRenderbuffer);

        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"The iOS OpenGL ES framebuffer is incomplete ({status}).");

        graphicsContext.DefaultFramebuffer = framebuffer;
        allocatedWidth = actualWidth;
        allocatedHeight = actualHeight;
        surface.RaiseSurfaceResized(actualWidth, actualHeight);
    }

    private void ReleaseSurfaceCore()
    {
        if (displayLink is not null)
        {
            displayLink.Paused = true;
            displayLink.Invalidate();
            displayLink = null;
        }

        var context = graphicsContext;
        var native = nativeContext;
        graphicsContext = null;
        nativeContext = null;
        allocatedWidth = 0;
        allocatedHeight = 0;

        try
        {
            if (native is not null)
            {
                EAGLContext.SetCurrentContext(native);
                if (context is not null && !context.IsApiDisposed)
                {
                    var gl = context.Api;
                    gl.Finish();
                    gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
                    if (framebuffer != 0)
                    {
                        gl.DeleteFramebuffer(framebuffer);
                        framebuffer = 0;
                    }
                    if (colorRenderbuffer != 0)
                    {
                        gl.DeleteRenderbuffer(colorRenderbuffer);
                        colorRenderbuffer = 0;
                    }
                    if (depthRenderbuffer != 0)
                    {
                        gl.DeleteRenderbuffer(depthRenderbuffer);
                        depthRenderbuffer = 0;
                    }
                }

                if (context is not null)
                    surface.RaiseContextLost(context);
            }
        }
        finally
        {
            context?.DisposeApi();
            if (native is not null)
            {
                if (EAGLContext.CurrentContext == native)
                    EAGLContext.SetCurrentContext(null);
                native.Dispose();
            }

            framebuffer = 0;
            colorRenderbuffer = 0;
            depthRenderbuffer = 0;
        }
    }
}

#pragma warning restore CA1422
#endif
