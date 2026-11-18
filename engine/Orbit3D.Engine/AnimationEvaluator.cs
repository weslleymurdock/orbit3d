using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Specifies the playback state of an <see cref="AnimationEvaluator"/>.
/// </summary>
public enum AnimationPlaybackState
{
    /// <summary>Playback is stopped and reset to the beginning (bind pose / t=0).</summary>
    Stopped,
    /// <summary>Playback is actively advancing.</summary>
    Playing,
    /// <summary>Playback is paused at <see cref="AnimationEvaluator.CurrentTimeInSeconds"/>.</summary>
    Paused,
    /// <summary>Non-looping playback reached the end of the clip.</summary>
    Completed
}

/// <summary>
/// Evaluates skeletal <see cref="AnimationClip3D"/> clips against a <see cref="Skeleton3D"/> without per-frame allocations.
/// </summary>
public sealed class AnimationEvaluator
{
    private readonly Skeleton3D skeleton;
    private readonly IReadOnlyList<AnimationClip3D> clips;
    private readonly Matrix4x4[] localTransforms;
    private readonly Matrix4x4[] globalTransforms;
    private readonly Matrix4x4[] boneMatrices;
    private readonly AnimationChannel3D?[] nodeChannels;
    private int selectedClipIndex = -1;
    private float playbackSpeed = 1.0f;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationEvaluator"/> class.
    /// </summary>
    public AnimationEvaluator(Skeleton3D skeleton, IReadOnlyList<AnimationClip3D> clips)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(clips);

        this.skeleton = skeleton;
        this.clips = clips.ToArray();
        localTransforms = new Matrix4x4[skeleton.Nodes.Count];
        globalTransforms = new Matrix4x4[skeleton.Nodes.Count];
        boneMatrices = new Matrix4x4[skeleton.Bones.Count];
        nodeChannels = new AnimationChannel3D?[skeleton.Nodes.Count];

