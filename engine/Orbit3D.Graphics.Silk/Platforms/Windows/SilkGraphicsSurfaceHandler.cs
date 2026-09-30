#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Orbit3D.Graphics.Silk;

internal sealed class SilkGraphicsSurfaceHandler : ViewHandler<SilkGraphicsSurface, SilkWindowsSurfaceView>
{
    public static readonly IPropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler> Mapper =
        new PropertyMapper<SilkGraphicsSurface, SilkGraphicsSurfaceHandler>(ViewHandler.ViewMapper);

    public SilkGraphicsSurfaceHandler() : base(Mapper)
    {
    }

    protected override SilkWindowsSurfaceView CreatePlatformView() => new(VirtualView);

    protected override void DisconnectHandler(SilkWindowsSurfaceView platformView)
    {
        platformView.ReleaseSurface();
        base.DisconnectHandler(platformView);
    }
}

internal sealed class SilkWindowsSurfaceView : Microsoft.UI.Xaml.Controls.Grid
{
    private readonly SilkGraphicsSurface surface;
    private NativeWglContext? nativeContext;
    private SilkGraphicsContext? graphicsContext;
    private Microsoft.UI.Xaml.Window? hostWindow;
    private nint hostWindowHandle;
    private int surfaceX;
    private int surfaceY;
    private int surfaceWidth;
    private int surfaceHeight;
    private bool nativeSurfaceVisible;

