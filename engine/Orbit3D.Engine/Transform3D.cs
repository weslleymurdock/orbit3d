using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Represents a 3D transformation in space, including position, rotation, and scale.
/// Uses a right-handed coordinate system (Y-up, -Z forward).
/// </summary>
public class Transform3D
{
    private Vector3 _position = Vector3.Zero;
    private Quaternion _rotation = Quaternion.Identity;
    private Vector3 _scale = Vector3.One;

    private Matrix4x4 _localMatrix = Matrix4x4.Identity;
    private Matrix4x4 _worldMatrix = Matrix4x4.Identity;
    private bool _isLocalDirty = false;
    private bool _isWorldDirty = false;

    /// <summary>
    /// Gets or sets the parent transform.
    /// </summary>
    public Transform3D? Parent { get; private set; }

    private readonly List<Transform3D> _children = new();

    /// <summary>
    /// Gets the children transforms.
    /// </summary>
    public IReadOnlyList<Transform3D> Children => _children;

    /// <summary>
    /// Gets or sets the local position.
    /// </summary>
    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            SetDirty();
        }
    }

    /// <summary>
    /// Gets or sets the local rotation.
    /// </summary>
    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            SetDirty();
        }
    }

    /// <summary>
    /// Gets or sets the local scale.
    /// </summary>
    public Vector3 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            SetDirty();
        }
    }

    /// <summary>
    /// Gets the local transformation matrix.
    /// </summary>
    public Matrix4x4 LocalMatrix
    {
        get
        {
            if (_isLocalDirty)
            {
                _localMatrix = Matrix4x4.CreateScale(_scale) *
                               Matrix4x4.CreateFromQuaternion(_rotation) *
                               Matrix4x4.CreateTranslation(_position);
                _isLocalDirty = false;
            }

            return _localMatrix;
        }
    }

    /// <summary>
    /// Gets the world transformation matrix.
    /// </summary>
    public Matrix4x4 WorldMatrix
    {
        get
        {
            if (_isWorldDirty)
            {
                if (Parent != null)
                {
                    _worldMatrix = LocalMatrix * Parent.WorldMatrix;
                }
                else
                {
                    _worldMatrix = LocalMatrix;
                }

                _isWorldDirty = false;
            }

            return _worldMatrix;
        }
    }

    /// <summary>
    /// Sets the parent of this transform.
    /// </summary>
    /// <param name="parent">The new parent transform.</param>
    public void SetParent(Transform3D? parent)
    {
        if (Parent == parent) return;

        Parent?._children.Remove(this);
        Parent = parent;
        Parent?._children.Add(this);

        SetDirty(true);
    }

    private void SetDirty(bool worldOnly = false)
    {
        if (!worldOnly)
        {
            _isLocalDirty = true;
        }

        _isWorldDirty = true;

        foreach (var child in _children)
        {
            child.SetDirty(true);
        }
    }
}
