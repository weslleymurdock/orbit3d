#if ANDROID
using System.Runtime.InteropServices;
using Android.Content;
using Android.Opengl;
using Android.Util;
using Android.Views;
using Microsoft.Maui.Handlers;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using Silk.NET.OpenGL;

namespace Orbit3D.Graphics.Silk;

public sealed class SilkGraphicsSurfaceHandler : ViewHandler<SilkGraphicsSurface, SilkGLSurfaceView>
{
    public static readonly IPropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler> Mapper =
        new PropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>(ViewHandler.ViewMapper);

    public SilkGraphicsSurfaceHandler() : base(Mapper)
    {
    }

    protected override SilkGLSurfaceView CreatePlatformView()
    {
        Log.Info("Orbit3D", "[Silk] CreatePlatformView");
        return new(Context!, VirtualView);
    }

    protected override void ConnectHandler(SilkGLSurfaceView platformView)
    {
        Log.Info("Orbit3D", "[Silk] ConnectHandler");
        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(SilkGLSurfaceView platformView)
    {
        Log.Info("Orbit3D", "[Silk] DisconnectHandler");
        platformView.OnPause();
        platformView.ReleaseSurface();
        platformView.UntrackSurface();
        base.DisconnectHandler(platformView);
    }
}

public sealed class SilkGLSurfaceView : GLSurfaceView
{
    private readonly SurfaceRenderer _renderer;

    public SilkGLSurfaceView(Context context, SilkGraphicsSurface surface) : base(context)
    {
        Log.Info("Orbit3D", "[Silk] GLSurfaceView ctor");
        _renderer = new SurfaceRenderer(this, surface);
        SetEGLContextClientVersion(3);
        SetEGLConfigChooser(8, 8, 8, 8, 16, 0);
        PreserveEGLContextOnPause = false;
        SetRenderer(_renderer);
        RenderMode = Rendermode.Continuously;
        Track(this);
    }

    public void PauseRendering()
    {
        Log.Info("Orbit3D", "[Silk] PauseRendering");
        RenderMode = Rendermode.WhenDirty;
    }

    public void SuspendSurface()
    {
        Log.Info("Orbit3D", "[Silk] SuspendSurface");
        OnPause();
    }

    public void ResumeSurface()
    {
        Log.Info("Orbit3D", "[Silk] ResumeSurface");
        RenderMode = Rendermode.Continuously;
        OnResume();
    }

    public override void OnPause()
    {
        var activeContext = _renderer.CurrentContext;
        if (activeContext is not null)
        {
            if (activeContext.IsCurrent)
            {
                _renderer.Release(activeContext);
            }
            else
            {
                using var released = new ManualResetEventSlim(false);
                QueueEvent(new Java.Lang.Runnable(() =>
                {
                    try
                    {
                        _renderer.Release(activeContext);
                    }
                    finally
                    {
                        released.Set();
                    }
                }));
                released.Wait(TimeSpan.FromMilliseconds(750));
            }
        }

        base.OnPause();
        if (activeContext is not null)
            _renderer.Release(activeContext);
    }

    public override void OnResume()
    {
        RenderMode = Rendermode.Continuously;
        base.OnResume();
    }

    public void ReleaseSurface() => _renderer.Release();

    public void UntrackSurface()
    {
        lock (Instances)
            Instances.RemoveAll(reference => !reference.TryGetTarget(out var view) || ReferenceEquals(view, this));
    }

    private static readonly List<WeakReference<SilkGLSurfaceView>> Instances = [];

    private static void Track(SilkGLSurfaceView view)
    {
        lock (Instances)
        {
            Instances.RemoveAll(reference => !reference.TryGetTarget(out _));
            Instances.Add(new WeakReference<SilkGLSurfaceView>(view));
        }
    }

    internal static void PauseAll() => ForEach(static view => view.OnPause());

    internal static void ResumeAll() => ForEach(static view => view.OnResume());

    private static void ForEach(Action<SilkGLSurfaceView> action)
    {
        SilkGLSurfaceView[] views;
        lock (Instances)
        {
            views = Instances
                .Select(reference => reference.TryGetTarget(out var view) ? view : null)
                .Where(view => view is not null)
                .Cast<SilkGLSurfaceView>()
                .ToArray();
            Instances.RemoveAll(reference => !reference.TryGetTarget(out _));
        }

        foreach (var view in views)
            action(view);
    }

    private sealed class SurfaceRenderer(SilkGLSurfaceView view, SilkGraphicsSurface surface) : Java.Lang.Object, GLSurfaceView.IRenderer
    {
        private readonly object syncLock = new();
        private SilkGraphicsContext? _context;
        private int surfaceWidth;
        private int surfaceHeight;

        internal SilkGraphicsContext? CurrentContext
        {
            get
            {
                lock (syncLock)
                    return _context;
            }
        }

        public void OnSurfaceCreated(IGL10? gl, Javax.Microedition.Khronos.Egl.EGLConfig? config)
        {
            Log.Info("Orbit3D", "[Silk] OnSurfaceCreated");
            Release();

            var created = CreateContext();
            lock (syncLock)
                _context = created;

            var api = created.Api;
            Log.Info(
                "Orbit3D",
                $"[Silk] GL context: version='{api.GetStringS(StringName.Version)}' renderer='{api.GetStringS(StringName.Renderer)}' glsl='{api.GetStringS(StringName.ShadingLanguageVersion)}'");
            surface.RaiseContextCreated(created);
        }

        public void OnSurfaceChanged(IGL10? gl, int width, int height)
        {
            surfaceWidth = width;
            surfaceHeight = height;
            Log.Info("Orbit3D", $"[Silk] OnSurfaceChanged {width}x{height}");
            surface.RaiseSurfaceResized(width, height);
        }

        public void OnDrawFrame(IGL10? gl)
        {
            if (CurrentContext is null)
            {
                Log.Error("Orbit3D", "[Silk] Render callback arrived without a current EGL context");
                return;
            }

            if (view.Width > 0 && view.Height > 0 &&
                (view.Width != surfaceWidth || view.Height != surfaceHeight))
            {
                surfaceWidth = view.Width;
                surfaceHeight = view.Height;
                surface.RaiseSurfaceResized(view.Width, view.Height);
            }

            surface.RaiseRenderFrame();
        }

        public void Release(SilkGraphicsContext? expectedContext = null)
        {
            SilkGraphicsContext? lost;
            lock (syncLock)
            {
                if (_context is null || (expectedContext is not null && !ReferenceEquals(_context, expectedContext)))
                    return;

                lost = _context;
                _context = null;
            }

            Log.Info("Orbit3D", $"[Silk] Release context; current={lost.IsCurrent}");
            surface.RaiseContextLost(lost);
        }

        private static nint GetProcAddress(string name) => EglGetProcAddress(name);

        private static SilkGraphicsContext CreateContext()
        {
            var contextHandle = GetCurrentEglContextHandle();
            if (contextHandle == 0)
                throw new InvalidOperationException("GLSurfaceView did not make an EGL context current.");

            return new SilkGraphicsContext(
                GetProcAddress,
                () => GetCurrentEglContextHandle() == contextHandle,
                present: null,
                isOpenGles: true,
                presentAfterRenderCallback: true);
        }

        [DllImport("EGL", EntryPoint = "eglGetCurrentContext", CallingConvention = CallingConvention.Cdecl)]
        private static extern nint GetCurrentEglContextHandle();

        [DllImport("EGL", EntryPoint = "eglGetProcAddress", CallingConvention = CallingConvention.Cdecl)]
        private static extern nint EglGetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    }
}
#endif