namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Tracks the single native OpenGL context that backs a <see cref="SilkGraphicsSurface"/> when the platform compositor,
/// rather than Orbit, creates the context (for example <c>CAOpenGLLayer</c> on Mac Catalyst).
/// It guarantees that a context is wrapped once, that <see cref="SilkGraphicsSurface.ContextLost"/> is raised while the lost
/// native context is still current, and that every retained native context is released exactly once.
/// All members must be used from the graphics thread.
/// </summary>
internal sealed class SilkNativeContextLifetime
{
    private readonly Action<SilkGraphicsContext> raiseContextCreated;
    private readonly Action<SilkGraphicsContext> raiseContextLost;
    private readonly Func<string, nint> getProcAddress;
    private readonly Func<nint> getCurrentContext;
    private readonly Action<nint> setCurrentContext;
    private readonly Action<nint> retainContext;
    private readonly Action<nint> releaseContext;
    private readonly bool isOpenGles;
    private nint nativeContext;
    private SilkGraphicsContext? context;

    /// <summary>Creates a lifetime tracker bound to <paramref name="surface"/>.</summary>
    /// <param name="surface">Surface that receives context created/lost notifications.</param>
    /// <param name="getProcAddress">Native OpenGL function resolver.</param>
    /// <param name="getCurrentContext">Returns the native context handle current on the calling thread, or 0.</param>
    /// <param name="setCurrentContext">Makes a native context current on the calling thread; 0 clears the current context.</param>
    /// <param name="retainContext">Adds an ownership reference to a native context.</param>
    /// <param name="releaseContext">Drops an ownership reference previously added by <paramref name="retainContext"/>.</param>
    /// <param name="isOpenGles">Whether the native contexts implement OpenGL ES semantics.</param>
    public SilkNativeContextLifetime(
        SilkGraphicsSurface surface,
        Func<string, nint> getProcAddress,
        Func<nint> getCurrentContext,
        Action<nint> setCurrentContext,
        Action<nint> retainContext,
        Action<nint> releaseContext,
        bool isOpenGles)
        : this(
            surface is not null ? surface.RaiseContextCreated : throw new ArgumentNullException(nameof(surface)),
            surface.RaiseContextLost,
            getProcAddress,
            getCurrentContext,
            setCurrentContext,
            retainContext,
            releaseContext,
            isOpenGles)
    {
    }

    internal SilkNativeContextLifetime(
        Action<SilkGraphicsContext> raiseContextCreated,
        Action<SilkGraphicsContext> raiseContextLost,
        Func<string, nint> getProcAddress,
        Func<nint> getCurrentContext,
        Action<nint> setCurrentContext,
        Action<nint> retainContext,
        Action<nint> releaseContext,
        bool isOpenGles)
    {
        this.raiseContextCreated = raiseContextCreated ?? throw new ArgumentNullException(nameof(raiseContextCreated));
        this.raiseContextLost = raiseContextLost ?? throw new ArgumentNullException(nameof(raiseContextLost));
        this.getProcAddress = getProcAddress ?? throw new ArgumentNullException(nameof(getProcAddress));
        this.getCurrentContext = getCurrentContext ?? throw new ArgumentNullException(nameof(getCurrentContext));
        this.setCurrentContext = setCurrentContext ?? throw new ArgumentNullException(nameof(setCurrentContext));
        this.retainContext = retainContext ?? throw new ArgumentNullException(nameof(retainContext));
        this.releaseContext = releaseContext ?? throw new ArgumentNullException(nameof(releaseContext));
        this.isOpenGles = isOpenGles;
    }

    /// <summary>Gets the Silk context wrapping the adopted native context, or <see langword="null"/> when none is active.</summary>
    public SilkGraphicsContext? Context => context;

    /// <summary>Gets the adopted native context handle, or 0 when none is active.</summary>
    public nint NativeContext => nativeContext;

    /// <summary>
    /// Adopts the native context that the platform made current for a draw callback.
    /// A context that is already adopted is left untouched; a different context first retires the previous one.
    /// </summary>
    /// <returns><see langword="true"/> when a new Silk context was created and <see cref="SilkGraphicsSurface.ContextCreated"/> was raised.</returns>
    public bool Adopt(nint currentNativeContext)
    {
        if (currentNativeContext == 0)
            throw new ArgumentException("A native context handle is required.", nameof(currentNativeContext));
        if (context is not null && nativeContext == currentNativeContext)
            return false;

        if (context is not null || nativeContext != 0)
            Retire(restoreCurrent: currentNativeContext);

        retainContext(currentNativeContext);
        nativeContext = currentNativeContext;
        setCurrentContext(currentNativeContext);
        var adopted = nativeContext;
        context = new SilkGraphicsContext(
            getProcAddress,
            () => getCurrentContext() == adopted,
            present: null,
            isOpenGles,
            presentAfterRenderCallback: true);
        raiseContextCreated(context);
        return true;
    }

    /// <summary>
    /// Raises <see cref="SilkGraphicsSurface.ContextLost"/> with the native context current, disposes the Silk API and releases
    /// the native context. Calling this method when nothing is adopted is a no-op.
    /// </summary>
    public void Release() => Retire(restoreCurrent: 0);

    private void Retire(nint restoreCurrent)
    {
        var lostContext = context;
        var lostNative = nativeContext;
        context = null;
        nativeContext = 0;
        if (lostNative == 0)
            return;

        try
        {
            setCurrentContext(lostNative);
            if (lostContext is not null)
                raiseContextLost(lostContext);
        }
        finally
        {
            lostContext?.DisposeApi();
            if (getCurrentContext() == lostNative)
                setCurrentContext(restoreCurrent);
            releaseContext(lostNative);
        }
    }
}
