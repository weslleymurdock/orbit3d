using AnimationSample.UITests.Infrastructure;
using AnimationSample.UITests.Shared;

namespace AnimationSample.UITests.Windows.Tests;

public sealed class WindowsAnimationTestFixture : PlatformTestFixture
{
    public WindowsAnimationTestFixture() : base(PlatformKind.Windows) { }
}

[Trait("Platform", "Windows")]
public sealed class WindowsAnimationTests : AnimationTestSuiteBase, IClassFixture<WindowsAnimationTestFixture>
{
    public WindowsAnimationTests(WindowsAnimationTestFixture fixture) : base(fixture) { }
}
