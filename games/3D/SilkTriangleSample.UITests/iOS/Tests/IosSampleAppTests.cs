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
    public void AppLaunchesAndDisplaysInitialPage()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosMainPage(driver);

        var headline = page.WaitForHeadline(TimeSpan.FromSeconds(30));
        Assert.Equal("Hello, World!", headline);
        Assert.True(page.IsCounterButtonDisplayed(), "Counter button should be visible on initial page.");
    }

    [Fact]
    public void CounterButtonIsResponsiveAndIncrements()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosMainPage(driver);

        page.WaitForHeadline(TimeSpan.FromSeconds(30));
        var initialText = page.GetCounterButtonText();
        Assert.Contains("Click me", initialText);

        page.ClickCounterButton();
        var clickedText = page.WaitForText(
            OpenQA.Selenium.Appium.MobileBy.AccessibilityId("CounterBtn"),
            t => t.Contains("Clicked 1 time"),
            "Clicked 1 time",
            TimeSpan.FromSeconds(10));

        Assert.Contains("Clicked 1 time", clickedText);
    }

    [Fact]
    public void DocumentsCurrentIosMainPageBehavior()
    {
        // This test documents the architectural reality of SilkTriangleSample on iOS:
        // App.xaml.cs currently returns new Window(new AppShell()) on iOS, loading MainPage.
        // Stage 08 will integrate GraphicSurfacePage and SilkGraphicsSurface for iOS.
        var driver = fixture.Driver ?? throw new InvalidOperationException("iOS driver was not initialized.");
        var page = new IosMainPage(driver);

        Assert.True(page.IsBotImageDisplayed(), "dotnet bot image is visible on the current iOS template page.");
    }
}

