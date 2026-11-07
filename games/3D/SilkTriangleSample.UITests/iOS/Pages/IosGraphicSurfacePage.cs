using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Interactions;
using SilkTriangleSample.UITests.Infrastructure;

namespace SilkTriangleSample.UITests.iOS.Pages;

/// <summary>
/// Page Object encapsulating the iOS Silk graphics surface, runtime diagnostics, camera gestures, and lifecycle actions.
/// </summary>
public sealed class IosGraphicSurfacePage : BasePage
{
    private static readonly By SurfaceSelector = MobileBy.AccessibilityId("SilkGraphicsSurface");
    private static readonly By StatusLabelSelector = MobileBy.AccessibilityId("StatusLabel");
    private static readonly By DiagnosticsLabelSelector = MobileBy.AccessibilityId("DiagnosticsLabel");

    public IosGraphicSurfacePage(AppiumDriver driver)
        : base(driver, PlatformKind.iOS)
    {
    }

    /// <summary>
    /// Waits until the native Silk graphics surface element is visible.
    /// </summary>
    public bool WaitForSurfaceVisible(TimeSpan? timeout = null) =>
        WaitForElement(SurfaceSelector, timeout).Displayed;

    /// <summary>
    /// Gets the bounding rectangle of the native Silk graphics surface.
    /// </summary>
    public System.Drawing.Rectangle GetSurfaceRect(TimeSpan? timeout = null) =>
        WaitForElement(SurfaceSelector, timeout).Rect;

    /// <summary>
    /// Gets the human-readable status text from StatusLabel.
    /// </summary>
    public string GetStatusText(TimeSpan? timeout = null) =>
        GetElementText(StatusLabelSelector, timeout);

    /// <summary>
    /// Gets the structured diagnostic string from DiagnosticsLabel.
    /// </summary>
    public string GetDiagnosticsText(TimeSpan? timeout = null) =>
        GetElementText(DiagnosticsLabelSelector, timeout);

    /// <summary>
    /// Parses a structured <c>Key=Value;...</c> diagnostics string into a dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseDiagnostics(string diagnostics)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(diagnostics))
            return map;

        foreach (var pair in diagnostics.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = pair[..separatorIndex].Trim();
            var value = pair[(separatorIndex + 1)..].Trim();
            map[key] = value;
        }

        return map;
    }

    /// <summary>
    /// Waits until the native graphics context reports <c>Context=Ready</c>.
    /// </summary>
    public string WaitForContextReady(TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text => text.Contains("Context=Ready", StringComparison.Ordinal) &&
                    !text.Contains("Phase=Failed", StringComparison.Ordinal),
            "Context=Ready without Phase=Failed",
            timeout);

    /// <summary>
    /// Waits until the GLB scene and sidecar texture assets are confirmed loaded.
    /// </summary>
    public string WaitForAssetsLoaded(TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text => text.Contains("Assets=Loaded", StringComparison.Ordinal) &&
                    !text.Contains("Phase=Failed", StringComparison.Ordinal),
            "Assets=Loaded without Phase=Failed",
            timeout);

    /// <summary>
    /// Waits until the renderer confirms active rendering progress (<c>Phase=Rendering</c>, <c>DrawCalls &gt; 0</c>, <c>Frames &gt; 0</c>).
    /// </summary>
    public string WaitForRenderingProgress(TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text =>
            {
                var parsed = ParseDiagnostics(text);
                return parsed.TryGetValue("Phase", out var phase) &&
                       string.Equals(phase, "Rendering", StringComparison.Ordinal) &&
                       parsed.TryGetValue("DrawCalls", out var drawCallsStr) &&
                       int.TryParse(drawCallsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var drawCalls) &&
                       drawCalls > 0 &&
                       parsed.TryGetValue("Frames", out var framesStr) &&
                       long.TryParse(framesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames) &&
                       frames > 0;
            },
            "Phase=Rendering with DrawCalls > 0 and Frames > 0",
            timeout);

    /// <summary>
    /// Waits until rendered frame count advances beyond <paramref name="baselineFrames"/> by at least <paramref name="minimumDelta"/>.
    /// </summary>
    public string WaitForFrameAdvance(long baselineFrames, long minimumDelta = 3, TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text =>
            {
                var parsed = ParseDiagnostics(text);
                return parsed.TryGetValue("Phase", out var phase) &&
                       string.Equals(phase, "Rendering", StringComparison.Ordinal) &&
                       parsed.TryGetValue("Frames", out var framesStr) &&
                       long.TryParse(framesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames) &&
                       frames >= baselineFrames + minimumDelta;
            },
            $"Frames >= {baselineFrames + minimumDelta}",
            timeout);

    /// <summary>
    /// Performs a touch pan/drag gesture across the SilkGraphicsSurface to orbit the camera.
    /// </summary>
    public void PerformCameraOrbitGesture(int deltaX = 140, int deltaY = -70)
    {
        var surfaceElement = WaitForElement(SurfaceSelector);
        var rect = surfaceElement.Rect;
        var startX = rect.X + (rect.Width / 2);
        var startY = rect.Y + (rect.Height / 2);
        var endX = Math.Clamp(startX + deltaX, rect.X + 20, rect.X + rect.Width - 20);
        var endY = Math.Clamp(startY + deltaY, rect.Y + 20, rect.Y + rect.Height - 20);

        var touch = new PointerInputDevice(PointerKind.Touch, "finger1");
        var sequence = new ActionSequence(touch, 0);
        sequence.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, startX, startY, TimeSpan.Zero));
        sequence.AddAction(touch.CreatePointerDown(MouseButton.Left));
        sequence.AddAction(touch.CreatePause(TimeSpan.FromMilliseconds(100)));

        const int steps = 5;
        for (var i = 1; i <= steps; i++)
        {
            var stepX = startX + ((endX - startX) * i / steps);
            var stepY = startY + ((endY - startY) * i / steps);
            sequence.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, stepX, stepY, TimeSpan.FromMilliseconds(90)));
        }

        sequence.AddAction(touch.CreatePointerUp(MouseButton.Left));
        Driver.PerformActions([sequence]);
    }

    /// <summary>
    /// Attempts to rotate the device orientation and returns whether rotation was supported by the session.
    /// </summary>
    public bool TrySetOrientation(ScreenOrientation targetOrientation, out string? failureReason)
    {
        try
        {
            Driver.Orientation = targetOrientation;
            failureReason = null;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Sends the application to the background and resumes it in the foreground.
    /// </summary>
    public void SendToBackgroundAndResume(TimeSpan backgroundDuration)
    {
        var bundleId = AppArtifactLocator.GetDeclaredApplicationId();
        try
        {
            Driver.BackgroundApp(backgroundDuration);
        }
        catch
        {
            Driver.ExecuteScript("mobile: pressButton", new Dictionary<string, object> { ["name"] = "home" });
            Thread.Sleep(backgroundDuration);
            Driver.ExecuteScript("mobile: activateApp", new Dictionary<string, object> { ["bundleId"] = bundleId });
        }
    }

    /// <summary>
    /// Checks whether the graphics surface is currently displayed.
    /// </summary>
    public bool IsSurfaceDisplayed() => IsElementDisplayed(SurfaceSelector);
}
