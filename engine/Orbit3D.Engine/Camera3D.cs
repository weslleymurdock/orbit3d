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
            if (!float.IsFinite(value) || value <= 0f || value >= MathF.PI)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Field of view must be finite, greater than zero, and less than PI radians.");

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
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Aspect ratio must be finite and greater than zero.");

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
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Near clipping plane must be finite and greater than zero.");
            if (value >= farClip)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Near clipping plane must be less than the far clipping plane.");

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
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Far clipping plane must be finite and greater than zero.");
            if (value <= nearClip)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Far clipping plane must be greater than the near clipping plane.");

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
