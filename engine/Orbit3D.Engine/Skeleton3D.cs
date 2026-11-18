using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Represents a single node in a skeleton's hierarchical transform tree, including intermediate pivot nodes.
/// Nodes are stored in topological parent-before-child order so hierarchy evaluation is linear and allocation-free.
/// </summary>
public sealed class SkeletonNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonNode"/> class.
    /// </summary>
    public SkeletonNode(
        int index,
        string name,
        int parentIndex,
        Matrix4x4 bindLocalTransform,
        Vector3 bindPosition,
        Quaternion bindRotation,
        Vector3 bindScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(name);
        if (parentIndex >= index)
            throw new ArgumentOutOfRangeException(nameof(parentIndex), "Parent node index must precede child node index.");

        Index = index;
        Name = name;
        ParentIndex = parentIndex;
        BindLocalTransform = bindLocalTransform;
        BindPosition = bindPosition;
        BindRotation = bindRotation;
        BindScale = bindScale;
    }

    /// <summary>Gets the zero-based topological index of this node.</summary>
    public int Index { get; }

    /// <summary>Gets the node name.</summary>
    public string Name { get; }

    /// <summary>Gets the parent node index, or -1 if this is the root node.</summary>
    public int ParentIndex { get; }

    /// <summary>Gets the local bind-pose transformation matrix.</summary>
    public Matrix4x4 BindLocalTransform { get; }

    /// <summary>Gets the decomposed bind-pose local translation.</summary>
    public Vector3 BindPosition { get; }

    /// <summary>Gets the decomposed bind-pose local rotation.</summary>
    public Quaternion BindRotation { get; }

    /// <summary>Gets the decomposed bind-pose local scale.</summary>
    public Vector3 BindScale { get; }
}

/// <summary>
/// Represents a skinning bone in a <see cref="Skeleton3D"/>, linking a hierarchy node to an inverse bind matrix.
/// </summary>
public sealed class Bone3D
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Bone3D"/> class.
    /// </summary>
    public Bone3D(
        int index,
        string name,
        int nodeIndex,
        int parentBoneIndex,
        Matrix4x4 inverseBindMatrix,
        Matrix4x4 bindLocalTransform)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(nodeIndex);

        Index = index;
        Name = name;
        NodeIndex = nodeIndex;
        ParentBoneIndex = parentBoneIndex;
        InverseBindMatrix = inverseBindMatrix;
        BindLocalTransform = bindLocalTransform;
    }

    /// <summary>Gets the zero-based bone index used by vertex skinning weights.</summary>
    public int Index { get; }

    /// <summary>Gets the stable bone name.</summary>
    public string Name { get; }

    /// <summary>Gets the index of the corresponding node in <see cref="Skeleton3D.Nodes"/>.</summary>
    public int NodeIndex { get; }

    /// <summary>Gets the index of the nearest ancestor bone in <see cref="Skeleton3D.Bones"/>, or -1 if this is a root bone.</summary>
    public int ParentBoneIndex { get; }

    /// <summary>Gets the inverse bind-pose (offset) matrix transforming from mesh space into bone space.</summary>
    public Matrix4x4 InverseBindMatrix { get; }

    /// <summary>Gets the local bind-pose transform of this bone's node.</summary>
    public Matrix4x4 BindLocalTransform { get; }
}

/// <summary>
/// Represents a backend-neutral skeletal hierarchy and bone palette for 3D skinning.
/// </summary>
public sealed class Skeleton3D
{
    private readonly Dictionary<string, int> nodeNameToIndex;
    private readonly Dictionary<string, int> boneNameToIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Skeleton3D"/> class.
    /// </summary>
    public Skeleton3D(
        string name,
        IReadOnlyList<SkeletonNode> nodes,
        IReadOnlyList<Bone3D> bones,
        Matrix4x4 globalInverseTransform)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(bones);
        if (nodes.Count == 0)
            throw new ArgumentException("Skeleton must contain at least one hierarchy node.", nameof(nodes));

        Name = name;
        Nodes = nodes.ToArray();
        Bones = bones.ToArray();
        GlobalInverseTransform = globalInverseTransform;

        nodeNameToIndex = new Dictionary<string, int>(Nodes.Count, StringComparer.Ordinal);
        for (var i = 0; i < Nodes.Count; i++)
        {
            nodeNameToIndex.TryAdd(Nodes[i].Name, i);
        }

        boneNameToIndex = new Dictionary<string, int>(Bones.Count, StringComparer.Ordinal);
        for (var i = 0; i < Bones.Count; i++)
        {
            boneNameToIndex.TryAdd(Bones[i].Name, i);
        }
    }

    /// <summary>Gets the skeleton name.</summary>
    public string Name { get; }

    /// <summary>Gets all hierarchy nodes in topological parent-before-child order.</summary>
    public IReadOnlyList<SkeletonNode> Nodes { get; }

    /// <summary>Gets all skinning bones.</summary>
    public IReadOnlyList<Bone3D> Bones { get; }

    /// <summary>Gets the inverse of the root node's bind transform.</summary>
    public Matrix4x4 GlobalInverseTransform { get; }

    /// <summary>Gets the number of skinning bones.</summary>
    public int BoneCount => Bones.Count;

    /// <summary>Finds a hierarchy node index by name, or -1 if not found.</summary>
    public int FindNodeIndex(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1;
        return nodeNameToIndex.TryGetValue(name, out var index) ? index : -1;
    }

    /// <summary>Finds a bone index by name, or -1 if not found.</summary>
    public int FindBoneIndex(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1;
        return boneNameToIndex.TryGetValue(name, out var index) ? index : -1;
    }

    /// <summary>
    /// Evaluates the bind-pose bone palette into <paramref name="boneMatrices"/>.
    /// </summary>
    public void EvaluateBindPose(Span<Matrix4x4> boneMatrices)
    {
        if (boneMatrices.Length < Bones.Count)
            throw new ArgumentException("Destination span is smaller than BoneCount.", nameof(boneMatrices));

        Span<Matrix4x4> globalTransforms = Nodes.Count <= 512
            ? stackalloc Matrix4x4[Nodes.Count]
            : new Matrix4x4[Nodes.Count];

        for (var i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];
            globalTransforms[i] = node.ParentIndex >= 0
                ? node.BindLocalTransform * globalTransforms[node.ParentIndex]
                : node.BindLocalTransform;
        }

        for (var i = 0; i < Bones.Count; i++)
        {
            var bone = Bones[i];
            boneMatrices[i] = bone.InverseBindMatrix * globalTransforms[bone.NodeIndex] * GlobalInverseTransform;
        }
    }
}