        if (this.clips.Count > 0)
            SelectClip(0);
        else
            Evaluate();
    }

    /// <summary>Gets the target skeleton.</summary>
    public Skeleton3D Skeleton => skeleton;

    /// <summary>Gets the available animation clips.</summary>
    public IReadOnlyList<AnimationClip3D> Clips => clips;

    /// <summary>Gets the currently selected clip index, or -1 if no clip is selected.</summary>
    public int SelectedClipIndex => selectedClipIndex;

    /// <summary>Gets the currently selected animation clip, if any.</summary>
    public AnimationClip3D? SelectedClip => selectedClipIndex >= 0 && selectedClipIndex < clips.Count
        ? clips[selectedClipIndex]
        : null;

    /// <summary>Gets the current playback state.</summary>
    public AnimationPlaybackState State { get; private set; } = AnimationPlaybackState.Stopped;

    /// <summary>Gets whether playback loops when reaching the end of the clip.</summary>
    public bool IsLooping { get; set; } = true;

    /// <summary>Gets or sets the playback speed multiplier (must be finite and non-negative).</summary>
    public float PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Playback speed must be finite and non-negative.");
            playbackSpeed = value;
        }
    }

    /// <summary>Gets the current animation playback time in seconds.</summary>
    public double CurrentTimeInSeconds { get; private set; }

    /// <summary>Gets the duration of the selected clip in seconds, or 0 if no clip is selected.</summary>
    public double DurationInSeconds => SelectedClip?.DurationInSeconds ?? 0.0;

    /// <summary>Gets the evaluated skinning bone matrices ready for GPU upload.</summary>
    public ReadOnlySpan<Matrix4x4> BoneMatrices => boneMatrices;

    /// <summary>Selects an animation clip by index and resets playback time to 0.</summary>
    public void SelectClip(int index)
    {
        if (index < 0 || index >= clips.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        selectedClipIndex = index;
        var clip = clips[index];
        var nodes = skeleton.Nodes;
        for (var i = 0; i < nodes.Count; i++)
            nodeChannels[i] = clip.FindChannel(nodes[i].Name);

        CurrentTimeInSeconds = 0.0;
        Evaluate();
    }

    /// <summary>Selects an animation clip by name and resets playback time to 0.</summary>
    public bool SelectClip(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        for (var i = 0; i < clips.Count; i++)
        {
            if (string.Equals(clips[i].Name, name, StringComparison.Ordinal))
            {
                SelectClip(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Starts or restarts playback of the selected clip.</summary>
    public void Play()
    {
        if (SelectedClip is null)
            return;

        if (State == AnimationPlaybackState.Completed)
            CurrentTimeInSeconds = 0.0;

        State = AnimationPlaybackState.Playing;
        Evaluate();
    }

    /// <summary>Selects <paramref name="clipName"/> and starts playback.</summary>
    public bool Play(string clipName)
    {
        if (!SelectClip(clipName))
            return false;
        Play();
        return true;
    }

    /// <summary>Pauses playback at the current time.</summary>
    public void Pause()
    {
        if (State == AnimationPlaybackState.Playing)
            State = AnimationPlaybackState.Paused;
    }

    /// <summary>Resumes playback if currently paused.</summary>
    public void Resume()
    {
        if (State == AnimationPlaybackState.Paused && SelectedClip is not null)
            State = AnimationPlaybackState.Playing;
    }

    /// <summary>Stops playback, resets time to 0, and evaluates the pose at t=0.</summary>
    public void Stop()
    {
        State = AnimationPlaybackState.Stopped;
        CurrentTimeInSeconds = 0.0;
        Evaluate();
    }

    /// <summary>Seeks to <paramref name="timeInSeconds"/> (clamped or wrapped when looping) and evaluates the pose.</summary>
    public void Seek(double timeInSeconds)
    {
        if (!double.IsFinite(timeInSeconds))
            throw new ArgumentOutOfRangeException(nameof(timeInSeconds));

        var duration = DurationInSeconds;
        if (duration <= 0.0)
        {
            CurrentTimeInSeconds = 0.0;
        }
        else if (IsLooping)
        {
            var wrapped = timeInSeconds % duration;
            if (wrapped < 0.0)
                wrapped += duration;
            CurrentTimeInSeconds = wrapped;
        }
        else
        {
            CurrentTimeInSeconds = Math.Clamp(timeInSeconds, 0.0, duration);
            if (CurrentTimeInSeconds >= duration && State == AnimationPlaybackState.Playing)
                State = AnimationPlaybackState.Completed;
        }

        Evaluate();
    }

    /// <summary>
    /// Advances animation playback by <paramref name="deltaSeconds"/> and evaluates the bone matrices.
    /// </summary>
    public void Update(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0.0)
            return;

        if (State != AnimationPlaybackState.Playing || SelectedClip is null)
            return;

        var duration = SelectedClip.DurationInSeconds;
        if (duration <= 0.0)
        {
            CurrentTimeInSeconds = 0.0;
            Evaluate();
            return;
        }

        var nextTime = CurrentTimeInSeconds + deltaSeconds * playbackSpeed;
        if (IsLooping)
        {
            CurrentTimeInSeconds = nextTime % duration;
        }
        else if (nextTime >= duration)
        {
            CurrentTimeInSeconds = duration;
            State = AnimationPlaybackState.Completed;
        }
        else
        {
            CurrentTimeInSeconds = nextTime;
        }

        Evaluate();
    }

    /// <summary>
    /// Evaluates the current pose into <see cref="BoneMatrices"/> without advancing time.
    /// </summary>
    public void Evaluate()
    {
        var nodes = skeleton.Nodes;
        var clip = SelectedClip;
        var timeInTicks = clip is not null ? CurrentTimeInSeconds * clip.TicksPerSecond : 0.0;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var channel = clip is not null ? nodeChannels[i] : null;
            if (channel is null)
            {
                localTransforms[i] = node.BindLocalTransform;
            }
            else
            {
                var pos = channel.SamplePosition(timeInTicks, node.BindPosition);
                var rot = channel.SampleRotation(timeInTicks, node.BindRotation);
                var scale = channel.SampleScale(timeInTicks, node.BindScale);
                localTransforms[i] =
                    Matrix4x4.CreateScale(scale) *
                    Matrix4x4.CreateFromQuaternion(rot) *
                    Matrix4x4.CreateTranslation(pos);
            }

            globalTransforms[i] = node.ParentIndex >= 0
                ? localTransforms[i] * globalTransforms[node.ParentIndex]
                : localTransforms[i];
        }

        var bones = skeleton.Bones;
        var globalInverse = skeleton.GlobalInverseTransform;
        for (var i = 0; i < bones.Count; i++)
        {
            var bone = bones[i];
            boneMatrices[i] = bone.InverseBindMatrix * globalTransforms[bone.NodeIndex] * globalInverse;
        }
    }
}