    public SilkWindowsSurfaceView(SilkGraphicsSurface surface)
    {
        this.surface = surface;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    public void ReleaseSurface()
    {
        CompositionTarget.Rendering -= OnRendering;
        ReleaseNativeContext();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (nativeContext is not null)
            return;

        var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        hostWindow = mauiWindow?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("The MAUI Windows host window is not available.");
        hostWindow.Closed += OnHostWindowClosed;
        try
        {
            hostWindowHandle = WindowNative.GetWindowHandle(hostWindow);
            if (hostWindowHandle == 0)
                throw new InvalidOperationException("The MAUI Windows host did not provide a native window handle.");

            nativeContext = NativeWglContext.Create(hostWindowHandle);
            graphicsContext = new SilkGraphicsContext(
                nativeContext.GetProcAddress,
                nativeContext.IsCurrent,
                nativeContext.Present,
                isOpenGles: false);
            UpdateSurfaceBounds();
            surface.RaiseContextCreated(graphicsContext);
            CompositionTarget.Rendering += OnRendering;
        }
        catch
        {
            ReleaseNativeContext();
            throw;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => ReleaseNativeContext();

    private void OnHostWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        CompositionTarget.Rendering -= OnRendering;
        ReleaseNativeContext();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => UpdateSurfaceBounds();

    private void OnRendering(object? sender, object args)
    {
        if (nativeContext is null || graphicsContext is null)
            return;

        var visible = Visibility == Microsoft.UI.Xaml.Visibility.Visible;
        if (visible != nativeSurfaceVisible)
        {
            NativeWglContext.Show(nativeContext.WindowHandle, visible);
            nativeSurfaceVisible = visible;
        }
        if (!visible)
            return;

        nativeContext.MakeCurrent();
        UpdateSurfaceBounds();
        if (surfaceWidth > 0 && surfaceHeight > 0 && NativeWglContext.IsWindowVisible(nativeContext.WindowHandle))
            surface.RaiseRenderFrame();
    }

    private void UpdateSurfaceBounds()
    {
        if (nativeContext is null || hostWindow is null || hostWindowHandle == 0)
            return;
        if (Visibility != Microsoft.UI.Xaml.Visibility.Visible)
        {
            NativeWglContext.Show(nativeContext.WindowHandle, false);
            nativeSurfaceVisible = false;
            return;
        }

        var root = hostWindow.Content;
        if (root is null)
            return;

        var origin = TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0));
        var scale = NativeWglContext.GetDpiForWindow(hostWindowHandle) / 96d;
        var clientOrigin = new NativeWglContext.NativePoint(
            (int)Math.Round(origin.X * scale),
            (int)Math.Round(origin.Y * scale));
        if (!NativeWglContext.ClientToScreen(hostWindowHandle, ref clientOrigin))
            throw new InvalidOperationException($"Mapping the Windows OpenGL surface position to screen coordinates failed ({Marshal.GetLastWin32Error()}).");
        var width = (int)Math.Round(ActualWidth * scale);
        var height = (int)Math.Round(ActualHeight * scale);

        if (width <= 0 || height <= 0)
        {
            NativeWglContext.Show(nativeContext.WindowHandle, false);
            nativeSurfaceVisible = false;
            return;
        }
        if (nativeSurfaceVisible &&
            clientOrigin.X == surfaceX &&
            clientOrigin.Y == surfaceY &&
            width == surfaceWidth &&
            height == surfaceHeight)
        {
            return;
        }

        NativeWglContext.SetBounds(nativeContext.WindowHandle, clientOrigin.X, clientOrigin.Y, width, height);
        nativeSurfaceVisible = true;
        surfaceX = clientOrigin.X;
        surfaceY = clientOrigin.Y;
        var dimensions = NativeWglContext.GetClientSize(nativeContext.WindowHandle);
        if (dimensions.Width == surfaceWidth && dimensions.Height == surfaceHeight)
            return;

        surfaceWidth = dimensions.Width;
        surfaceHeight = dimensions.Height;
        if (surfaceWidth > 0 && surfaceHeight > 0)
        {
            nativeContext.MakeCurrent();
            surface.RaiseSurfaceResized(surfaceWidth, surfaceHeight);
        }
    }

    private void ReleaseNativeContext()
    {
        var context = graphicsContext;
        var native = nativeContext;
        if (hostWindow is not null)
            hostWindow.Closed -= OnHostWindowClosed;
        graphicsContext = null;
        nativeContext = null;
        surfaceWidth = 0;
        surfaceHeight = 0;
        surfaceX = 0;
        surfaceY = 0;
        nativeSurfaceVisible = false;

        try
        {
            if (context is not null && native is not null)
            {
                native.MakeCurrent();
                surface.RaiseContextLost(context);
            }

        }
        finally
        {
            native?.Dispose();
            hostWindow = null;
            hostWindowHandle = 0;
        }
    }
}

internal sealed class NativeWglContext : IDisposable
{
    private const int PixelFormatDrawToWindow = 0x00000004;
    private const int PixelFormatSupportOpenGl = 0x00000020;
    private const int PixelFormatDoubleBuffer = 0x00000001;
    private const int WindowStylePopup = unchecked((int)0x80000000);
    private const int WindowStyleVisible = 0x10000000;
    private const int WindowExStyleNoActivate = 0x08000000;
    private const int WindowExStyleToolWindow = 0x00000080;
    private const uint WindowMessageNonClientHitTest = 0x0084;
    private const uint WindowMessageMouseActivate = 0x0021;
    private const nint HitTestTransparent = -1;
    private const nint MouseActivateNoActivate = 3;
    private const uint WindowClassStyleHorizontalRedraw = 0x0002;
    private const uint WindowClassStyleVerticalRedraw = 0x0001;
    private const uint WindowClassStyleOwnDeviceContext = 0x0020;
    private const int ErrorClassAlreadyExists = 1410;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionNoZOrder = 0x0004;
    private const int ShowWindowHide = 0;
    private const int ShowWindowShowNoActivate = 4;
    private const int WglContextMajorVersion = 0x2091;
    private const int WglContextMinorVersion = 0x2092;
    private const int WglContextProfileMask = 0x9126;
    private const int WglContextCoreProfile = 0x00000001;
    private static readonly nint OpenGlLibrary = NativeLibrary.Load("opengl32.dll");
    private static readonly WindowProcedure WindowProc = HandleWindowMessage;
    private static readonly nint ModuleInstance = GetModuleHandle(null);
    private static readonly string WindowClassName = RegisterWindowClass();

    private nint deviceContext;
    private nint renderingContext;
    private bool disposed;

    private NativeWglContext(nint windowHandle, nint deviceContext, nint renderingContext)
    {
        WindowHandle = windowHandle;
        this.deviceContext = deviceContext;
        this.renderingContext = renderingContext;
    }

    public nint WindowHandle { get; }

