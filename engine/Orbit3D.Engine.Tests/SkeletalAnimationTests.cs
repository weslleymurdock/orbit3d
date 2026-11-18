using System.Numerics;
using Orbit3D.Engine.Graphics;
using Xunit;

namespace Orbit3D.Engine.Tests;

public class SkeletalAnimationTests
{
    private static Skeleton3D CreateTwoBoneSkeleton(out Matrix4x4 rootBindGlobal, out Matrix4x4 childBindGlobal)
    {
        var rootLocal = Matrix4x4.CreateTranslation(0f, 1f, 0f);
        var childLocal = Matrix4x4.CreateTranslation(0f, 2f, 0f);
        rootBindGlobal = rootLocal;
        childBindGlobal = childLocal * rootLocal;

        Matrix4x4.Invert(rootBindGlobal, out var rootInvBind);
        Matrix4x4.Invert(childBindGlobal, out var childInvBind);

        var nodes = new[]
        {
            new SkeletonNode(0, "Root", -1, rootLocal, new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One),
            new SkeletonNode(1, "Child", 0, childLocal, new Vector3(0f, 2f, 0f), Quaternion.Identity, Vector3.One)
        };

        var bones = new[]
        {
            new Bone3D(0, "Root", 0, -1, rootInvBind, rootLocal),
            new Bone3D(1, "Child", 1, 0, childInvBind, childLocal)
        };

        return new Skeleton3D("TestSkeleton", nodes, bones, Matrix4x4.Identity);
    }

    [Fact]
    public void Skeleton_EvaluateBindPose_ProducesIdentityBoneMatrices()
    {
        var skeleton = CreateTwoBoneSkeleton(out _, out _);
        var matrices = new Matrix4x4[skeleton.BoneCount];
        skeleton.EvaluateBindPose(matrices);

        Assert.Equal(2, skeleton.BoneCount);
        Assert.Equal(-1, skeleton.Bones[0].ParentBoneIndex);
        Assert.Equal(0, skeleton.Bones[1].ParentBoneIndex);
        AssertMatrixNear(Matrix4x4.Identity, matrices[0]);
        AssertMatrixNear(Matrix4x4.Identity, matrices[1]);
    }

    [Fact]
    public void SkinningData_NormalizesWeights_KeepsTop4_AndRejectsInvalidIndices()
    {
        var skin = new SkinningData(2);
        // Vertex 0: 5 influences -> keeps top 4 (weights 0.5, 0.3, 0.2, 0.15; drops 0.05) and normalizes sum to 1.0
        skin.AddInfluence(0, 0, 0.50f);
        skin.AddInfluence(0, 1, 0.05f);
        skin.AddInfluence(0, 2, 0.30f);
        skin.AddInfluence(0, 3, 0.20f);
        skin.AddInfluence(0, 4, 0.15f);

        // Vertex 1: invalid bone index 99 should be zeroed out, leaving bone 1 with normalized weight 1.0
        skin.AddInfluence(1, 99, 0.7f);
        skin.AddInfluence(1, 1, 0.3f);

        skin.ValidateAndNormalize(boneCount: 5);

        Assert.True(skin.IsValid(5));
        var w0 = skin.BoneWeights[0];
        Assert.Equal(1.0f, w0.X + w0.Y + w0.Z + w0.W, 4);
        Assert.DoesNotContain(1, skin.BoneIndices.AsSpan(0, 4).ToArray());

        var w1 = skin.BoneWeights[1];
        Assert.Equal(1.0f, w1.X + w1.Y + w1.Z + w1.W, 4);
        Assert.Equal(1, skin.InfluenceCounts[1]);
    }

