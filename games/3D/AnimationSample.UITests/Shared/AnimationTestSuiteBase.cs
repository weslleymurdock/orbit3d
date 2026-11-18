using AnimationSample.UITests.Infrastructure;

namespace AnimationSample.UITests.Shared;

/// <summary>
/// Shared end-to-end UI test assertions for AnimationSample across Windows and Android.
/// </summary>
public abstract class AnimationTestSuiteBase
{
    private readonly PlatformTestFixture fixture;

    protected AnimationTestSuiteBase(PlatformTestFixture fixture)
    {
        this.fixture = fixture;
    }

    protected AnimationSurfacePageObject CreatePage()
    {
        var driver = fixture.Driver ?? throw new InvalidOperationException($"{fixture.Platform} driver was not initialized.");
        return new AnimationSurfacePageObject(driver, fixture.Platform);
    }

    [Fact]
    public void AppLaunchesAndInitializesSurfaceAndIdleFbx()
    {
        var page = CreatePage();
        Assert.True(page.WaitForSurfaceVisible(TimeSpan.FromSeconds(30)));

        var status = page.GetStatusText();
        Assert.False(string.IsNullOrWhiteSpace(status));
        Assert.False(status.StartsWith("Import failed", StringComparison.OrdinalIgnoreCase), $"Asset import failed: {status}");

        var diag = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        Assert.Equal("Ready", diag["Context"]);
        Assert.Equal("Loaded", diag["Assets"]);
        Assert.Equal("Ready", diag["Skeleton"]);
        Assert.Equal(88, AnimationSurfacePageObject.ParseInt(diag, "Bones"));
        Assert.Equal(240, AnimationSurfacePageObject.ParseInt(diag, "Nodes"));
        Assert.Equal(1, AnimationSurfacePageObject.ParseInt(diag, "Clips"));
        Assert.Equal("mixamo.com", diag["SelectedClip"]);
        Assert.True(AnimationSurfacePageObject.ParseInt(diag, "DrawCalls") >= 4);
        Assert.True(AnimationSurfacePageObject.ParseInt(diag, "Triangles") >= 8222);
    }

    [Fact]
    public void AnimationTimeAdvancesAndBonePosesChangeDuringPlayback()
    {
        var page = CreatePage();
        var d1 = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        Assert.Equal("Playing", d1["State"]);
        var t1 = AnimationSurfacePageObject.ParseDouble(d1, "Time");
        var pose1 = d1["PoseHash"];

        var d2 = page.WaitForDiagnostics(
            d => AnimationSurfacePageObject.ParseDouble(d, "Time") != t1 && d.TryGetValue("PoseHash", out var p) && p != pose1,
            "Animation time and PoseHash advance during playback",
            TimeSpan.FromSeconds(15));

        Assert.NotEqual(pose1, d2["PoseHash"]);
    }

    [Fact]
    public void PlaybackControls_PauseResumeStopLoopAndSpeedWork()
    {
        var page = CreatePage();
        page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));

        // Pause
        page.ClickPause();
        var paused = page.WaitForDiagnostics(d => d.TryGetValue("State", out var s) && s == "Paused", "State=Paused", TimeSpan.FromSeconds(10));
        var pausedTime = AnimationSurfacePageObject.ParseDouble(paused, "Time");
        var pausedPose = paused["PoseHash"];
        // Verify time stays frozen while paused
        var pausedAgain = page.GetDiagnostics();
        Assert.Equal(pausedTime, AnimationSurfacePageObject.ParseDouble(pausedAgain, "Time"), 3);
        Assert.Equal(pausedPose, pausedAgain["PoseHash"]);

        // Resume
        page.ClickResume();
        var resumed = page.WaitForDiagnostics(
            d => d.TryGetValue("State", out var s) && s == "Playing" && AnimationSurfacePageObject.ParseDouble(d, "Time") != pausedTime,
            "State=Playing with advancing time",
            TimeSpan.FromSeconds(10));
        Assert.Equal("Playing", resumed["State"]);

        // Speed toggle (1.0 -> 2.0)
        page.ClickSpeed();
        var spedUp = page.WaitForDiagnostics(d => d.TryGetValue("Speed", out var sp) && sp == "2.0", "Speed=2.0", TimeSpan.FromSeconds(10));
        Assert.Equal("2.0", spedUp["Speed"]);

        // Loop toggle (True -> False -> True)
        page.ClickLoop();
        page.WaitForDiagnostics(d => d.TryGetValue("Loop", out var lp) && lp == "False", "Loop=False", TimeSpan.FromSeconds(10));
        page.ClickLoop();
        page.WaitForDiagnostics(d => d.TryGetValue("Loop", out var lp) && lp == "True", "Loop=True", TimeSpan.FromSeconds(10));

        // Stop then Play
        page.ClickStop();
        var stopped = page.WaitForDiagnostics(d => d.TryGetValue("State", out var s) && s == "Stopped", "State=Stopped", TimeSpan.FromSeconds(10));
        Assert.Equal(0.0, AnimationSurfacePageObject.ParseDouble(stopped, "Time"), 3);
        page.ClickPlay();
        page.WaitForDiagnostics(d => d.TryGetValue("State", out var s) && s == "Playing", "State=Playing after Play", TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ContextRecreationPreservesAnimationAndResumesRendering()
    {
        var page = CreatePage();
        var before = page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        var creationsBefore = AnimationSurfacePageObject.ParseInt(before, "CtxCreations");

        page.ClickRecreateContext();

        var after = page.WaitForDiagnostics(
            d => AnimationSurfacePageObject.ParseInt(d, "CtxCreations") > creationsBefore
                 && d.TryGetValue("Phase", out var p) && p == "Rendering"
                 && AnimationSurfacePageObject.ParseInt(d, "DrawCalls") >= 4,
            "Graphics context recreated and rendering resumed",
            TimeSpan.FromSeconds(20));

        Assert.True(AnimationSurfacePageObject.ParseInt(after, "CtxLosses") >= 1);
        Assert.Equal(88, AnimationSurfacePageObject.ParseInt(after, "Bones"));
        Assert.Equal("Playing", after["State"]);
    }

    [Fact]
    public void CaptureRenderedAnimationScreenshot()
    {
        var page = CreatePage();
        page.WaitForRenderingProgress(TimeSpan.FromSeconds(45));
        var path = page.CaptureScreenshot($"{fixture.Platform}_AnimationSample_Rendered");
        Assert.NotNull(path);
        Assert.True(File.Exists(path), $"Screenshot should exist at: {path}");
    }
}
