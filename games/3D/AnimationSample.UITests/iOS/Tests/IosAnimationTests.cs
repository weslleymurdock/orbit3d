using AnimationSample.UITests.Infrastructure;
using AnimationSample.UITests.Shared;

namespace AnimationSample.UITests.iOS.Tests;

public sealed class IosAnimationTestFixture : PlatformTestFixture
{
    public IosAnimationTestFixture() : base(PlatformKind.iOS) { }
}

[Trait("Platform", "iOS")]
public sealed class IosAnimationTests : AnimationTestSuiteBase, IClassFixture<IosAnimationTestFixture>
{
    public IosAnimationTests(IosAnimationTestFixture fixture) : base(fixture) { }

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
}
