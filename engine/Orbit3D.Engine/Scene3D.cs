namespace Orbit3D.Engine;

/// <summary>
/// Represents a 3D scene containing a hierarchy of nodes.
/// </summary>
public class Scene3D
{
    /// <summary>
    /// Gets the root node of the scene.
    /// </summary>
    public Node3D RootNode { get; } = new Node3D { Name = "Root" };

    /// <summary>
    /// Gets or sets the main camera for the scene.
    /// </summary>
    public Camera3D? MainCamera { get; set; }

    /// <summary>
    /// Gets the list of lights in the scene.
    /// </summary>
    public List<Light3D> Lights { get; } = new List<Light3D>();
}
