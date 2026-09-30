#if ANDROID
using System.Runtime.InteropServices;
using Android.Content;
using Android.Opengl;
using Android.Views;
using Microsoft.Maui.Handlers;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;

namespace Orbit3D.Graphics.Silk;

internal sealed class SilkGraphicsSurfaceHandler : ViewHandler<SilkGraphicsSurface, SilkGLSurfaceView>
{
    public static readonly IPropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler> Mapper =
        new PropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>(ViewHandler.ViewMapper);

    public SilkGraphicsSurfaceHandler() : base(Mapper)
    {
    }

    protected override SilkGLSurfaceView CreatePlatformView() => new(Context!, VirtualView);

    protected override void ConnectHandler(SilkGLSurfaceView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.OnResume();
    }

    protected override void DisconnectHandler(SilkGLSurfaceView platformView)
    {
        platformView.OnPause();
        platformView.ReleaseSurface();
        platformView.UntrackSurface();
        base.DisconnectHandler(platformView);
    }
}

internal sealed class SilkGLSurfaceView : GLSurfaceView
{
    private readonly SurfaceRenderer _renderer;

    public SilkGLSurfaceView(Context context, SilkGraphicsSurface surface) : base(context)
    {
        _renderer = new SurfaceRenderer(surface);
        SetEGLContextClientVersion(3);
        SetEGLConfigChooser(8, 8, 8, 8, 16, 0);
        PreserveEGLContextOnPause = false;
        SetRenderer(_renderer);
        Track(this);
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

    private sealed class SurfaceRenderer(SilkGraphicsSurface surface) : Java.Lang.Object, GLSurfaceView.IRenderer
    {
        private SilkGraphicsContext? _context;

        public void OnSurfaceCreated(IGL10? gl, Javax.Microedition.Khronos.Egl.EGLConfig? config)
        {
            if (_context is not null)
                surface.RaiseContextLost(_context);

            var currentContext = EGL14.EglGetCurrentContext();
            if (currentContext is null)
                throw new InvalidOperationException("GLSurfaceView did not make an EGL context current.");
            var contextHandle = currentContext.Handle;
            _context = new SilkGraphicsContext(
                GetProcAddress,
                () => EGL14.EglGetCurrentContext()?.Handle == contextHandle,
                present: null,
                isOpenGles: true,
                presentAfterRenderCallback: true);
            surface.RaiseContextCreated(_context);
        }

        public void OnSurfaceChanged(IGL10? gl, int width, int height) =>
            surface.RaiseSurfaceResized(width, height);

        public void OnDrawFrame(IGL10? gl) => surface.RaiseRenderFrame();

        public void Release()
        {
            if (_context is not null)
                surface.RaiseContextLost(_context);
            _context = null;
        }

        private static nint GetProcAddress(string name) => EglGetProcAddress(name);

        [DllImport("EGL", EntryPoint = "eglGetProcAddress", CallingConvention = CallingConvention.Cdecl)]
        private static extern nint EglGetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    }
}
#endif