using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Interactions;
using SilkTriangleSample.UITests.Infrastructure;
using SilkTriangleSample.UITests.iOS.Pages;

namespace SilkTriangleSample.UITests.MacCatalyst.Pages;

/// <summary>
/// Page Object encapsulating the Mac Catalyst Silk graphics surface, runtime diagnostics, camera gestures, and lifecycle actions.
/// </summary>
public sealed class MacCatalystGraphicSurfacePage : BasePage
{
    private static readonly By SurfaceSelector = MobileBy.AccessibilityId("SilkGraphicsSurface");
    private static readonly By StatusLabelSelector = MobileBy.AccessibilityId("StatusLabel");
    private static readonly By DiagnosticsLabelSelector = MobileBy.AccessibilityId("DiagnosticsLabel");

    public MacCatalystGraphicSurfacePage(AppiumDriver driver)
        : base(driver, PlatformKind.MacCatalyst)
    {
    }

    public bool WaitForSurfaceVisible(TimeSpan? timeout = null) =>
        WaitForElement(SurfaceSelector, timeout).Displayed;

    public System.Drawing.Rectangle GetSurfaceRect(TimeSpan? timeout = null) =>
        WaitForElement(SurfaceSelector, timeout).Rect;

    public string GetStatusText(TimeSpan? timeout = null) =>
        GetElementText(StatusLabelSelector, timeout);

    public string GetDiagnosticsText(TimeSpan? timeout = null) =>
        GetElementText(DiagnosticsLabelSelector, timeout);

    public string WaitForContextReady(TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text => text.Contains("Context=Ready", StringComparison.Ordinal) &&
                    !text.Contains("Phase=Failed", StringComparison.Ordinal),
            "Context=Ready without Phase=Failed",
            timeout);

    public string WaitForAssetsLoaded(TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text => text.Contains("Assets=Loaded", StringComparison.Ordinal) &&
                    !text.Contains("Phase=Failed", StringComparison.Ordinal),
            "Assets=Loaded without Phase=Failed",
            timeout);

    public string WaitForRenderingProgress(TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text =>
            {
                var parsed = IosGraphicSurfacePage.ParseDiagnostics(text);
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

    public string WaitForFrameAdvance(long baselineFrames, long minimumDelta = 3, TimeSpan? timeout = null) =>
        WaitForText(
            DiagnosticsLabelSelector,
            text =>
            {
                var parsed = IosGraphicSurfacePage.ParseDiagnostics(text);
                return parsed.TryGetValue("Phase", out var phase) &&
                       string.Equals(phase, "Rendering", StringComparison.Ordinal) &&
                       parsed.TryGetValue("Frames", out var framesStr) &&
                       long.TryParse(framesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames) &&
                       frames >= baselineFrames + minimumDelta;
            },
            $"Frames >= {baselineFrames + minimumDelta}",
            timeout);

    public void PerformCameraOrbitGesture(int deltaX = 140, int deltaY = -70)
    {
        var surfaceElement = WaitForElement(SurfaceSelector);
        var rect = surfaceElement.Rect;
        var startX = rect.X + (rect.Width / 2);
        var startY = rect.Y + (rect.Height / 2);
        var endX = Math.Clamp(startX + deltaX, rect.X + 20, rect.X + rect.Width - 20);
        var endY = Math.Clamp(startY + deltaY, rect.Y + 20, rect.Y + rect.Height - 20);

        var pointer = new PointerInputDevice(PointerKind.Mouse, "mouse");
        var sequence = new ActionSequence(pointer, 0);
        sequence.AddAction(pointer.CreatePointerMove(CoordinateOrigin.Viewport, startX, startY, TimeSpan.Zero));
        sequence.AddAction(pointer.CreatePointerDown(MouseButton.Left));
        sequence.AddAction(pointer.CreatePause(TimeSpan.FromMilliseconds(100)));

        const int steps = 5;
        for (var i = 1; i <= steps; i++)
        {
            var stepX = startX + ((endX - startX) * i / steps);
            var stepY = startY + ((endY - startY) * i / steps);
            sequence.AddAction(pointer.CreatePointerMove(CoordinateOrigin.Viewport, stepX, stepY, TimeSpan.FromMilliseconds(90)));
        }

        sequence.AddAction(pointer.CreatePointerUp(MouseButton.Left));
        Driver.PerformActions([sequence]);
    }
}
