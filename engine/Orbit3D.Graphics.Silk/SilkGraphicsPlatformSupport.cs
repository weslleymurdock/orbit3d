namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Exposes the platform matrix supported by the Silk graphics backend.
/// </summary>
public static class SilkGraphicsPlatformSupport
{
    /// <summary>
    /// Gets a value indicating whether the current build targets a MAUI platform with a native Silk surface implementation.
    /// </summary>
    public static bool IsCurrentPlatformSupported() =>
#if ANDROID || IOS || WINDOWS
        true;
#else
        false;
#endif
}
