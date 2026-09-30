using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Specifies the format of the indices in a mesh.
/// </summary>
public enum IndexFormat
{
    /// <summary>16-bit unsigned integer.</summary>
    UInt16,
    /// <summary>32-bit unsigned integer.</summary>
    UInt32
}

/// <summary>
/// Specifies how vertices are assembled into primitives.
/// </summary>
public enum PrimitiveTopology
{
    /// <summary>Point list.</summary>
    PointList,
    /// <summary>Line list.</summary>
    LineList,
    /// <summary>Line strip.</summary>
    LineStrip,
    /// <summary>Triangle list.</summary>
    TriangleList,
    /// <summary>Triangle strip.</summary>
    TriangleStrip
}

/// <summary>
/// Represents a backend-neutral 3D mesh containing vertex and index data.
/// </summary>
public class Mesh3D
{
    /// <summary>
    /// Gets or sets the name of the mesh.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vertex positions.
    /// </summary>
    public Vector3[]? Positions { get; set; }

    /// <summary>
    /// Gets or sets the vertex normals.
    /// </summary>
    public Vector3[]? Normals { get; set; }

    /// <summary>
    /// Gets or sets the vertex tangents.
    /// </summary>
    public Vector3[]? Tangents { get; set; }

    /// <summary>
    /// Gets or sets the texture coordinates.
    /// </summary>
    public Vector2[]? TextureCoordinates { get; set; }

    /// <summary>
    /// Gets or sets the vertex colors.
    /// </summary>
    public Vector4[]? Colors { get; set; }

    /// <summary>
    /// Gets or sets the primitive topology.
    /// </summary>
    public PrimitiveTopology Topology { get; set; } = PrimitiveTopology.TriangleList;

    /// <summary>
    /// Gets or sets the index format.
    /// </summary>
    public IndexFormat IndexFormat { get; set; } = IndexFormat.UInt32;

    /// <summary>
    /// Gets or sets the 16-bit indices. Used if IndexFormat is UInt16.
    /// </summary>
    public ushort[]? Indices16 { get; set; }

    /// <summary>
    /// Gets or sets the 32-bit indices. Used if IndexFormat is UInt32.
    /// </summary>
    public uint[]? Indices32 { get; set; }

    /// <summary>
    /// Gets or sets the material reference index or ID.
    /// </summary>
    public int MaterialIndex { get; set; } = -1;

    /// <summary>
    /// Validates that the mesh data is well-formed.
    /// </summary>
    /// <returns>True if the mesh is valid.</returns>
    public bool IsValid()
    {
        if (Positions == null || Positions.Length == 0) return false;

        int vertexCount = Positions.Length;
        if (Normals != null && Normals.Length != vertexCount) return false;
        if (Tangents != null && Tangents.Length != vertexCount) return false;
        if (TextureCoordinates != null && TextureCoordinates.Length != vertexCount) return false;
        if (Colors != null && Colors.Length != vertexCount) return false;

        if (IndexFormat == IndexFormat.UInt16 && (Indices16 == null || Indices16.Length == 0)) return false;
        if (IndexFormat == IndexFormat.UInt32 && (Indices32 == null || Indices32.Length == 0)) return false;

        return true;
    }
}
