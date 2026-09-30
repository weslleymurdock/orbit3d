namespace Orbit3D.Engine;

/// <summary>
/// Represents an engine-owned 3D model containing a hierarchy, meshes, materials, and textures.
/// Suitable for receiving data converted from imported assets (e.g., Assimp).
/// </summary>
public class Model3D
{
    /// <summary>
    /// Gets the root node of the model's hierarchy.
    /// </summary>
    public Node3D RootNode { get; } = new Node3D { Name = "ModelRoot" };

    /// <summary>
    /// Gets the list of meshes contained in this model.
    /// </summary>
    public List<Mesh3D> Meshes { get; } = new List<Mesh3D>();

    /// <summary>
    /// Gets the list of materials contained in this model.
    /// </summary>
    public List<Material3D> Materials { get; } = new List<Material3D>();

    /// <summary>
    /// Gets the list of textures contained in this model.
    /// </summary>
    public List<Texture2D> Textures { get; } = new List<Texture2D>();
}
