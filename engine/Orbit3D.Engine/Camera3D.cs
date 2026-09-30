using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Represents a 3D camera with perspective projection.
/// Uses a right-handed coordinate system.
/// </summary>
public class Camera3D
{
    /// <summary>
    /// Gets the transform of the camera.
    /// </summary>
    public Transform3D Transform { get; } = new Transform3D();

    private float fieldOfView = MathF.PI / 4f; // 45 degrees
    private float aspectRatio = 16f / 9f;
    private float nearClip = 0.1f;
    private float farClip = 1000f;

    private Matrix4x4 projectionMatrix;
    private bool isProjectionDirty = true;

    /// <summary>
    /// Gets or sets the field of view in radians.
    /// </summary>
    public float FieldOfView
    {
        get => fieldOfView;
        set
        {
            fieldOfView = value;
            isProjectionDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the aspect ratio (width / height).
    /// </summary>
    public float AspectRatio
    {
        get => aspectRatio;
        set
        {
            aspectRatio = value;
            isProjectionDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the near clipping plane distance.
    /// </summary>
    public float NearClip
    {
        get => nearClip;
        set
        {
            nearClip = value;
            isProjectionDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the far clipping plane distance.
    /// </summary>
    public float FarClip
    {
        get => farClip;
        set
        {
            farClip = value;
            isProjectionDirty = true;
        }
    }

    /// <summary>
    /// Gets the view matrix based on the camera's world transform.
    /// </summary>
    public Matrix4x4 ViewMatrix
    {
        get
        {
            // The view matrix is the inverse of the camera's world matrix.
            Matrix4x4.Invert(Transform.WorldMatrix, out var viewMatrix);
            return viewMatrix;
        }
    }

    /// <summary>
    /// Gets the projection matrix.
    /// </summary>
    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            if (isProjectionDirty)
            {
                // Note: System.Numerics CreatePerspectiveFieldOfView creates a right-handed perspective projection
                // that maps Z to [-1, 1]. If a different depth range (e.g., [0, 1]) is required by the graphics API,
                // this may need to be adjusted or abstracted in the renderer layer.
                projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearClip, farClip);
                isProjectionDirty = false;
            }

            return projectionMatrix;
        }
    }
}
