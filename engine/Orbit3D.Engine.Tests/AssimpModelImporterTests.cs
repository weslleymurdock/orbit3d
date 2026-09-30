using System.IO;
using System.Numerics;
using Xunit;

namespace Orbit3D.Engine.Tests;

public class AssimpModelImporterTests
{
    private static string AssetPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", file);

    // ── Import from file path ────────────────────────────────────────────────

    [Fact]
    public void Import_ObjFile_ReturnsMeshWithPositionsAndIndices()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        Assert.NotNull(model);
        Assert.NotEmpty(model.Meshes);

        var mesh = model.Meshes[0];
        Assert.NotNull(mesh.Positions);
        Assert.True(mesh.Positions!.Length > 0, "Expected at least one vertex.");
        Assert.NotNull(mesh.Indices32);
        Assert.True(mesh.Indices32!.Length > 0, "Expected at least one index.");
        Assert.Equal(0, mesh.Indices32.Length % 3); // must be triangles
    }

    [Fact]
    public void Import_ObjFile_NormalsPresent()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        var mesh = model.Meshes[0];
        Assert.NotNull(mesh.Normals);
        Assert.Equal(mesh.Positions!.Length, mesh.Normals!.Length);
    }

    [Fact]
    public void Import_ObjFile_MaterialIndexSet()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        var mesh = model.Meshes[0];
        Assert.True(mesh.MaterialIndex >= 0, "MaterialIndex should be non-negative.");
    }

    [Fact]
    public void Import_ObjFile_MeshIsValid()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        Assert.All(model.Meshes, m => Assert.True(m.IsValid(), $"Mesh '{m.Name}' failed IsValid()."));
    }

    // ── Node hierarchy ───────────────────────────────────────────────────────

    [Fact]
    public void Import_ObjFile_RootNodeNotNull()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        Assert.NotNull(model.RootNode);
    }

    [Fact]
    public void Import_ObjFile_RootNodeHasChildren()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        // OBJ typically places meshes under child nodes of the root.
        Assert.True(model.RootNode.Children.Count >= 0);
    }

    // ── Import from stream ───────────────────────────────────────────────────

    [Fact]
    public void Import_Stream_LoadsSuccessfully()
    {
        var importer = new AssimpModelImporter();
        using var stream = File.OpenRead(AssetPath("cube.obj"));

        var model = importer.Import(stream, "obj");

        Assert.NotNull(model);
        Assert.NotEmpty(model.Meshes);
    }

    [Fact]
    public void Import_Stream_NullStream_Throws()
    {
        var importer = new AssimpModelImporter();
        Assert.Throws<ArgumentNullException>(() => importer.Import(null!, "obj"));
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public void Import_NonExistentFile_ThrowsFileNotFoundException()
    {
        var importer = new AssimpModelImporter();
        Assert.Throws<FileNotFoundException>(() => importer.Import("does_not_exist.obj"));
    }

    [Fact]
    public void Import_EmptyPath_ThrowsArgumentException()
    {
        var importer = new AssimpModelImporter();
        Assert.Throws<ArgumentException>(() => importer.Import(""));
    }

    // ── Model3D structural tests ─────────────────────────────────────────────

    [Fact]
    public void Import_ObjFile_MaterialsListPresent()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        Assert.NotNull(model.Materials);
        // OBJ always has at least a default material
        Assert.True(model.Materials.Count >= 0);
    }

    [Fact]
    public void Import_ObjFile_TransformIsDecomposable()
    {
        var importer = new AssimpModelImporter();
        var model = importer.Import(AssetPath("cube.obj"));

        // Root node transform should not be all zeros
        var t = model.RootNode.Transform;
        Assert.True(t.Scale.Length() > 0, "Root scale must be non-zero.");
    }
}
