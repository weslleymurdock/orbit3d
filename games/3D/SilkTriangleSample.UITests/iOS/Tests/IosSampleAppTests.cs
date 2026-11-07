using System.Globalization;
using OpenQA.Selenium;
using SilkTriangleSample.UITests.Infrastructure;
using SilkTriangleSample.UITests.iOS.Pages;

namespace SilkTriangleSample.UITests.iOS.Tests;

public sealed class IosTestFixture : PlatformTestFixture
{
    public IosTestFixture() : base(PlatformKind.iOS) { }
}

[Trait("Platform", "iOS")]
public sealed class IosSampleAppTests : IClassFixture<IosTestFixture>
{
    private readonly IosTestFixture fixture;

    public IosSampleAppTests(IosTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Test01_ApplicationLaunch_DisplaysSurfaceAndStatusWithoutErrors()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosGraphicSurfacePage(driver);

        try
        {
            Assert.True(page.WaitForSurfaceVisible(TimeSpan.FromSeconds(45)), "SilkGraphicsSurface should be visible after launch.");
            var status = page.GetStatusText(TimeSpan.FromSeconds(15));
            Assert.False(string.IsNullOrWhiteSpace(status), "StatusLabel should not be empty upon launch.");
            Assert.False(status.StartsWith("Import failed", StringComparison.OrdinalIgnoreCase), $"Asset import failed: {status}");

            var screenshotPath = page.CaptureScreenshot("01-app-launch");
            Assert.NotNull(screenshotPath);
            Assert.True(File.Exists(screenshotPath), $"Launch screenshot should exist at '{screenshotPath}'.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.iOS, nameof(Test01_ApplicationLaunch_DisplaysSurfaceAndStatusWithoutErrors), ex);
            throw;
        }
    }

    [Fact]
    public void Test02_GraphicsInitialization_CreatesNativeSurfaceAndSilkContext()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosGraphicSurfacePage(driver);

        try
        {
            var diagnosticsText = page.WaitForContextReady(TimeSpan.FromSeconds(45));
            var diagnostics = IosGraphicSurfacePage.ParseDiagnostics(diagnosticsText);

            Assert.Equal("Ready", diagnostics.GetValueOrDefault("Context"));
            Assert.NotEqual("Failed", diagnostics.GetValueOrDefault("Phase"));

            var surfaceRect = page.GetSurfaceRect();
            Assert.True(surfaceRect.Width > 0 && surfaceRect.Height > 0, $"Native surface bounds must be positive, got {surfaceRect.Width}x{surfaceRect.Height}.");

            var screenshotPath = page.CaptureScreenshot("02-graphics-initialized");
            Assert.NotNull(screenshotPath);
            Assert.True(File.Exists(screenshotPath), $"Graphics initialization screenshot should exist at '{screenshotPath}'.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.iOS, nameof(Test02_GraphicsInitialization_CreatesNativeSurfaceAndSilkContext), ex);
            throw;
        }
    }

    [Fact]
    public void Test03_SceneAndAssetLoading_LoadsGlbModelAndSidecarTexture()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosGraphicSurfacePage(driver);

        try
        {
            var diagnosticsText = page.WaitForAssetsLoaded(TimeSpan.FromSeconds(45));
            var diagnostics = IosGraphicSurfacePage.ParseDiagnostics(diagnosticsText);

            Assert.Equal("Loaded", diagnostics.GetValueOrDefault("Assets"));
            Assert.True(
                diagnostics.TryGetValue("Meshes", out var meshesStr) &&
                int.TryParse(meshesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var meshCount) &&
                meshCount > 0,
                $"Expected Meshes > 0 in diagnostics: '{diagnosticsText}'.");

            if (diagnostics.TryGetValue("Texture", out var textureDim))
            {
                Assert.False(string.Equals(textureDim, "0x0", StringComparison.Ordinal), $"Expected decoded sidecar texture dimensions, got '{textureDim}'.");
            }

            var screenshotPath = page.CaptureScreenshot("03-scene-rendered");
            Assert.NotNull(screenshotPath);
            Assert.True(File.Exists(screenshotPath), $"Scene rendered screenshot should exist at '{screenshotPath}'.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.iOS, nameof(Test03_SceneAndAssetLoading_LoadsGlbModelAndSidecarTexture), ex);
            throw;
        }
    }

    [Fact]
    public void Test04_RenderingProgress_AdvancesFramesAndReportsMetrics()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosGraphicSurfacePage(driver);

        try
        {
            var initialText = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
            var initial = IosGraphicSurfacePage.ParseDiagnostics(initialText);

            Assert.Equal("Rendering", initial.GetValueOrDefault("Phase"));
            Assert.Equal("Ready", initial.GetValueOrDefault("Context"));
            Assert.True(
                int.TryParse(initial.GetValueOrDefault("DrawCalls"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var drawCalls) &&
                drawCalls > 0,
                $"Expected DrawCalls > 0 in '{initialText}'.");
            Assert.True(
                int.TryParse(initial.GetValueOrDefault("Triangles"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var triangles) &&
                triangles > 0,
                $"Expected Triangles > 0 in '{initialText}'.");
            Assert.True(
                long.TryParse(initial.GetValueOrDefault("Frames"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var baselineFrames) &&
                baselineFrames > 0,
                $"Expected initial Frames > 0 in '{initialText}'.");

            var advancedText = page.WaitForFrameAdvance(baselineFrames, minimumDelta: 5, timeout: TimeSpan.FromSeconds(20));
            var advanced = IosGraphicSurfacePage.ParseDiagnostics(advancedText);
            var advancedFrames = long.Parse(advanced["Frames"], CultureInfo.InvariantCulture);

            Assert.True(advancedFrames > baselineFrames, $"Expected frame count to advance beyond {baselineFrames}, got {advancedFrames}.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.iOS, nameof(Test04_RenderingProgress_AdvancesFramesAndReportsMetrics), ex);
            throw;
        }
    }

    [Fact]
    public void Test05_CameraInteraction_UpdatesCameraStateAndContinuesRendering()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosGraphicSurfacePage(driver);

        try
        {
            var beforeText = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
            var before = IosGraphicSurfacePage.ParseDiagnostics(beforeText);
            var framesBefore = long.Parse(before["Frames"], CultureInfo.InvariantCulture);
            var yawBefore = before.GetValueOrDefault("Yaw", "0.00");
            var pitchBefore = before.GetValueOrDefault("Pitch", "0.00");

            page.PerformCameraOrbitGesture(deltaX: 150, deltaY: -80);

            var afterText = page.WaitForFrameAdvance(framesBefore, minimumDelta: 5, timeout: TimeSpan.FromSeconds(20));
            var after = IosGraphicSurfacePage.ParseDiagnostics(afterText);
            var yawAfter = after.GetValueOrDefault("Yaw", "0.00");
            var pitchAfter = after.GetValueOrDefault("Pitch", "0.00");

            Assert.True(
                !string.Equals(yawBefore, yawAfter, StringComparison.Ordinal) ||
                !string.Equals(pitchBefore, pitchAfter, StringComparison.Ordinal),
                $"Expected camera Yaw/Pitch to change after pan gesture (Before: Yaw={yawBefore}, Pitch={pitchBefore}; After: Yaw={yawAfter}, Pitch={pitchAfter}).");

            var screenshotPath = page.CaptureScreenshot("04-camera-interaction");
            Assert.NotNull(screenshotPath);
            Assert.True(File.Exists(screenshotPath), $"Camera interaction screenshot should exist at '{screenshotPath}'.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.iOS, nameof(Test05_CameraInteraction_UpdatesCameraStateAndContinuesRendering), ex);
            throw;
        }
    }

    [Fact]
    public void Test06_OrientationAndViewport_UpdatesDimensionsAndContinuesRendering()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosGraphicSurfacePage(driver);

        try
        {
            var initialText = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
            var initial = IosGraphicSurfacePage.ParseDiagnostics(initialText);
            var initialViewport = initial.GetValueOrDefault("Viewport", string.Empty);
            Assert.False(string.IsNullOrWhiteSpace(initialViewport), $"Expected Viewport dimension in '{initialText}'.");

            if (page.TrySetOrientation(ScreenOrientation.Landscape, out var failureReason))
            {
                try
                {
                    var rotatedText = page.WaitForText(
                        OpenQA.Selenium.Appium.MobileBy.AccessibilityId("DiagnosticsLabel"),
                        text =>
                        {
                            var parsed = IosGraphicSurfacePage.ParseDiagnostics(text);
                            return parsed.TryGetValue("Viewport", out var vp) &&
                                   !string.Equals(vp, initialViewport, StringComparison.Ordinal) &&
                                   parsed.TryGetValue("Phase", out var phase) &&
                                   string.Equals(phase, "Rendering", StringComparison.Ordinal);
                        },
                        $"Viewport changed from {initialViewport} in Landscape",
                        TimeSpan.FromSeconds(20));

                    var rotated = IosGraphicSurfacePage.ParseDiagnostics(rotatedText);
                    var framesAfterRotate = long.Parse(rotated["Frames"], CultureInfo.InvariantCulture);
                    page.WaitForFrameAdvance(framesAfterRotate, minimumDelta: 3, timeout: TimeSpan.FromSeconds(15));

                    var screenshotPath = page.CaptureScreenshot("05-orientation-landscape");
                    Assert.NotNull(screenshotPath);
                }
                finally
                {
                    page.TrySetOrientation(ScreenOrientation.Portrait, out _);
                }
            }
            else
            {
                Console.WriteLine($"[iOS Orientation] Device rotation not supported in current session: {failureReason}");
            }
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.iOS, nameof(Test06_OrientationAndViewport_UpdatesDimensionsAndContinuesRendering), ex);
            throw;
        }
    }

    [Fact]
    public void Test07_ApplicationLifecycle_BackgroundAndForegroundRecoversRendering()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosGraphicSurfacePage(driver);

        try
        {
            var beforeText = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
            var before = IosGraphicSurfacePage.ParseDiagnostics(beforeText);
            var framesBefore = long.Parse(before["Frames"], CultureInfo.InvariantCulture);

            page.SendToBackgroundAndResume(TimeSpan.FromSeconds(3));

            Assert.True(page.WaitForSurfaceVisible(TimeSpan.FromSeconds(30)), "SilkGraphicsSurface should be visible after foreground resume.");
            var recoveredText = page.WaitForRenderingProgress(TimeSpan.FromSeconds(30));
            var recovered = IosGraphicSurfacePage.ParseDiagnostics(recoveredText);
            var framesAfterResume = long.Parse(recovered["Frames"], CultureInfo.InvariantCulture);

            var continuedText = page.WaitForFrameAdvance(framesAfterResume, minimumDelta: 5, timeout: TimeSpan.FromSeconds(20));
            var continued = IosGraphicSurfacePage.ParseDiagnostics(continuedText);
            var framesContinued = long.Parse(continued["Frames"], CultureInfo.InvariantCulture);

            Assert.Equal("Rendering", continued.GetValueOrDefault("Phase"));
            Assert.Equal("Ready", continued.GetValueOrDefault("Context"));
            Assert.True(framesContinued > framesBefore, $"Expected frames to continue advancing after foreground resume (before={framesBefore}, after={framesContinued}).");

            var screenshotPath = page.CaptureScreenshot("06-lifecycle-recovery");
            Assert.NotNull(screenshotPath);
            Assert.True(File.Exists(screenshotPath), $"Lifecycle recovery screenshot should exist at '{screenshotPath}'.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.iOS, nameof(Test07_ApplicationLifecycle_BackgroundAndForegroundRecoversRendering), ex);
            throw;
        }
    }
}
