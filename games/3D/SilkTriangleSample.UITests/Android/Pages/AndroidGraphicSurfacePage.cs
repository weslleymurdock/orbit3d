using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using SilkTriangleSample.UITests.Infrastructure;

namespace SilkTriangleSample.UITests.Android.Pages;

/// <summary>
/// Page Object encapsulating the Android Silk graphics surface and runtime diagnostics.
/// </summary>
public sealed class AndroidGraphicSurfacePage : BasePage
{
    private static readonly By SurfaceSelector = MobileBy.AccessibilityId("SilkGraphicsSurface");
    private static readonly By StatusLabelSelector = MobileBy.AccessibilityId("StatusLabel");
    private static readonly By DiagnosticsLabelSelector = MobileBy.AccessibilityId("DiagnosticsLabel");

    public AndroidGraphicSurfacePage(AppiumDriver driver)
        : base(driver, PlatformKind.Android)
    {
    }

    /// <summary>
    /// Waits until the native Silk graphics surface element is visible.
    /// </summary>
    public bool WaitForSurfaceVisible(TimeSpan? timeout = null)
    {
        return WaitForElement(SurfaceSelector, timeout).Displayed;
    }

    /// <summary>
    /// Gets the human-readable status text from StatusLabel.
    /// </summary>
    public string GetStatusText()
    {
        return GetElementText(StatusLabelSelector);
    }

    /// <summary>
    /// Gets the structured diagnostic string from DiagnosticsLabel.
    /// Format: Phase=...;Context=...;Assets=...;DrawCalls=...;Triangles=...;FPS=...;Frames=...
    /// </summary>
    public string GetDiagnosticsText()
    {
        return GetElementText(DiagnosticsLabelSelector);
    }

    /// <summary>
    /// Waits until the renderer confirms active rendering progress (Phase=Rendering, DrawCalls > 0).
    /// </summary>
    public string WaitForRenderingProgress(TimeSpan? timeout = null)
    {
        return WaitForText(
            DiagnosticsLabelSelector,
            text => text.Contains("Phase=Rendering") && !text.Contains("DrawCalls=0"),
            "Phase=Rendering with DrawCalls > 0",
            timeout);
    }

    /// <summary>
    /// Waits until the scene assets are confirmed loaded.
    /// </summary>
    public string WaitForAssetsLoaded(TimeSpan? timeout = null)
    {
        return WaitForText(
            DiagnosticsLabelSelector,
            text => text.Contains("Assets=Loaded"),
            "Assets=Loaded",
            timeout);
    }

    /// <summary>
    /// Checks whether the graphics surface is currently displayed.
    /// </summary>
    public bool IsSurfaceDisplayed() => IsElementDisplayed(SurfaceSelector);
}

