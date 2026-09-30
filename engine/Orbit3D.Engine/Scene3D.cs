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
    /// Gets or sets the camera for the scene.
    /// </summary>
    public Camera3D? Camera
    {
        get => MainCamera;
        set => MainCamera = value;
    }

    /// <summary>
    /// Gets the list of lights in the scene.
    /// </summary>
    public List<Light3D> Lights { get; } = new List<Light3D>();

    /// <summary>
    /// Builds a render queue from the scene hierarchy.
    /// </summary>
    public RenderQueue3D BuildRenderQueue()
    {
        var queue = new RenderQueue3D();
        PopulateRenderQueue(RootNode, queue, 0);
        return queue;
    }

    /// <summary>
    /// Builds a render queue and applies the active camera view/projection setup for runtime rendering.
    /// </summary>
    public RenderQueue3D BuildRenderQueue(Camera3D? camera, int viewportWidth = 0, int viewportHeight = 0)
    {
        if (camera is not null)
        {
            camera.AspectRatio = viewportWidth > 0 && viewportHeight > 0 ? viewportWidth / (float)viewportHeight : camera.AspectRatio;
        }

        var queue = new RenderQueue3D();
        PopulateRenderQueue(RootNode, queue, 0, camera);
        return queue;
    }

    /// <summary>
    /// Creates a render queue from the scene hierarchy.
    /// </summary>
    public RenderQueue3D CreateRenderQueue() => BuildRenderQueue();

    /// <summary>
    /// Gets a render queue from the scene hierarchy.
    /// </summary>
    public RenderQueue3D GetRenderQueue() => BuildRenderQueue();

    private static void PopulateRenderQueue(Node3D node, RenderQueue3D queue, int sortKey, Camera3D? camera = null)
    {
        if (node.Model is not null && node.IsVisible)
        {
            var world = node.WorldMatrix;
            foreach (var mesh in node.Model.Meshes)
            {
                var material = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < node.Model.Materials.Count
                    ? node.Model.Materials[mesh.MaterialIndex]
                    : null;

                queue.Add(new RenderItem3D
                {
                    Node = node,
                    Mesh = mesh,
                    Material = material,
                    WorldMatrix = world,
                    IsVisible = mesh is not null && node.IsVisible,
                    SortKey = sortKey
                });
            }
        }

        var childSortKey = sortKey + 1;
        foreach (var child in node.Children)
            PopulateRenderQueue(child, queue, childSortKey, camera);
    }
}
