using SilkTriangleSample.UITests.Android.Pages;
using SilkTriangleSample.UITests.Infrastructure;

namespace SilkTriangleSample.UITests.Android.Tests;

public sealed class AndroidTestFixture : PlatformTestFixture
{
    public AndroidTestFixture() : base(PlatformKind.Android) { }
}

[Trait("Platform", "Android")]
public sealed class AndroidGraphicSurfaceTests : IClassFixture<AndroidTestFixture>
{
    private readonly AndroidTestFixture fixture;

    public AndroidGraphicSurfaceTests(AndroidTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void AppLaunchesAndInitialUiIsVisible()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Android driver was not initialized.");
        var page = new AndroidGraphicSurfacePage(driver);

        Assert.True(page.WaitForSurfaceVisible(TimeSpan.FromSeconds(30)));
        var status = page.GetStatusText();
        Assert.False(string.IsNullOrWhiteSpace(status), "Status label should not be empty upon launch.");
        Assert.False(status.StartsWith("Import failed", StringComparison.OrdinalIgnoreCase), $"Asset import failed: {status}");
    }

    [Fact]
    public void GraphicsSurfaceAndContextInitialize()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Android driver was not initialized.");
        var page = new AndroidGraphicSurfacePage(driver);

        page.WaitForAssetsLoaded(TimeSpan.FromSeconds(30));
        var diagnostics = page.GetDiagnosticsText();
        Assert.Contains("Assets=Loaded", diagnostics);
        Assert.False(diagnostics.Contains("Phase=Failed"), $"Graphics or asset loading failed: {diagnostics}");
    }

    [Fact]
    public void SceneAndAssetsLoadSuccessfully()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Android driver was not initialized.");
        var page = new AndroidGraphicSurfacePage(driver);

        var diagnostics = page.WaitForAssetsLoaded(TimeSpan.FromSeconds(30));
        Assert.Contains("Meshes=", diagnostics);
        Assert.False(diagnostics.Contains("Meshes=0"), "Imported GLB model should contain at least one mesh.");
    }

    [Fact]
    public void RenderingMakesObservableProgress()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Android driver was not initialized.");
        var page = new AndroidGraphicSurfacePage(driver);

        var diagnostics = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        Assert.Contains("Phase=Rendering", diagnostics);
        Assert.Contains("Context=Ready", diagnostics);
        Assert.False(diagnostics.Contains("DrawCalls=0"), "Renderer should perform at least one indexed draw call.");
    }

    [Fact]
    public void CaptureRenderingScreenshot()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Android driver was not initialized.");
        var page = new AndroidGraphicSurfacePage(driver);

        page.WaitForRenderingProgress(TimeSpan.FromSeconds(30));
        var path = page.CaptureScreenshot("Android_SilkSurface_Rendered");
        Assert.NotNull(path);
        Assert.True(File.Exists(path), $"Screenshot should exist at: {path}");
    }
}

