using Silk.NET.OpenGL;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Describes a native OpenGL/OpenGLES context that is current on the calling render thread.
/// The platform surface owns native context creation and buffer presentation.
/// </summary>
public sealed class SilkGraphicsContext
{
    private readonly Func<bool> _isCurrent;
    private readonly Action _present;
    private readonly int _threadId;

    /// <summary>
    /// Creates a Silk graphics context wrapper for an already-current native context.
    /// </summary>
    /// <param name="getProcAddress">Native function resolver from the platform context.</param>
    /// <param name="isCurrent">Returns whether the native context is current on this thread.</param>
    /// <param name="present">Presents the native surface by swapping its buffers.</param>
    /// <param name="isOpenGles">Whether the context implements OpenGL ES shader rules.</param>
    public SilkGraphicsContext(
        Func<string, nint> getProcAddress,
        Func<bool> isCurrent,
        Action present,
        bool isOpenGles)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentNullException.ThrowIfNull(present);
        if (!isCurrent())
            throw new InvalidOperationException("The native OpenGL context must be current before creating Silk resources.");

        _isCurrent = isCurrent;
        _present = present;
        _threadId = Environment.CurrentManagedThreadId;
        IsOpenGles = isOpenGles;
        Api = GL.GetApi(getProcAddress);
    }

    internal GL Api { get; }

    /// <summary>Gets whether the context uses OpenGL ES semantics.</summary>
    public bool IsOpenGles { get; }

    /// <summary>Gets whether this context is current on its owning render thread.</summary>
    public bool IsCurrent => Environment.CurrentManagedThreadId == _threadId && _isCurrent();

    internal void EnsureCurrent()
    {
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new InvalidOperationException("OpenGL operations must run on the graphics context thread.");
        if (!_isCurrent())
            throw new InvalidOperationException("The native OpenGL context is not current on the graphics context thread.");
    }

    internal void Present()
    {
        EnsureCurrent();
        _present();
    }

    internal void DisposeApi() => Api.Dispose();
}