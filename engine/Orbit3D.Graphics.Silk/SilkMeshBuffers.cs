using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// Owns the Silk GPU buffers uploaded from an Orbit mesh. The device also tracks and disposes these resources.
/// </summary>
public sealed class SilkMeshBuffers : IDisposable
{
    private SilkMeshBuffers(IVertexBuffer vertices, IIndexBuffer indices, PrimitiveTopology topology)
    {
        VertexBuffer = vertices;
        IndexBuffer = indices;
        Topology = topology;
    }

    /// <summary>Gets the uploaded vertex buffer.</summary>
    public IVertexBuffer VertexBuffer { get; }

    /// <summary>Gets the uploaded index buffer.</summary>
    public IIndexBuffer IndexBuffer { get; }

    /// <summary>Gets the source mesh topology for pipeline creation.</summary>
    public PrimitiveTopology Topology { get; }

    /// <summary>Uploads an Orbit mesh to a Silk device once for reuse by subsequent frames.</summary>
    public static SilkMeshBuffers Create(IRenderDevice device, Mesh3D mesh)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(mesh);
        if (device is not SilkRenderDevice)
            throw new ArgumentException("Mesh upload requires a SilkRenderDevice.", nameof(device));
        if (!mesh.IsValid())
            throw new ArgumentException("Mesh data is invalid or incomplete.", nameof(mesh));
        if (!Enum.IsDefined(mesh.Topology))
            throw new ArgumentOutOfRangeException(nameof(mesh), "Mesh topology is not supported.");

        var positions = mesh.Positions!;
        if (mesh.IndexFormat == IndexFormat.UInt16)
        {
            foreach (var index in mesh.Indices16!)
            {
                if (index >= positions.Length)
                    throw new ArgumentException("Mesh contains an index outside its vertex range.", nameof(mesh));
            }
        }
        else
        {
            foreach (var index in mesh.Indices32!)
            {
                if (index >= positions.Length)
                    throw new ArgumentException("Mesh contains an index outside its vertex range.", nameof(mesh));
            }
        }

        var normals = mesh.Normals;
        var uvs = mesh.TextureCoordinates;
        var tangents = normals is not null && uvs is not null ? mesh.Tangents : null;
        var hasNormals = normals is not null;
        var hasUvs = uvs is not null;
        var stride = sizeof(float) * (3 + (hasNormals ? 3 : 0) + (hasUvs ? 2 : 0) + (tangents is not null ? 3 : 0));
        var vertexData = new byte[checked(positions.Length * stride)];
        for (var vertexIndex = 0; vertexIndex < positions.Length; vertexIndex++)
        {
            var destination = vertexData.AsSpan(vertexIndex * stride, stride);
            var offset = 0;
            WriteVector3(destination, ref offset, positions[vertexIndex]);
            if (normals is not null)
                WriteVector3(destination, ref offset, normals[vertexIndex]);
            if (uvs is not null)
                WriteVector2(destination, ref offset, uvs[vertexIndex]);
            if (tangents is not null)
                WriteVector3(destination, ref offset, tangents[vertexIndex]);
        }

        var indexCount = mesh.IndexFormat == IndexFormat.UInt16 ? mesh.Indices16!.Length : mesh.Indices32!.Length;
        var indexData = mesh.IndexFormat == IndexFormat.UInt16
            ? MemoryMarshal.AsBytes(mesh.Indices16!.AsSpan()).ToArray()
            : MemoryMarshal.AsBytes(mesh.Indices32!.AsSpan()).ToArray();
        var vertexBuffer = device.CreateVertexBuffer(new VertexBufferDescription(positions.Length, stride), vertexData);
        try
        {
            var indexBuffer = device.CreateIndexBuffer(new IndexBufferDescription(indexCount, mesh.IndexFormat), indexData);
            return new SilkMeshBuffers(vertexBuffer, indexBuffer, mesh.Topology);
        }
        catch
        {
            vertexBuffer.Dispose();
            throw;
        }
    }

    /// <summary>Disposes both uploaded buffers; repeated calls are safe.</summary>
    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
    }

    private static void WriteVector2(Span<byte> destination, ref int offset, Vector2 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value.X);
        offset += sizeof(float);
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value.Y);
        offset += sizeof(float);
    }

    private static void WriteVector3(Span<byte> destination, ref int offset, Vector3 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value.X);
        offset += sizeof(float);
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value.Y);
        offset += sizeof(float);
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value.Z);
        offset += sizeof(float);
    }
}