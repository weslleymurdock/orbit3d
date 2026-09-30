namespace Orbit3D.Engine.Tests;

public class RenderMetricsTests
{
    [Fact]
    public void Metrics_StartAtZero_AndRecordSuccessfulDraws()
    {
        var tracker = new RenderMetricsTracker();

        tracker.BeginFrame();
        Assert.Equal(0, tracker.Current.DrawCalls);
        Assert.Equal(0, tracker.Current.RenderedItems);
        Assert.Equal(0f, tracker.Current.FrameTime);
        Assert.Equal(0f, tracker.Current.FramesPerSecond);

        tracker.RecordDraw(6, 2);
        tracker.RecordDraw(3, 1);
        tracker.CompleteFrame();

        Assert.Equal(2, tracker.Current.DrawCalls);
        Assert.Equal(2, tracker.Current.RenderedItems);
        Assert.Equal(9, tracker.Current.IndexCount);
        Assert.Equal(3, tracker.Current.TriangleCount);
        Assert.True(tracker.Current.FrameTime >= 0f);
        Assert.True(tracker.Current.FramesPerSecond >= 0f);
    }

    [Fact]
    public void Metrics_ResetCorrectly_BetweenFrames()
    {
        var tracker = new RenderMetricsTracker();

        tracker.BeginFrame();
        tracker.RecordDraw(6, 2);
        tracker.CompleteFrame();

        var firstFrame = tracker.Current;
        tracker.BeginFrame();

        Assert.Equal(0, tracker.Current.DrawCalls);
        Assert.Equal(0, tracker.Current.RenderedItems);
        Assert.Equal(0, tracker.Current.IndexCount);
        Assert.Equal(0, tracker.Current.TriangleCount);
        Assert.Equal(0f, tracker.Current.FrameTime);

        tracker.RecordDraw(12, 4);
        tracker.CompleteFrame();

        Assert.Equal(1, tracker.Current.DrawCalls);
        Assert.Equal(1, tracker.Current.RenderedItems);
        Assert.Equal(12, tracker.Current.IndexCount);
        Assert.Equal(4, tracker.Current.TriangleCount);
        Assert.NotEqual(firstFrame.IndexCount, tracker.Current.IndexCount);
    }
}
