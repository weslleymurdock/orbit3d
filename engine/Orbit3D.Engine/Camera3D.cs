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

    private float _fieldOfView = MathF.PI / 4f; // 45 degrees
    private float _aspectRatio = 16f / 9f;
    private float _nearClip = 0.1f;
    private float _farClip = 1000f;

    private Matrix4x4 _projectionMatrix;
    private bool _isProjectionDirty = true;

    /// <summary>
    /// Gets or sets the field of view in radians.
    /// </summary>
    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            _fieldOfView = value;
            _isProjectionDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the aspect ratio (width / height).
    /// </summary>
    public float AspectRatio
    {
        get => _aspectRatio;
        set
        {
            _aspectRatio = value;
            _isProjectionDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the near clipping plane distance.
    /// </summary>
    public float NearClip
    {
        get => _nearClip;
        set
        {
            _nearClip = value;
            _isProjectionDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the far clipping plane distance.
    /// </summary>
    public float FarClip
    {
        get => _farClip;
        set
        {
            _farClip = value;
            _isProjectionDirty = true;
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
            if (_isProjectionDirty)
            {
                // Note: System.Numerics CreatePerspectiveFieldOfView creates a right-handed perspective projection
                // that maps Z to [-1, 1]. If a different depth range (e.g., [0, 1]) is required by the graphics API,
                // this may need to be adjusted or abstracted in the renderer layer.
                _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(_fieldOfView, _aspectRatio, _nearClip, _farClip);
                _isProjectionDirty = false;
            }

            return _projectionMatrix;
        }
    }
}
