using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Represents a 3D vector keyframe (translation or scale) at a given tick timestamp.
/// </summary>
public readonly record struct VectorKeyframe(double TimeInTicks, Vector3 Value);

/// <summary>
/// Represents a quaternion rotation keyframe at a given tick timestamp.
/// </summary>
public readonly record struct QuaternionKeyframe(double TimeInTicks, Quaternion Value);

/// <summary>
/// Represents animation keyframes targeting a single node in a <see cref="Skeleton3D"/>.
/// </summary>
public sealed class AnimationChannel3D
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationChannel3D"/> class.
    /// </summary>
    public AnimationChannel3D(
        string nodeName,
        IReadOnlyList<VectorKeyframe> positionKeys,
        IReadOnlyList<QuaternionKeyframe> rotationKeys,
        IReadOnlyList<VectorKeyframe> scaleKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeName);
        ArgumentNullException.ThrowIfNull(positionKeys);
        ArgumentNullException.ThrowIfNull(rotationKeys);
        ArgumentNullException.ThrowIfNull(scaleKeys);

        NodeName = nodeName;
        PositionKeys = positionKeys.ToArray();
        RotationKeys = rotationKeys.ToArray();
        ScaleKeys = scaleKeys.ToArray();
    }

    /// <summary>Gets the target skeleton node name.</summary>
    public string NodeName { get; }

    /// <summary>Gets the ordered position keyframes.</summary>
    public IReadOnlyList<VectorKeyframe> PositionKeys { get; }

    /// <summary>Gets the ordered rotation keyframes.</summary>
    public IReadOnlyList<QuaternionKeyframe> RotationKeys { get; }

    /// <summary>Gets the ordered scale keyframes.</summary>
    public IReadOnlyList<VectorKeyframe> ScaleKeys { get; }

    /// <summary>Samples the interpolated position at <paramref name="timeInTicks"/>, or returns <paramref name="fallback"/> if empty.</summary>
    public Vector3 SamplePosition(double timeInTicks, Vector3 fallback) =>
        SampleVector(PositionKeys, timeInTicks, fallback);

    /// <summary>Samples the interpolated scale at <paramref name="timeInTicks"/>, or returns <paramref name="fallback"/> if empty.</summary>
    public Vector3 SampleScale(double timeInTicks, Vector3 fallback) =>
        SampleVector(ScaleKeys, timeInTicks, fallback);

    /// <summary>Samples the spherically interpolated rotation at <paramref name="timeInTicks"/>, or returns <paramref name="fallback"/> if empty.</summary>
    public Quaternion SampleRotation(double timeInTicks, Quaternion fallback)
    {
        var count = RotationKeys.Count;
        if (count == 0)
            return fallback;
        if (count == 1 || timeInTicks <= RotationKeys[0].TimeInTicks)
            return Quaternion.Normalize(RotationKeys[0].Value);
        if (timeInTicks >= RotationKeys[count - 1].TimeInTicks)
            return Quaternion.Normalize(RotationKeys[count - 1].Value);

        var index = FindKeyframeIndex(RotationKeys, timeInTicks);
        var k0 = RotationKeys[index];
        var k1 = RotationKeys[index + 1];
        var span = k1.TimeInTicks - k0.TimeInTicks;
        if (span <= 1e-9)
            return Quaternion.Normalize(k0.Value);

        var factor = (float)Math.Clamp((timeInTicks - k0.TimeInTicks) / span, 0.0, 1.0);
        var q0 = Quaternion.Normalize(k0.Value);
        var q1 = Quaternion.Normalize(k1.Value);
        return Quaternion.Normalize(Quaternion.Slerp(q0, q1, factor));
    }

    private static Vector3 SampleVector(IReadOnlyList<VectorKeyframe> keys, double timeInTicks, Vector3 fallback)
    {
        var count = keys.Count;
        if (count == 0)
            return fallback;
        if (count == 1 || timeInTicks <= keys[0].TimeInTicks)
            return keys[0].Value;
        if (timeInTicks >= keys[count - 1].TimeInTicks)
            return keys[count - 1].Value;

        var index = FindKeyframeIndex(keys, timeInTicks);
        var k0 = keys[index];
        var k1 = keys[index + 1];
        var span = k1.TimeInTicks - k0.TimeInTicks;
        if (span <= 1e-9)
            return k0.Value;

        var factor = (float)Math.Clamp((timeInTicks - k0.TimeInTicks) / span, 0.0, 1.0);
        return Vector3.Lerp(k0.Value, k1.Value, factor);
    }

    private static int FindKeyframeIndex(IReadOnlyList<VectorKeyframe> keys, double timeInTicks)
    {
        var low = 0;
        var high = keys.Count - 2;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            if (timeInTicks < keys[mid].TimeInTicks)
                high = mid - 1;
            else if (timeInTicks >= keys[mid + 1].TimeInTicks)
                low = mid + 1;
            else
                return mid;
        }
        return Math.Clamp(low, 0, keys.Count - 2);
    }

    private static int FindKeyframeIndex(IReadOnlyList<QuaternionKeyframe> keys, double timeInTicks)
    {
        var low = 0;
        var high = keys.Count - 2;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            if (timeInTicks < keys[mid].TimeInTicks)
                high = mid - 1;
            else if (timeInTicks >= keys[mid + 1].TimeInTicks)
                low = mid + 1;
            else
                return mid;
        }
        return Math.Clamp(low, 0, keys.Count - 2);
    }
}

/// <summary>
/// Represents a backend-neutral skeletal animation clip.
/// </summary>
public sealed class AnimationClip3D
{
    /// <summary>Default ticks per second used when an imported clip omits or zeroes its tick rate.</summary>
    public const double DefaultTicksPerSecond = 25.0;

    private readonly Dictionary<string, AnimationChannel3D> channelLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationClip3D"/> class.
    /// </summary>
    public AnimationClip3D(
        string name,
        double durationInTicks,
        double ticksPerSecond,
        IReadOnlyList<AnimationChannel3D> channels)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(channels);
        if (!double.IsFinite(durationInTicks) || durationInTicks < 0.0)
            throw new ArgumentOutOfRangeException(nameof(durationInTicks));

        Name = name;
        DurationInTicks = durationInTicks;
        TicksPerSecond = double.IsFinite(ticksPerSecond) && ticksPerSecond > 0.0
            ? ticksPerSecond
            : DefaultTicksPerSecond;
        Channels = channels.ToArray();

        channelLookup = new Dictionary<string, AnimationChannel3D>(Channels.Count, StringComparer.Ordinal);
        foreach (var channel in Channels)
            channelLookup.TryAdd(channel.NodeName, channel);
    }

    /// <summary>Gets the animation clip name.</summary>
    public string Name { get; }

    /// <summary>Gets the clip duration in ticks.</summary>
    public double DurationInTicks { get; }

    /// <summary>Gets the playback tick rate in ticks per second.</summary>
    public double TicksPerSecond { get; }

    /// <summary>Gets the clip duration in seconds.</summary>
    public double DurationInSeconds => DurationInTicks / TicksPerSecond;

    /// <summary>Gets all node animation channels.</summary>
    public IReadOnlyList<AnimationChannel3D> Channels { get; }

    /// <summary>Finds the animation channel for <paramref name="nodeName"/>, or null if none exists.</summary>
    public AnimationChannel3D? FindChannel(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName))
            return null;
        return channelLookup.TryGetValue(nodeName, out var channel) ? channel : null;
    }
}
