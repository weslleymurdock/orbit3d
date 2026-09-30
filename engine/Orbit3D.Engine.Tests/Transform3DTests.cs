using System.Numerics;
using Orbit3D.Engine;

namespace Orbit3D.Engine.Tests;

public class Transform3DTests
{
    [Fact]
    public void Identity_HasExpectedValues()
    {
        var transform = new Transform3D();
        Assert.Equal(Vector3.Zero, transform.Position);
        Assert.Equal(Quaternion.Identity, transform.Rotation);
        Assert.Equal(Vector3.One, transform.Scale);
        Assert.Equal(Matrix4x4.Identity, transform.LocalMatrix);
        Assert.Equal(Matrix4x4.Identity, transform.WorldMatrix);
    }

    [Fact]
    public void Translation_UpdatesMatrices()
    {
        var transform = new Transform3D
        {
            Position = new Vector3(1, 2, 3)
        };
        
        var expected = Matrix4x4.CreateTranslation(1, 2, 3);
        Assert.Equal(expected, transform.LocalMatrix);
        Assert.Equal(expected, transform.WorldMatrix);
    }

    [Fact]
    public void Rotation_UpdatesMatrices()
    {
        var transform = new Transform3D();
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        transform.Rotation = rotation;

        var expected = Matrix4x4.CreateFromQuaternion(rotation);
        Assert.Equal(expected, transform.LocalMatrix);
    }

    [Fact]
    public void Scale_UpdatesMatrices()
    {
        var transform = new Transform3D
        {
            Scale = new Vector3(2, 2, 2)
        };

        var expected = Matrix4x4.CreateScale(2);
        Assert.Equal(expected, transform.LocalMatrix);
    }

    [Fact]
    public void ParentChildComposition_CalculatesWorldCorrectly()
    {
        var parent = new Transform3D { Position = new Vector3(10, 0, 0) };
        var child = new Transform3D { Position = new Vector3(5, 0, 0) };
        child.SetParent(parent);

        // World matrix should be child * parent
        var expectedWorld = Matrix4x4.CreateTranslation(5, 0, 0) * Matrix4x4.CreateTranslation(10, 0, 0);
        Assert.Equal(expectedWorld, child.WorldMatrix);
        
        // Extracted translation from world matrix should be (15, 0, 0)
        Assert.Equal(new Vector3(15, 0, 0), child.WorldMatrix.Translation);
    }

    [Fact]
    public void NestedHierarchy_UpdatesWhenParentChanges()
    {
        var root = new Transform3D { Position = new Vector3(1, 0, 0) };
        var node1 = new Transform3D { Position = new Vector3(2, 0, 0) };
        var node2 = new Transform3D { Position = new Vector3(3, 0, 0) };
        
        node1.SetParent(root);
        node2.SetParent(node1);

        Assert.Equal(new Vector3(6, 0, 0), node2.WorldMatrix.Translation);

        // Move root
        root.Position = new Vector3(10, 0, 0);
        
        // World position of node2 should update
        Assert.Equal(new Vector3(15, 0, 0), node2.WorldMatrix.Translation);
    }
}
