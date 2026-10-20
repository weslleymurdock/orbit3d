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
        base.DisconnectHandler(platformView);
    }
}

/// <summary>
/// MAUI iOS view that owns a CAEAGLLayer-backed GLES 3 context and coordinates render callbacks with the existing runtime scene path.
/// </summary>
public sealed class SilkIosSurfaceView : UIView
{
    [Export("layerClass")]
    public static ObjCRuntime.Class LayerClass() => new(typeof(CAEAGLLayer));

    private readonly SilkGraphicsSurface surface;
    private readonly object syncLock = new();
    private EAGLContext? nativeContext;
    private CAEAGLLayer? nativeLayer;
    private CADisplayLink? displayLink;
    private SilkGraphicsContext? graphicsContext;
    private uint framebuffer;
    private uint renderbuffer;
    private int pixelWidth;
    private int pixelHeight;

    public SilkIosSurfaceView(SilkGraphicsSurface surface)
    {
        this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
        Opaque = true;
        ClipsToBounds = true;
        ContentScaleFactor = UIScreen.MainScreen.Scale;
        BackgroundColor = UIColor.Clear;
        Layer.MasksToBounds = true;
        SetupNativeLayer();
        InitializeGraphicsContext();
        LayoutSubviews();
    }

    public void ReleaseSurface()
    {
        displayLink?.Invalidate();
        displayLink = null;

        var context = graphicsContext;
        if (context is not null)
        {
            surface.RaiseContextLost(context);
            context.DisposeApi();
            graphicsContext = null;
        }

        if (nativeContext is not null)
        {
            EAGLContext.SetCurrentContext(null);
            nativeContext.Dispose();
            nativeContext = null;
        }

        framebuffer = 0;
        renderbuffer = 0;
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        if (nativeLayer is not null)
        {
            nativeLayer.Frame = Bounds;
            var scale = ContentScaleFactor > 0 ? ContentScaleFactor : 1f;
            var width = Math.Max(1, (int)Math.Round(Bounds.Width * scale));
            var height = Math.Max(1, (int)Math.Round(Bounds.Height * scale));
            if (width != pixelWidth || height != pixelHeight)
            {
                pixelWidth = width;
                pixelHeight = height;
                UpdateSurfaceDimensions();
            }
        }
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
            if (nativeContext is not null)
                return;

            var context = new EAGLContext(EAGLRenderingAPI.OpenGLES3);
            if (context is null)
                throw new InvalidOperationException("The iOS device does not expose a supported OpenGL ES 3 context.");

            nativeContext = context;
            EAGLContext.SetCurrentContext(context);
            graphicsContext = new SilkGraphicsContext(
                GetProcAddress,
                () => EAGLContext.CurrentContext == context,
                () => context.PresentRenderBuffer((uint)RenderbufferTarget.Renderbuffer),
                isOpenGles: true);

            surface.RaiseContextCreated(graphicsContext);
            UpdateSurfaceDimensions();

            displayLink = CADisplayLink.Create(RenderFrameTick);
            displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }
    }

    private static nint GetProcAddress(string name)
    {
        const string openGlesFramework = "/System/Library/Frameworks/OpenGLES.framework/OpenGLES";
        var handle = NativeLibrary.Load(openGlesFramework);
        return NativeLibrary.GetExport(handle, name);
    }

    private void RenderFrameTick()
    {
        if (nativeContext is null || graphicsContext is null)
            return;

        if (EAGLContext.CurrentContext != nativeContext)
            EAGLContext.SetCurrentContext(nativeContext);

        UpdateSurfaceDimensions();
        surface.RaiseRenderFrame();
    }

    private void UpdateSurfaceDimensions()
    {
        if (nativeContext is null || graphicsContext is null || nativeLayer is null)
            return;

        var width = Math.Max(1, pixelWidth);
        var height = Math.Max(1, pixelHeight);
        if (width <= 0 || height <= 0)
            return;

        EAGLContext.SetCurrentContext(nativeContext);
        var gl = graphicsContext.Api;
        if (framebuffer == 0)
            framebuffer = gl.GenFramebuffer();
        if (renderbuffer == 0)
            renderbuffer = gl.GenRenderbuffer();

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderbuffer);
        nativeContext.RenderBufferStorage((uint)RenderbufferTarget.Renderbuffer, nativeLayer);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, renderbuffer);

        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"The iOS OpenGL ES framebuffer is incomplete ({status}).");

        surface.RaiseSurfaceResized(width, height);
    }
}

#pragma warning restore CA1422
#endif