    public static NativeWglContext Create(nint parentWindow)
    {
        var window = CreateWindowEx(
            WindowExStyleNoActivate | WindowExStyleToolWindow,
            WindowClassName,
            string.Empty,
            WindowStylePopup | WindowStyleVisible,
            0,
            0,
            1,
            1,
            parentWindow,
            0,
            ModuleInstance,
            0);
        if (window == 0)
            throw new InvalidOperationException($"Creating the Windows OpenGL child surface failed ({Marshal.GetLastWin32Error()}).");

        var deviceContext = GetDC(window);
        if (deviceContext == 0)
        {
            DestroyWindow(window);
            throw new InvalidOperationException($"Acquiring the Windows OpenGL device context failed ({Marshal.GetLastWin32Error()}).");
        }

        nint legacyContext = 0;
        nint modernContext = 0;
        try
        {
            var pixelFormat = new PixelFormatDescriptor
            {
                Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
                Version = 1,
                Flags = PixelFormatDrawToWindow | PixelFormatSupportOpenGl | PixelFormatDoubleBuffer,
                PixelType = 0,
                ColorBits = 32,
                AlphaBits = 8,
                DepthBits = 24,
                StencilBits = 8,
                LayerType = 0
            };
            var pixelFormatIndex = ChoosePixelFormat(deviceContext, ref pixelFormat);
            if (pixelFormatIndex == 0 || !SetPixelFormat(deviceContext, pixelFormatIndex, ref pixelFormat))
                throw new InvalidOperationException($"Setting the Windows OpenGL pixel format failed ({Marshal.GetLastWin32Error()}).");

            legacyContext = WglCreateContext(deviceContext);
            if (legacyContext == 0 || !WglMakeCurrent(deviceContext, legacyContext))
                throw new InvalidOperationException($"Creating the Windows OpenGL bootstrap context failed ({Marshal.GetLastWin32Error()}).");

            var createContextAddress = WglGetProcAddress("wglCreateContextAttribsARB");
            if (IsInvalidWglAddress(createContextAddress))
                throw new PlatformNotSupportedException("The Windows OpenGL driver must support WGL_ARB_create_context and OpenGL 3.3.");

            var createContext = Marshal.GetDelegateForFunctionPointer<CreateContextAttribs>(createContextAddress);
            var attributes = new[]
            {
                WglContextMajorVersion, 3,
                WglContextMinorVersion, 3,
                WglContextProfileMask, WglContextCoreProfile,
                0
            };
            var attributesPointer = Marshal.AllocHGlobal(attributes.Length * sizeof(int));
            try
            {
                Marshal.Copy(attributes, 0, attributesPointer, attributes.Length);
                modernContext = createContext(deviceContext, 0, attributesPointer);
            }
            finally
            {
                Marshal.FreeHGlobal(attributesPointer);
            }

            if (modernContext == 0)
                throw new PlatformNotSupportedException($"The Windows OpenGL driver could not create a 3.3 core context ({Marshal.GetLastWin32Error()}).");

            if (!WglMakeCurrent(0, 0))
                throw new InvalidOperationException($"Releasing the bootstrap OpenGL context failed ({Marshal.GetLastWin32Error()}).");
            WglDeleteContext(legacyContext);
            legacyContext = 0;
            if (!WglMakeCurrent(deviceContext, modernContext))
                throw new InvalidOperationException($"Making the OpenGL 3.3 context current failed ({Marshal.GetLastWin32Error()}).");

            var result = new NativeWglContext(window, deviceContext, modernContext);
            modernContext = 0;
            deviceContext = 0;
            return result;
        }
        catch
        {
            if (WglGetCurrentContext() == legacyContext || WglGetCurrentContext() == modernContext)
                WglMakeCurrent(0, 0);
            if (modernContext != 0)
                WglDeleteContext(modernContext);
            if (legacyContext != 0)
                WglDeleteContext(legacyContext);
            ReleaseDC(window, deviceContext);
            DestroyWindow(window);
            throw;
        }
    }

    public bool IsCurrent() => !disposed && WglGetCurrentContext() == renderingContext;

