using SilkTriangleSample.UITests.Infrastructure;
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
    public void AppLaunchesAndDisplaysInitialPage()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Mac Catalyst driver was not initialized.");
        var page = new MacCatalystMainPage(driver);

        var headline = page.WaitForHeadline(TimeSpan.FromSeconds(30));
        Assert.Equal("Hello, World!", headline);
        Assert.True(page.IsCounterButtonDisplayed(), "Counter button should be visible on initial page.");
    }

    [Fact]
    public void CounterButtonIsResponsiveAndIncrements()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException("Mac Catalyst driver was not initialized.");
        var page = new MacCatalystMainPage(driver);

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
    public void DocumentsCurrentMacCatalystMainPageBehavior()
    {
        // This test documents that SilkTriangleSample currently loads MainPage on Mac Catalyst.
        // Stage 09 will introduce the native Silk surface for Mac Catalyst.
        var driver = fixture.Driver ?? throw new InvalidOperationException("Mac Catalyst driver was not initialized.");
        var page = new MacCatalystMainPage(driver);

        Assert.True(page.IsBotImageDisplayed(), "dotnet bot image is visible on the current Mac Catalyst template page.");
    }
}

