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

    [Fact]
    public void TransformMotion_UpdatesViewMatrix()
    {
        var camera = new Camera3D();
        var initialView = camera.ViewMatrix;

        camera.Transform.Position = new Vector3(3f, 2f, -5f);
        camera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.7f, -0.5f, 0f);

        var updatedView = camera.ViewMatrix;
        Assert.NotEqual(initialView, updatedView);
        Assert.NotEqual(Matrix4x4.Identity, updatedView);
    }

    [Fact]
    public void InvalidProjectionSettings_ThrowArgumentOutOfRangeException()
    {
        var camera = new Camera3D();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FieldOfView = 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FieldOfView = MathF.PI + 0.01f);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.AspectRatio = 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.NearClip = 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FarClip = 0.1f);
    }
}