    public void MakeCurrent()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!WglMakeCurrent(deviceContext, renderingContext))
            throw new InvalidOperationException($"Making the Windows OpenGL context current failed ({Marshal.GetLastWin32Error()}).");
    }

    public nint GetProcAddress(string name)
    {
        var address = WglGetProcAddress(name);
        if (!IsInvalidWglAddress(address))
            return address;

        return NativeLibrary.TryGetExport(OpenGlLibrary, name, out var export) ? export : 0;
    }

    public void Present()
    {
        if (!IsCurrent())
            throw new InvalidOperationException("The Windows OpenGL context must be current before presenting.");
        if (!SwapBuffers(deviceContext))
            throw new InvalidOperationException($"Presenting the Windows OpenGL surface failed ({Marshal.GetLastWin32Error()}).");
    }

    public static void SetBounds(nint window, int x, int y, int width, int height)
    {
        if (window == 0)
            return;

        if (width <= 0 || height <= 0)
        {
            ShowWindow(window, ShowWindowHide);
            return;
        }

        if (!SetWindowPos(window, 0, x, y, width, height, SetWindowPositionNoActivate | SetWindowPositionNoZOrder))
            throw new InvalidOperationException($"Resizing the Windows OpenGL surface failed ({Marshal.GetLastWin32Error()}).");
        ShowWindow(window, ShowWindowShowNoActivate);
    }

    public static void Show(nint window, bool visible)
    {
        if (window != 0)
            ShowWindow(window, visible ? ShowWindowShowNoActivate : ShowWindowHide);
    }

    public static bool IsWindowVisible(nint window) => window != 0 && IsWindowVisibleNative(window);

    public static uint GetDpiForWindow(nint window) => GetDpiForWindowNative(window);

    public static (int Width, int Height) GetClientSize(nint window)
    {
        if (!GetClientRect(window, out var rectangle))
            throw new InvalidOperationException($"Reading the Windows OpenGL surface size failed ({Marshal.GetLastWin32Error()}).");
        return (rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (WglGetCurrentContext() == renderingContext)
            WglMakeCurrent(0, 0);
        if (renderingContext != 0)
            WglDeleteContext(renderingContext);
        if (deviceContext != 0)
            ReleaseDC(WindowHandle, deviceContext);
        DestroyWindow(WindowHandle);
        renderingContext = 0;
        deviceContext = 0;
    }

    private static bool IsInvalidWglAddress(nint address) =>
        address == 0 || address == 1 || address == 2 || address == 3 || address == -1;

    private static string RegisterWindowClass()
    {
        const string className = "Orbit3D.Silk.WglSurface";
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Style = WindowClassStyleHorizontalRedraw | WindowClassStyleVerticalRedraw | WindowClassStyleOwnDeviceContext,
            WindowProcedure = WindowProc,
            Instance = ModuleInstance,
            ClassName = className
        };

        if (RegisterClassEx(ref windowClass) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorClassAlreadyExists)
                throw new InvalidOperationException($"Registering the Windows OpenGL surface class failed ({error}).");
        }

        return className;
    }

    private static nint HandleWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        if (message == WindowMessageNonClientHitTest)
            return HitTestTransparent;
        if (message == WindowMessageMouseActivate)
            return MouseActivateNoActivate;
        return DefWindowProc(window, message, wParam, lParam);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint CreateContextAttribs(nint deviceContext, nint sharedContext, nint attributes);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelFormatDescriptor
    {
        public ushort Size;
        public ushort Version;
        public int Flags;
        public byte PixelType;
        public byte ColorBits;
        public byte RedBits;
        public byte RedShift;
        public byte GreenBits;
        public byte GreenShift;
        public byte BlueBits;
        public byte BlueShift;
        public byte AlphaBits;
        public byte AlphaShift;
        public byte AccumulationBits;
        public byte AccumulationRedBits;
        public byte AccumulationGreenBits;
        public byte AccumulationBlueBits;
        public byte AccumulationAlphaBits;
        public byte DepthBits;
        public byte StencilBits;
        public byte AuxiliaryBuffers;
        public byte LayerType;
        public byte Reserved;
        public int LayerMask;
        public int VisibleMask;
        public int DamageMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        int extendedStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool ClientToScreen(nint window, ref NativePoint point);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
    private static extern bool IsWindowVisibleNative(nint window);

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint GetDpiForWindowNative(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetDC(nint window);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int ChoosePixelFormat(nint deviceContext, ref PixelFormatDescriptor descriptor);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SetPixelFormat(nint deviceContext, int format, ref PixelFormatDescriptor descriptor);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SwapBuffers(nint deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("opengl32.dll", EntryPoint = "wglCreateContext", SetLastError = true)]
    private static extern nint WglCreateContext(nint deviceContext);

    [DllImport("opengl32.dll", EntryPoint = "wglDeleteContext", SetLastError = true)]
    private static extern bool WglDeleteContext(nint renderingContext);

    [DllImport("opengl32.dll", EntryPoint = "wglMakeCurrent", SetLastError = true)]
    private static extern bool WglMakeCurrent(nint deviceContext, nint renderingContext);

    [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentContext")]
    private static extern nint WglGetCurrentContext();

    [DllImport("opengl32.dll", EntryPoint = "wglGetProcAddress", CharSet = CharSet.Ansi)]
    private static extern nint WglGetProcAddress(string name);
}
#endif
