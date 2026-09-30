using Orbit3D.Engine;
using System.Numerics;

namespace Orbit3D.Engine.Tests;

public class Node3DTests
{
    [Fact]
    public void SetParent_UpdatesHierarchy()
    {
        var parent = new Node3D { Name = "Parent" };
        var child = new Node3D { Name = "Child" };
        
        child.SetParent(parent);
        
        Assert.Same(parent, child.Parent);
        Assert.Contains(child, parent.Children);
        Assert.Same(parent.Transform, child.Transform.Parent);
    }

    [Fact]
    public void SetParent_NullRemovesFromHierarchy()
    {
        var parent = new Node3D { Name = "Parent" };
        var child = new Node3D { Name = "Child" };
        
        child.SetParent(parent);
        child.SetParent(null);
        
        Assert.Null(child.Parent);
        Assert.DoesNotContain(child, parent.Children);
        Assert.Null(child.Transform.Parent);
    }

    [Fact]
    public void Transform_IsAccessibleAndModifiable()
    {
        var node = new Node3D();
        node.Transform.Position = new Vector3(1, 2, 3);
        
        Assert.Equal(new Vector3(1, 2, 3), node.Transform.Position);
    }
}
