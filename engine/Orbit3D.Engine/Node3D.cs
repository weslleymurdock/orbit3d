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
    /// Sets the parent of this node and updates the transform hierarchy.
    /// </summary>
    /// <param name="parent">The parent node.</param>
    public void SetParent(Node3D? parent)
    {
        if (Parent == parent) return;

        Parent?._children.Remove(this);
        Parent = parent;
        Parent?._children.Add(this);

        Transform.SetParent(parent?.Transform);
    }
}
