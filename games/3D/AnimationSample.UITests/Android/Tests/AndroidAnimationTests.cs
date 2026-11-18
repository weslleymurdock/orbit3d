using AnimationSample.UITests.Infrastructure;
using AnimationSample.UITests.Shared;

namespace AnimationSample.UITests.Android.Tests;

public sealed class AndroidAnimationTestFixture : PlatformTestFixture
{
    public AndroidAnimationTestFixture() : base(PlatformKind.Android) { }
}

[Trait("Platform", "Android")]
public sealed class AndroidAnimationTests : AnimationTestSuiteBase, IClassFixture<AndroidAnimationTestFixture>
{
    public AndroidAnimationTests(AndroidAnimationTestFixture fixture) : base(fixture) { }
}
