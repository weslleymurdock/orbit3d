using AnimationSample.UITests.Infrastructure;
using AnimationSample.UITests.Shared;

namespace AnimationSample.UITests.MacCatalyst.Tests;

public sealed class MacCatalystAnimationTestFixture : PlatformTestFixture
{
    public MacCatalystAnimationTestFixture() : base(PlatformKind.MacCatalyst) { }
}

[Trait("Platform", "MacCatalyst")]
public sealed class MacCatalystAnimationTests : AnimationTestSuiteBase, IClassFixture<MacCatalystAnimationTestFixture>
{
    private readonly MacCatalystAnimationTestFixture fixture;

    public MacCatalystAnimationTests(MacCatalystAnimationTestFixture fixture) : base(fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void ConsecutiveContextRecreationsPreserveAnimationAndResumeRendering()
    {
        var page = CreatePage();
        var initial = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        var creations = AnimationSurfacePageObject.ParseInt(initial, "CtxCreations");
        var losses = AnimationSurfacePageObject.ParseInt(initial, "CtxLosses");

        for (var cycle = 1; cycle <= 2; cycle++)
        {
            page.ClickRecreateContext();
            var expectedCreations = creations + cycle;
            var expectedLosses = losses + cycle;

            var after = page.WaitForDiagnostics(
                d => AnimationSurfacePageObject.ParseInt(d, "CtxCreations") >= expectedCreations
                     && AnimationSurfacePageObject.ParseInt(d, "CtxLosses") >= expectedLosses
                     && d.TryGetValue("Phase", out var p) && p == "Rendering"
                     && AnimationSurfacePageObject.ParseInt(d, "DrawCalls") >= 4,
                $"Context recreation cycle {cycle} completes and resumes rendering",
                TimeSpan.FromSeconds(25));

            Assert.Equal(88, AnimationSurfacePageObject.ParseInt(after, "Bones"));
            Assert.Equal(240, AnimationSurfacePageObject.ParseInt(after, "Nodes"));
            Assert.Equal("Playing", after["State"]);
        }
    }

    [Fact]
    public void WindowResizeUpdatesViewportAndContinuesRendering()
    {
        var page = CreatePage();
        var before = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        var viewportBefore = before["Viewport"];

        var driver = fixture.Driver!;
        var window = driver.FindElement(OpenQA.Selenium.By.XPath("//XCUIElementTypeWindow[1]"));
        var rect = window.Rect;
        var cornerX = rect.X + rect.Width - 2;
        var cornerY = rect.Y + rect.Height - 2;

        new OpenQA.Selenium.Interactions.Actions(driver)
            .MoveToLocation(cornerX, cornerY)
            .ClickAndHold()
            .MoveByOffset(-200, -120)
            .Release()
            .Perform();

        var resized = page.WaitForDiagnostics(
            d => d.TryGetValue("Phase", out var p) && p == "Rendering"
                 && d.TryGetValue("Viewport", out var vp) && vp != viewportBefore
                 && AnimationSurfacePageObject.ParseInt(d, "DrawCalls") >= 4,
            $"Viewport changes from {viewportBefore} after window resize",
            TimeSpan.FromSeconds(15));

        Assert.NotEqual(viewportBefore, resized["Viewport"]);
        Assert.Equal("Playing", resized["State"]);
    }
}
