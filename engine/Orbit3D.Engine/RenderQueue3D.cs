using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Represents one renderable instance produced from a scene hierarchy.
/// </summary>
public sealed class RenderItem3D
{
    /// <summary>
    /// Gets or sets the scene node that owns this renderable.
    /// </summary>
    public Node3D? Node { get; set; }

    /// <summary>
    /// Gets or sets the mesh to render.
    /// </summary>
    public Mesh3D? Mesh { get; set; }

    /// <summary>
    /// Gets or sets the resolved material.
    /// </summary>
    public Material3D? Material { get; set; }

    /// <summary>
    /// Gets or sets the world transform of the renderable.
    /// </summary>
    public Matrix4x4 WorldMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// Gets or sets the camera-relative visibility flag for the item.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets a stable ordering hint for deterministic traversal.
    /// </summary>
    public int SortKey { get; set; }
}

/// <summary>
/// Represents a backend-neutral render queue populated from a scene.
/// </summary>
public sealed class RenderQueue3D : IReadOnlyList<RenderItem3D>
{
    private readonly List<RenderItem3D> items = new();

    /// <summary>
    /// Gets the items in the queue in deterministic traversal order.
    /// </summary>
    public IReadOnlyList<RenderItem3D> Items => items;

    /// <summary>
    /// Gets the number of queued renderables.
    /// </summary>
    public int Count => items.Count;

    /// <summary>
    /// Adds a render item to the queue.
    /// </summary>
    public void Add(RenderItem3D item)
    {
        ArgumentNullException.ThrowIfNull(item);
        items.Add(item);
    }

    /// <summary>
    /// Clears all queued renderables.
    /// </summary>
    public void Clear() => items.Clear();

    /// <inheritdoc />
    public IEnumerator<RenderItem3D> GetEnumerator() => items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the render item at the supplied index.
    /// </summary>
    public RenderItem3D this[int index] => items[index];
}
