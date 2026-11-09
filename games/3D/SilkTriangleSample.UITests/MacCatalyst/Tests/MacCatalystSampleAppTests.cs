using System.Globalization;
using SilkTriangleSample.UITests.Infrastructure;
using SilkTriangleSample.UITests.iOS.Pages;
using SilkTriangleSample.UITests.MacCatalyst.Pages;

namespace SilkTriangleSample.UITests.MacCatalyst.Tests;

public sealed class MacCatalystTestFixture : PlatformTestFixture
{
    public MacCatalystTestFixture() : base(PlatformKind.MacCatalyst) { }
}

[Trait("Platform", "MacCatalyst")]
public sealed class MacCatalystSampleAppTests : IClassFixture<MacCatalystTestFixture>
{
    private readonly MacCatalystTestFixture fixture;

    public MacCatalystSampleAppTests(MacCatalystTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Test01_ApplicationLaunch_DisplaysSurfaceAndStatusWithoutErrors()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Mac Catalyst driver was not initialized.");
        var page = new MacCatalystGraphicSurfacePage(driver);

        try
        {
            Assert.True(page.WaitForSurfaceVisible(TimeSpan.FromSeconds(45)), "SilkGraphicsSurface should be visible after launch.");
            var status = page.GetStatusText(TimeSpan.FromSeconds(15));
            Assert.False(string.IsNullOrWhiteSpace(status), "StatusLabel should not be empty upon launch.");
            Assert.False(status.StartsWith("Import failed", StringComparison.OrdinalIgnoreCase), $"Asset import failed: {status}");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.MacCatalyst, nameof(Test01_ApplicationLaunch_DisplaysSurfaceAndStatusWithoutErrors), ex);
            throw;
        }
    }

    [Fact]
    public void Test02_GraphicsInitialization_CreatesNativeSurfaceAndSilkContext()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Mac Catalyst driver was not initialized.");
        var page = new MacCatalystGraphicSurfacePage(driver);

        try
        {
            var diagnosticsText = page.WaitForContextReady(TimeSpan.FromSeconds(45));
            var diagnostics = IosGraphicSurfacePage.ParseDiagnostics(diagnosticsText);

            Assert.Equal("Ready", diagnostics.GetValueOrDefault("Context"));
            Assert.NotEqual("Failed", diagnostics.GetValueOrDefault("Phase"));

            var surfaceRect = page.GetSurfaceRect();
            Assert.True(surfaceRect.Width > 0 && surfaceRect.Height > 0, $"Native surface bounds must be positive, got {surfaceRect.Width}x{surfaceRect.Height}.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.MacCatalyst, nameof(Test02_GraphicsInitialization_CreatesNativeSurfaceAndSilkContext), ex);
            throw;
        }
    }

    [Fact]
    public void Test03_SceneAndAssetLoading_LoadsGlbModelAndSidecarTexture()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Mac Catalyst driver was not initialized.");
        var page = new MacCatalystGraphicSurfacePage(driver);

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
                Assert.False(string.Equals(textureDim, "0x0", StringComparison.Ordinal), $"Expected decoded sidecar texture dimensions, got '{textureDim}'.");
        }
        catch (Exception ex)
        {
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.MacCatalyst, nameof(Test03_SceneAndAssetLoading_LoadsGlbModelAndSidecarTexture), ex);
            throw;
        }
    }

    [Fact]
    public void Test04_RenderingProgress_AdvancesFramesAndReportsMetrics()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Mac Catalyst driver was not initialized.");
        var page = new MacCatalystGraphicSurfacePage(driver);

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
            DiagnosticsCollector.CaptureFailureDiagnostics(driver, PlatformKind.MacCatalyst, nameof(Test04_RenderingProgress_AdvancesFramesAndReportsMetrics), ex);
            throw;
        }
    }
}
