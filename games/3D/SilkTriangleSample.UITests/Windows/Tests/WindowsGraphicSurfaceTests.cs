using SilkTriangleSample.UITests.Infrastructure;
using SilkTriangleSample.UITests.Windows.Pages;

namespace SilkTriangleSample.UITests.Windows.Tests;

public sealed class WindowsTestFixture : PlatformTestFixture
{
    public WindowsTestFixture() : base(PlatformKind.Windows) { }
}

[Trait("Platform", "Windows")]
public sealed class WindowsGraphicSurfaceTests : IClassFixture<WindowsTestFixture>
{
    private readonly WindowsTestFixture fixture;

    public WindowsGraphicSurfaceTests(WindowsTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void AppLaunchesAndInitialUiIsVisible()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Windows driver was not initialized.");
        var page = new WindowsGraphicSurfacePage(driver);

        Assert.True(page.WaitForSurfaceVisible(TimeSpan.FromSeconds(30)));
        var status = page.GetStatusText();
        Assert.False(string.IsNullOrWhiteSpace(status), "Status label should not be empty upon launch.");
        Assert.False(status.StartsWith("Import failed", StringComparison.OrdinalIgnoreCase), $"Asset import failed: {status}");
    }

    [Fact]
    public void GraphicsSurfaceAndWglContextInitialize()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Windows driver was not initialized.");
        var page = new WindowsGraphicSurfacePage(driver);

        page.WaitForAssetsLoaded(TimeSpan.FromSeconds(30));
        var diagnostics = page.GetDiagnosticsText();
        Assert.Contains("Assets=Loaded", diagnostics);
        Assert.False(diagnostics.Contains("Phase=Failed"), $"WGL graphics or asset loading failed: {diagnostics}");
    }

    [Fact]
    public void SceneAndAssetsLoadSuccessfully()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Windows driver was not initialized.");
        var page = new WindowsGraphicSurfacePage(driver);

        var diagnostics = page.WaitForAssetsLoaded(TimeSpan.FromSeconds(30));
        Assert.Contains("Meshes=", diagnostics);
        Assert.False(diagnostics.Contains("Meshes=0"), "Imported GLB model should contain at least one mesh.");
    }

    [Fact]
    public void RenderingMakesObservableProgress()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Windows driver was not initialized.");
        var page = new WindowsGraphicSurfacePage(driver);

        var diagnostics = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        Assert.Contains("Phase=Rendering", diagnostics);
        Assert.Contains("Context=Ready", diagnostics);
        Assert.False(diagnostics.Contains("DrawCalls=0"), "Renderer should perform at least one indexed draw call.");
    }

    [Fact]
    public void CaptureRenderingScreenshot()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Windows driver was not initialized.");
        var page = new WindowsGraphicSurfacePage(driver);

        page.WaitForRenderingProgress(TimeSpan.FromSeconds(30));
        var path = page.CaptureScreenshot("Windows_SilkSurface_Rendered");
        Assert.NotNull(path);
        Assert.True(File.Exists(path), $"Screenshot should exist at: {path}");
    }
}

