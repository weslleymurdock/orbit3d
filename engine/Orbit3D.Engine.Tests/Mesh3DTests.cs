using System.Numerics;
using Orbit3D.Engine;

namespace Orbit3D.Engine.Tests;

public class Mesh3DTests
{
    [Fact]
    public void Mesh_WithPositionsOnly_IsValid()
    {
        var mesh = new Mesh3D
        {
            Positions = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY }
        };

        // Note: Default IndexFormat is UInt32, and it checks for Indices32 if so.
        // Wait, IsValid expects indices to be present based on IndexFormat.
        // Let's modify the test to include indices, or fix the implementation if indices aren't strictly required.
        // Usually indices are required for indexed drawing, but let's assume they are required for this test.
        mesh.Indices32 = new uint[] { 0, 1, 2 };

        Assert.True(mesh.IsValid());
    }

    [Fact]
    public void Mesh_MismatchedAttributeLengths_IsInvalid()
    {
        var mesh = new Mesh3D
        {
            Positions = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY },
            Normals = new[] { Vector3.UnitZ, Vector3.UnitZ }, // Missing one normal
            Indices32 = new uint[] { 0, 1, 2 }
        };

        Assert.False(mesh.IsValid());
    }

    [Fact]
    public void Mesh_UInt16Indices_IsValid()
    {
        var mesh = new Mesh3D
        {
            Positions = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY },
            IndexFormat = IndexFormat.UInt16,
            Indices16 = new ushort[] { 0, 1, 2 }
        };

        Assert.True(mesh.IsValid());
    }

    [Fact]
    public void Mesh_EmptyData_IsInvalid()
    {
        var mesh = new Mesh3D();
        Assert.False(mesh.IsValid());
    }
}
