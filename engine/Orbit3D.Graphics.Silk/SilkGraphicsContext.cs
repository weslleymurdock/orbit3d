using Silk.NET.OpenGL;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Describes a native OpenGL/OpenGLES context that is current on the calling render thread.
/// The platform surface owns native context creation and buffer presentation.
/// </summary>
public sealed class SilkGraphicsContext
{
    private readonly Func<bool> isCurrent;
    private readonly Action? present;
    private readonly bool presentAfterRenderCallback;
    private readonly int threadId;
    private bool apiDisposed;

    /// <summary>
    /// Creates a Silk graphics context wrapper for an already-current native context.
    /// </summary>
    /// <param name="getProcAddress">Native function resolver from the platform context.</param>
    /// <param name="isCurrent">Returns whether the native context is current on this thread.</param>
    /// <param name="present">Presents the native surface by swapping its buffers when the host does not present automatically.</param>
    /// <param name="isOpenGles">Whether the context implements OpenGL ES shader rules.</param>
    /// <param name="presentAfterRenderCallback">Whether the platform presents automatically after its render callback returns.</param>
    public SilkGraphicsContext(
        Func<string, nint> getProcAddress,
        Func<bool> isCurrent,
        Action? present,
        bool isOpenGles,
        bool presentAfterRenderCallback = false)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);
        ArgumentNullException.ThrowIfNull(isCurrent);
        if (present is null && !presentAfterRenderCallback)
            throw new ArgumentNullException(nameof(present));
        if (!isCurrent())
            throw new InvalidOperationException("The native OpenGL context must be current before creating Silk resources.");

        this.isCurrent = isCurrent;
        this.present = present;
        this.presentAfterRenderCallback = presentAfterRenderCallback;
        threadId = Environment.CurrentManagedThreadId;
        IsOpenGles = isOpenGles;
        Api = GL.GetApi(getProcAddress);
    }

    internal GL Api { get; }

    /// <summary>Gets whether the context uses OpenGL ES semantics.</summary>
    public bool IsOpenGles { get; }

    /// <summary>Gets whether this context is current on its owning render thread.</summary>
    public bool IsCurrent => Environment.CurrentManagedThreadId == threadId && isCurrent();

    internal bool IsApiDisposed => apiDisposed;

    internal void EnsureCurrent()
    {
        ObjectDisposedException.ThrowIf(apiDisposed, this);
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("OpenGL operations must run on the graphics context thread.");
        if (!isCurrent())
            throw new InvalidOperationException("The native OpenGL context is not current on the graphics context thread.");
    }

    internal void Present()
    {
        EnsureCurrent();
        if (!presentAfterRenderCallback)
            present!();
    }

    internal void DisposeApi()
    {
        if (apiDisposed)
            return;
        apiDisposed = true;
        Api.Dispose();
    }
}