using System.Numerics;
using Orbit3D.Engine;

namespace Orbit3D.Engine.Tests;

public class Runtime3DSceneTests
{
    [Fact]
    public void AssetCache_ReusesSameModelForSameIdentity()
    {
        var cache = new ModelAssetCache();
        var first = new Model3D();
        var calls = 0;

        var result1 = cache.GetOrAdd("/models/test.obj", () =>
        {
            calls++;
            return first;
        }, new AssetImportSettings { GenerateNormals = true });

        var result2 = cache.GetOrAdd("/models/test.obj", () => new Model3D(), new AssetImportSettings { GenerateNormals = true });

        Assert.Same(first, result1);
        Assert.Same(first, result2);
        Assert.Equal(1, calls);
        Assert.True(cache.TryGet("/models/test.obj", new AssetImportSettings { GenerateNormals = true }, out var cached));
        Assert.Same(first, cached);
    }

    [Fact]
    public void AssetCache_DoesNotCollideAcrossDifferentImportSettings()
    {
        var cache = new ModelAssetCache();
        var defaultModel = new Model3D();
        var altModel = new Model3D();

        var defaultResult = cache.GetOrAdd("/models/test.obj", () => defaultModel, new AssetImportSettings { GenerateNormals = true });
        var altResult = cache.GetOrAdd("/models/test.obj", () => altModel, new AssetImportSettings { GenerateNormals = false });

        Assert.Same(defaultModel, defaultResult);
        Assert.Same(altModel, altResult);
        Assert.NotSame(defaultResult, altResult);
    }

    [Fact]
    public async Task AssetCache_ConcurrentRequestsShareSingleImport()
    {
        var cache = new ModelAssetCache();
        var importCount = 0;

        var tasks = Enumerable.Range(0, 12)
            .Select(async _ => await cache.GetOrAddAsync("/models/shared.obj", async () =>
            {
                Interlocked.Increment(ref importCount);
                await Task.Delay(25);
                return new Model3D();
            }, new AssetImportSettings { GenerateNormals = true })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, importCount);
        Assert.All(results, model => Assert.NotNull(model));
        Assert.All(results, model => Assert.Same(results[0], model));
    }

    [Fact]
    public void AssetCache_ClearRemovesEntries()
    {
        var cache = new ModelAssetCache();
        var model = cache.GetOrAdd("/models/test.obj", () => new Model3D(), new AssetImportSettings { GenerateNormals = true });

        Assert.True(cache.TryGet("/models/test.obj", new AssetImportSettings { GenerateNormals = true }, out var cached));
        Assert.Same(model, cached);

        cache.Clear();

        Assert.False(cache.TryGet("/models/test.obj", new AssetImportSettings { GenerateNormals = true }, out _));
    }

    [Fact]
    public void Scene_BuildRenderQueue_TraversesHierarchyDeterministically()
    {
        var scene = new Scene3D();
        var rootChild = new Node3D { Name = "ChildA" };
        var childModel = new Model3D();
        childModel.Meshes.Add(new Mesh3D
        {
            Name = "Quad",
            Positions = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)
            },
            Indices32 = new[] { 0u, 1u, 2u },
            IndexFormat = IndexFormat.UInt32,
            Topology = PrimitiveTopology.TriangleList,
            Normals = new[]
            {
                Vector3.UnitZ,
                Vector3.UnitZ,
                Vector3.UnitZ
            }
        });
        childModel.Materials.Add(new Material3D { Name = "Primary" });
        childModel.Meshes[0].MaterialIndex = 0;
        rootChild.Model = childModel;
        rootChild.Transform.Position = new Vector3(2f, 0f, 0f);
        rootChild.SetParent(scene.RootNode);

        var queue = scene.BuildRenderQueue();

        Assert.Single(queue);
        Assert.Same(rootChild, queue[0].Node);
        Assert.Same(childModel.Meshes[0], queue[0].Mesh);
        Assert.Equal(rootChild.WorldMatrix, queue[0].WorldMatrix);
    }
}
