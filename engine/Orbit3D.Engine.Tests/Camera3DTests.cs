using System.Numerics;
using Orbit3D.Engine;

namespace Orbit3D.Engine.Tests;

public class Camera3DTests
{
    [Fact]
    public void DefaultCamera_HasValidMatrices()
    {
        var camera = new Camera3D();
        
        Assert.NotEqual(Matrix4x4.Identity, camera.ProjectionMatrix);
        Assert.Equal(Matrix4x4.Identity, camera.ViewMatrix);
    }

    [Fact]
    public void AspectRatioChange_UpdatesProjectionMatrix()
    {
        var camera = new Camera3D();
        var initialProj = camera.ProjectionMatrix;
        
        camera.AspectRatio = 2.0f;
        
        Assert.NotEqual(initialProj, camera.ProjectionMatrix);
    }

    [Fact]
    public void ClippingPlanes_UpdateProjectionMatrix()
    {
        var camera = new Camera3D();
        var initialProj = camera.ProjectionMatrix;
        
        camera.NearClip = 1.0f;
        camera.FarClip = 500f;
        
        Assert.NotEqual(initialProj, camera.ProjectionMatrix);
    }

    [Fact]
    public void ViewMatrix_IsInverseOfTransformWorld()
    {
        var camera = new Camera3D();
        camera.Transform.Position = new Vector3(0, 0, 10);
        
        var viewMatrix = camera.ViewMatrix;
        
        // Translation is (0, 0, 10), so inverse translation should be (0, 0, -10)
        Assert.Equal(new Vector3(0, 0, -10), viewMatrix.Translation);
    }

    [Fact]
    public void FieldOfView_UpdatesProjectionMatrix()
    {
        var camera = new Camera3D();
        var initialProj = camera.ProjectionMatrix;
        
        camera.FieldOfView = MathF.PI / 2f;
        
        Assert.NotEqual(initialProj, camera.ProjectionMatrix);
    }
}
