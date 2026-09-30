using Orbit3D.Engine;

namespace Orbit3D.Engine.Tests;

public class Model3DTests
{
    [Fact]
    public void Model_ContainsExpectedCollections()
    {
        var model = new Model3D();
        
        Assert.NotNull(model.RootNode);
        Assert.NotNull(model.Meshes);
        Assert.NotNull(model.Materials);
        Assert.NotNull(model.Textures);
        
        Assert.Empty(model.Meshes);
        Assert.Empty(model.Materials);
        Assert.Empty(model.Textures);
    }

    [Fact]
    public void Model_HierarchyAndResources_CanBeAssociated()
    {
        var model = new Model3D();
        
        var mesh = new Mesh3D { Name = "Cube" };
        model.Meshes.Add(mesh);
        
        var material = new Material3D { Name = "DefaultMaterial" };
        model.Materials.Add(material);
        
        mesh.MaterialIndex = 0; // Reference to the first material
        
        var childNode = new Node3D { Name = "CubeNode", Model = model };
        childNode.SetParent(model.RootNode);
        
        Assert.Single(model.RootNode.Children);
        Assert.Equal("CubeNode", model.RootNode.Children[0].Name);
        Assert.Same(model, model.RootNode.Children[0].Model);
        Assert.Single(model.Meshes);
        Assert.Single(model.Materials);
    }
}