    [Fact]
    public void AnimationChannel_InterpolatesVectorLerpAndQuaternionSlerp_WithBoundaryClamping()
    {
        var q0 = Quaternion.Identity;
        var q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        var channel = new AnimationChannel3D(
            "Bone",
            [new VectorKeyframe(10.0, Vector3.Zero), new VectorKeyframe(20.0, new Vector3(10f, 0f, 0f))],
            [new QuaternionKeyframe(10.0, q0), new QuaternionKeyframe(20.0, q1)],
            [new VectorKeyframe(10.0, Vector3.One), new VectorKeyframe(20.0, new Vector3(3f, 3f, 3f))]);

        // Clamping before first key and after last key
        Assert.Equal(Vector3.Zero, channel.SamplePosition(0.0, Vector3.One));
        Assert.Equal(new Vector3(10f, 0f, 0f), channel.SamplePosition(30.0, Vector3.Zero));

        // Midpoint interpolation
        var midPos = channel.SamplePosition(15.0, Vector3.Zero);
        Assert.Equal(5f, midPos.X, 4);

        var midScale = channel.SampleScale(15.0, Vector3.One);
        Assert.Equal(2f, midScale.X, 4);

        var midRot = channel.SampleRotation(15.0, Quaternion.Identity);
        var expectedRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.25f);
        Assert.True(MathF.Abs(Quaternion.Dot(midRot, expectedRot)) > 0.999f);
    }

    [Fact]
    public void AnimationEvaluator_PlaybackControls_Looping_Speed_Seek_AndCompletion()
    {
        var skeleton = CreateTwoBoneSkeleton(out _, out _);
        var clipA = new AnimationClip3D(
            "ClipA",
            durationInTicks: 60.0,
            ticksPerSecond: 30.0, // 2 seconds
            [
                new AnimationChannel3D(
                    "Root",
                    [new VectorKeyframe(0.0, new Vector3(0f, 1f, 0f)), new VectorKeyframe(60.0, new Vector3(4f, 1f, 0f))],
                    [],
                    [])
            ]);
        var clipB = new AnimationClip3D(
            "ClipB",
            durationInTicks: 30.0,
            ticksPerSecond: 30.0, // 1 second
            []);

        var evaluator = new AnimationEvaluator(skeleton, [clipA, clipB]);
        Assert.Equal(AnimationPlaybackState.Stopped, evaluator.State);
        Assert.Equal(2.0, evaluator.DurationInSeconds, 5);

        // Play & advance
        evaluator.Play();
        Assert.Equal(AnimationPlaybackState.Playing, evaluator.State);
        evaluator.Update(0.5);
        Assert.Equal(0.5, evaluator.CurrentTimeInSeconds, 5);

        // Child bone has no channel -> preserves bind pose relative to moved Root (+1m in X at t=0.5s of 2s)
        var rootDelta = evaluator.BoneMatrices[0].M41;
        var childDelta = evaluator.BoneMatrices[1].M41;
        Assert.Equal(1.0f, rootDelta, 4);
        Assert.Equal(1.0f, childDelta, 4);

        // Pause stops progression
        evaluator.Pause();
        Assert.Equal(AnimationPlaybackState.Paused, evaluator.State);
        evaluator.Update(0.5);
        Assert.Equal(0.5, evaluator.CurrentTimeInSeconds, 5);

        // Resume + 2x speed + looping wrap
        evaluator.Resume();
        evaluator.PlaybackSpeed = 2.0f;
        evaluator.IsLooping = true;
        evaluator.Update(1.0); // 0.5 + 2.0 = 2.5 -> wraps to 0.5
        Assert.Equal(0.5, evaluator.CurrentTimeInSeconds, 5);

        // Non-looping completion
        evaluator.IsLooping = false;
        evaluator.Update(2.0);
        Assert.Equal(2.0, evaluator.CurrentTimeInSeconds, 5);
        Assert.Equal(AnimationPlaybackState.Completed, evaluator.State);

        // Seek & Stop
        evaluator.Seek(1.0);
        Assert.Equal(1.0, evaluator.CurrentTimeInSeconds, 5);
        evaluator.Stop();
        Assert.Equal(AnimationPlaybackState.Stopped, evaluator.State);
        Assert.Equal(0.0, evaluator.CurrentTimeInSeconds, 5);
        AssertMatrixNear(Matrix4x4.Identity, evaluator.BoneMatrices[0]);

        // Clip selection
        Assert.True(evaluator.SelectClip("ClipB"));
        Assert.Equal(1, evaluator.SelectedClipIndex);
        Assert.False(evaluator.SelectClip("MissingClip"));
    }

    [Fact]
    public void AnimationEvaluator_And_ModelCache_SurviveGpuResourceCacheInvalidation()
    {
        var skeleton = CreateTwoBoneSkeleton(out _, out _);
        var clip = new AnimationClip3D(
            "Idle",
            durationInTicks: 30.0,
            ticksPerSecond: 30.0,
            [
                new AnimationChannel3D(
                    "Root",
                    [new VectorKeyframe(0.0, new Vector3(0f, 1f, 0f)), new VectorKeyframe(30.0, new Vector3(2f, 1f, 0f))],
                    [],
                    [])
            ]);

        var model = new Model3D { Skeleton = skeleton };
        model.Animations.Add(clip);

        var assetCache = new ModelAssetCache();
        var cachedModel = assetCache.GetOrAdd("idle.fbx", () => model);
        var evaluator = new AnimationEvaluator(cachedModel.Skeleton!, cachedModel.Animations);
        evaluator.Play();
        evaluator.Update(0.5);

        var gpuCache = new GpuResourceCache();
        gpuCache.Invalidate();

        // CPU model, skeleton, animation clips, and evaluator state remain intact across GPU context loss.
        Assert.True(assetCache.TryGet("idle.fbx", null, out var afterLoss));
        Assert.Same(cachedModel, afterLoss);
        Assert.Equal(AnimationPlaybackState.Playing, evaluator.State);
        Assert.Equal(0.5, evaluator.CurrentTimeInSeconds, 5);
        Assert.Equal(1.0f, evaluator.BoneMatrices[0].M41, 3);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual, float tolerance = 1e-4f)
    {
        Assert.True(MathF.Abs(expected.M11 - actual.M11) < tolerance);
        Assert.True(MathF.Abs(expected.M22 - actual.M22) < tolerance);
        Assert.True(MathF.Abs(expected.M33 - actual.M33) < tolerance);
        Assert.True(MathF.Abs(expected.M41 - actual.M41) < tolerance);
        Assert.True(MathF.Abs(expected.M42 - actual.M42) < tolerance);
        Assert.True(MathF.Abs(expected.M43 - actual.M43) < tolerance);
    }
}
