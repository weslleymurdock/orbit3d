using System.Diagnostics;

namespace Orbit3D.Engine;

/// <summary>
/// Exposes the runtime frame metrics collected by the 3D renderer.
/// </summary>
public readonly record struct RenderMetrics
{
    /// <summary>
    /// Gets the elapsed frame time in milliseconds.
    /// </summary>
    public float FrameTime { get; init; }

    /// <summary>
    /// Gets the calculated frames-per-second value for the most recently completed frame.
    /// </summary>
    public float FramesPerSecond { get; init; }

    /// <summary>
    /// Gets the number of actual draw calls executed during the current frame.
    /// </summary>
    public int DrawCalls { get; init; }

    /// <summary>
    /// Gets the number of rendered items that were successfully submitted to the GPU this frame.
    /// </summary>
    public int RenderedItems { get; init; }

    /// <summary>
    /// Gets the number of triangles emitted during the current frame.
    /// </summary>
    public int TriangleCount { get; init; }

    /// <summary>
    /// Gets the number of indices processed during the current frame.
    /// </summary>
    public int IndexCount { get; init; }
}

/// <summary>
/// Tracks per-frame rendering statistics without creating per-frame allocations.
/// </summary>
public sealed class RenderMetricsTracker
{
    private long frameStartTimestamp;

    /// <summary>
    /// Gets the current metrics snapshot.
    /// </summary>
    public RenderMetrics Current { get; private set; }

    /// <summary>
    /// Resets the frame counters for the next frame.
    /// </summary>
    public void BeginFrame()
    {
        frameStartTimestamp = Stopwatch.GetTimestamp();
        Current = default;
    }

    /// <summary>
    /// Records a successfully submitted draw call.
    /// </summary>
    public void RecordDraw(int indexCount = 0, int triangleCount = 0)
    {
        Current = Current with
        {
            DrawCalls = Current.DrawCalls + 1,
            RenderedItems = Current.RenderedItems + 1,
            IndexCount = Current.IndexCount + indexCount,
            TriangleCount = Current.TriangleCount + triangleCount
        };
    }

    /// <summary>
    /// Finalizes the frame timing and FPS values.
    /// </summary>
    public void CompleteFrame()
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - frameStartTimestamp;
        var elapsedMs = elapsedTicks * 1000d / Stopwatch.Frequency;
        var frameTime = (float)elapsedMs;
        var fps = elapsedMs > 0 ? (float)(1000d / elapsedMs) : 0f;

        Current = Current with
        {
            FrameTime = frameTime,
            FramesPerSecond = fps
        };
    }
}
