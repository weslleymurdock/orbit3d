namespace Orbit3D.Engine;

/// <summary>
/// Represents a node in the 3D scene hierarchy.
/// </summary>
public class Node3D
{
    /// <summary>
    /// Gets or sets the name of the node.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the transform of this node.
    /// </summary>
    public Transform3D Transform { get; } = new Transform3D();

    /// <summary>
    /// Gets the world matrix for this node.
    /// </summary>
    public System.Numerics.Matrix4x4 WorldMatrix => Transform.WorldMatrix;

    /// <summary>
    /// Gets or sets a value indicating whether this node participates in the active render queue.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the model associated with this node, if any.
    /// </summary>
    public Model3D? Model { get; set; }

    /// <summary>
    /// Gets the parent node.
    /// </summary>
    public Node3D? Parent { get; private set; }

    private readonly List<Node3D> _children = new();

    /// <summary>
    /// Gets the list of child nodes.
    /// </summary>
    public IReadOnlyList<Node3D> Children => _children;

    /// <summary>
    /// Enumerates the node and all descendants in depth-first order.
    /// </summary>
    public IEnumerable<Node3D> EnumerateDescendants()
    {
        foreach (var child in _children)
        {
            yield return child;
            foreach (var descendant in child.EnumerateDescendants())
                yield return descendant;
        }
    }

    /// <summary>
    /// Sets the parent of this node and updates the transform hierarchy.
    /// </summary>
    /// <param name="parent">The parent node.</param>
    public void SetParent(Node3D? parent)
    {
        if (ReferenceEquals(parent, this))
            throw new ArgumentException("A node cannot be its own parent.", nameof(parent));

        if (parent is not null && IsDescendantOf(parent, this))
            throw new ArgumentException("A node cannot be parented to one of its descendants.", nameof(parent));

        if (Parent == parent) return;

        Parent?._children.Remove(this);
        Parent = parent;
        Parent?._children.Add(this);

        Transform.SetParent(parent?.Transform);
    }

    private static bool IsDescendantOf(Node3D candidate, Node3D ancestor)
    {
        var current = candidate;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;

            current = current.Parent;
        }

        return false;
    }
}
