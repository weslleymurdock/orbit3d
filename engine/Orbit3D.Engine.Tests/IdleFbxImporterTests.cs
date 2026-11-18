#if ANDROID || IOS || MACCATALYST || WINDOWS
using System;
using System.IO;
using System.Numerics;
using Xunit;

namespace Orbit3D.Engine.Tests;

public class IdleFbxImporterTests
{
    private static string FindIdleFbxPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "assets", "Idle.fbx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "assets", "Idle.fbx");
    }

    [Fact]
    public void Import_IdleFbx_ImportsSkeletonSkinningAndAnimationClip()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(FindIdleFbxPath());

        Assert.Equal(4, model.Meshes.Count);
        Assert.True(model.HasSkeleton);
        Assert.True(model.HasAnimations);
        Assert.NotNull(model.Skeleton);
        Assert.Equal(88, model.Skeleton!.BoneCount);
        Assert.Equal(240, model.Skeleton.Nodes.Count);

        foreach (var mesh in model.Meshes)
        {
            Assert.True(mesh.IsValid());
            Assert.NotNull(mesh.Skinning);
            Assert.True(mesh.Skinning!.IsValid(model.Skeleton.BoneCount));
            for (var v = 0; v < mesh.Skinning.VertexCount; v++)
            {
                var w = mesh.Skinning.BoneWeights[v];
                var sum = w.X + w.Y + w.Z + w.W;
                Assert.InRange(sum, 0.999f, 1.001f);
            }
        }

        var clip = Assert.Single(model.Animations);
        Assert.Equal("mixamo.com", clip.Name);
        Assert.Equal(240.0, clip.DurationInTicks, 3);
        Assert.Equal(60.0, clip.TicksPerSecond, 3);
        Assert.Equal(4.0, clip.DurationInSeconds, 3);
        Assert.Equal(51, clip.Channels.Count);
    }

    [Fact]
    public void IdleFbx_Evaluator_ProducesChangingBoneMatricesAcrossTime()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(FindIdleFbxPath());

        var evaluator = new AnimationEvaluator(model.Skeleton!, model.Animations);
        evaluator.Play();
        var poseAt0 = evaluator.BoneMatrices.ToArray();

        evaluator.Update(1.0);
        var poseAt1 = evaluator.BoneMatrices.ToArray();

        evaluator.Update(1.0);
        var poseAt2 = evaluator.BoneMatrices.ToArray();

        var maxDelta0To1 = 0f;
        var maxDelta1To2 = 0f;
        for (var i = 0; i < poseAt0.Length; i++)
        {
            Assert.False(float.IsNaN(poseAt1[i].M11));
            Assert.False(float.IsNaN(poseAt2[i].M11));
            maxDelta0To1 = MathF.Max(maxDelta0To1, MathF.Abs(poseAt1[i].M41 - poseAt0[i].M41) + MathF.Abs(poseAt1[i].M12 - poseAt0[i].M12));
            maxDelta1To2 = MathF.Max(maxDelta1To2, MathF.Abs(poseAt2[i].M41 - poseAt1[i].M41) + MathF.Abs(poseAt2[i].M12 - poseAt1[i].M12));
        }

        Assert.True(maxDelta0To1 > 0.01f, $"Expected animated pose change between t=0 and t=1, got {maxDelta0To1}.");
        Assert.True(maxDelta1To2 > 0.01f, $"Expected animated pose change between t=1 and t=2, got {maxDelta1To2}.");
    }
}
#endif
