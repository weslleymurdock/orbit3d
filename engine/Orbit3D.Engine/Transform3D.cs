using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Represents a 3D transformation in space, including position, rotation, and scale.
/// Uses a right-handed coordinate system (Y-up, -Z forward).
/// </summary>
public class Transform3D
{
    private Vector3 position = Vector3.Zero;
    private Quaternion rotation = Quaternion.Identity;
    private Vector3 scale = Vector3.One;

    private Matrix4x4 localMatrix = Matrix4x4.Identity;
    private Matrix4x4 worldMatrix = Matrix4x4.Identity;
    private bool isLocalDirty = false;
    private bool isWorldDirty = false;

    /// <summary>
    /// Gets or sets the parent transform.
    /// </summary>
    public Transform3D? Parent { get; private set; }

    private readonly List<Transform3D> children = new();

    /// <summary>
    /// Gets the children transforms.
    /// </summary>
    public IReadOnlyList<Transform3D> Children => children;

    /// <summary>
    /// Gets or sets the local position.
    /// </summary>
    public Vector3 Position
    {
        get => position;
        set
        {
            position = value;
            SetDirty();
        }
    }

    /// <summary>
    /// Gets or sets the local rotation.
    /// </summary>
    public Quaternion Rotation
    {
        get => rotation;
        set
        {
            rotation = value;
            SetDirty();
        }
    }

    /// <summary>
    /// Gets or sets the local scale.
    /// </summary>
    public Vector3 Scale
    {
        get => scale;
        set
        {
            scale = value;
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
            if (isLocalDirty)
            {
                localMatrix = Matrix4x4.CreateScale(scale) *
                               Matrix4x4.CreateFromQuaternion(rotation) *
                               Matrix4x4.CreateTranslation(position);
                isLocalDirty = false;
            }

            return localMatrix;
        }
    }

    /// <summary>
    /// Gets the world transformation matrix.
    /// </summary>
    public Matrix4x4 WorldMatrix
    {
        get
        {
            if (isWorldDirty)
            {
                if (Parent != null)
                {
                    worldMatrix = LocalMatrix * Parent.WorldMatrix;
                }
                else
                {
                    worldMatrix = LocalMatrix;
                }

                isWorldDirty = false;
            }

            return worldMatrix;
        }
    }

    /// <summary>
    /// Sets the parent of this transform.
    /// </summary>
    /// <param name="parent">The new parent transform.</param>
    public void SetParent(Transform3D? parent)
    {
        if (Parent == parent) return;

        Parent?.children.Remove(this);
        Parent = parent;
        Parent?.children.Add(this);

        SetDirty(true);
    }

    private void SetDirty(bool worldOnly = false)
    {
        if (!worldOnly)
        {
            isLocalDirty = true;
        }

        isWorldDirty = true;

        foreach (var child in children)
        {
            child.SetDirty(true);
        }
    }
}
