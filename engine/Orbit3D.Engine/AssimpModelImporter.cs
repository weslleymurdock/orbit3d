#if ANDROID || IOS || MACCATALYST || WINDOWS
using System.Numerics;
using Assimp.Maui;

namespace Orbit3D.Engine;

/// <summary>
/// Imports 3D assets through Assimp.MAUI and converts them to Orbit3D runtime types.
/// </summary>
public sealed class AssimpModelImporter : IModelImporter
{
    private const uint DefaultFlags =
        (uint)(
            PostProcessSteps.Process_Triangulate |
            PostProcessSteps.Process_JoinIdenticalVertices |
            PostProcessSteps.Process_GenNormals |
            PostProcessSteps.Process_CalcTangentSpace |
            PostProcessSteps.Process_FlipUVs);

    /// <inheritdoc />
    public Model3D Import(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path must not be null or empty.", nameof(filePath));

        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException("Asset file not found.", filePath);

        var scene = global::Assimp.Maui.Assimp.ImportFile(filePath, DefaultFlags);
        if (scene is null)
        {
            var msg = global::Assimp.Maui.Assimp.GetErrorString();
            throw new InvalidOperationException(
                $"Assimp failed to import '{filePath}': {msg}");
        }
        try
        {
            return ConvertScene(scene);
        }
        finally
        {
            scene.Dispose();
        }
    }

    /// <inheritdoc />
    public Model3D Import(Stream stream, string formatHint)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrWhiteSpace(formatHint))
            throw new ArgumentException("Format hint must not be null or empty.", nameof(formatHint));

        var extension = formatHint.TrimStart('.');
        var tempFile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"orbit3d_import_{Guid.NewGuid():N}.{extension}");

        try
        {
            using (var file = System.IO.File.Create(tempFile))
            {
                stream.CopyTo(file);
            }

            return Import(tempFile);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    private static Model3D ConvertScene(Scene scene)
    {
        if (!scene.HasMeshes())
            throw new InvalidOperationException("Imported scene contains no meshes.");

        var model = new Model3D();

        for (uint materialIndex = 0; materialIndex < scene.NumMaterials; materialIndex++)
        {
            var material = scene.GetMaterial(materialIndex);
            if (material is not null)
                model.Materials.Add(ConvertMaterial(material, model));
        }

        for (uint meshIndex = 0; meshIndex < scene.NumMeshes; meshIndex++)
        {
            var mesh = scene.GetMesh(meshIndex);
            if (mesh is not null)
                model.Meshes.Add(ConvertMesh(mesh));
        }

        var root = scene.GetRootNode();
        if (root is not null)
            ConvertNode(root, model.RootNode, model);

        return model;
    }

    private static Material3D ConvertMaterial(Material material, Model3D model)
    {
        var result = new Material3D();

        using (var name = material.GetName())
        {
            result.Name = name?.data ?? string.Empty;
        }

        if (material.GetTextureCount(TextureType.TextureType_DIFFUSE) > 0)
            result.BaseColorTexture = ReadTexture(
                material,
                TextureType.TextureType_DIFFUSE,
                TextureUsage.BaseColor,
                model);

        if (material.GetTextureCount(TextureType.TextureType_NORMALS) > 0)
            result.NormalTexture = ReadTexture(
                material,
                TextureType.TextureType_NORMALS,
                TextureUsage.Normal,
                model);

        if (material.GetTextureCount(TextureType.TextureType_GLTF_METALLIC_ROUGHNESS) > 0)
            result.MetallicRoughnessTexture = ReadTexture(
                material,
                TextureType.TextureType_GLTF_METALLIC_ROUGHNESS,
                TextureUsage.MetallicRoughness,
                model);

        return result;
    }

    private static Texture2D? ReadTexture(
        Material material,
        TextureType type,
        TextureUsage usage,
        Model3D model)
    {
        using var path = new Assimp.Maui.String();

        var result = global::Assimp.Maui.Assimp.GetMaterialTexture(
            material,
            type,
            0,
            path,
            null,
            null,
            null,
            null,
            null,
            null);

        if (result != Return.Return_SUCCESS || string.IsNullOrWhiteSpace(path.data))
            return null;

        var texture = new Texture2D
        {
            Name = System.IO.Path.GetFileName(path.data),
            FilePath = path.data,
            Usage = usage
        };

        model.Textures.Add(texture);
        return texture;
    }

    private static Mesh3D ConvertMesh(Mesh mesh)
    {
        var vertexCount = checked((int)mesh.NumVertices);
        var result = new Mesh3D
        {
            Name = mesh.GetName() ?? string.Empty,
            MaterialIndex = checked((int)mesh.GetMaterialIndex()),
            Topology = PrimitiveTopology.TriangleList,
            IndexFormat = IndexFormat.UInt32
        };

        if (mesh.HasPositions())
        {
            var positions = new Vector3[vertexCount];
            for (uint i = 0; i < mesh.NumVertices; i++)
            {
                positions[(int)i] = new Vector3(
                    mesh.GetVertexComponent(i, 0),
                    mesh.GetVertexComponent(i, 1),
                    mesh.GetVertexComponent(i, 2));
            }

            result.Positions = positions;
        }

        if (mesh.HasNormals())
        {
            var normals = new Vector3[vertexCount];
            for (uint i = 0; i < mesh.NumVertices; i++)
            {
                normals[(int)i] = new Vector3(
                    mesh.GetNormalComponent(i, 0),
                    mesh.GetNormalComponent(i, 1),
                    mesh.GetNormalComponent(i, 2));
            }

            result.Normals = normals;
        }

        if (mesh.HasTangentsAndBitangents())
        {
            var tangents = new Vector3[vertexCount];
            for (uint i = 0; i < mesh.NumVertices; i++)
            {
                tangents[(int)i] = new Vector3(
                    mesh.GetTangentComponent(i, 0),
                    mesh.GetTangentComponent(i, 1),
                    mesh.GetTangentComponent(i, 2));
            }

            result.Tangents = tangents;
        }

        if (mesh.HasTextureCoords(0))
        {
            var uvs = new Vector2[vertexCount];
            var components = Math.Min(mesh.GetTextureCoordinateComponentCount(0), 2u);

            for (uint i = 0; i < mesh.NumVertices; i++)
            {
                var u = mesh.GetTextureCoordinateComponent(0, i, 0);
                var v = components > 1
                    ? mesh.GetTextureCoordinateComponent(0, i, 1)
                    : 0f;

                uvs[(int)i] = new Vector2(u, v);
            }

            result.TextureCoordinates = uvs;
        }

        if (mesh.HasVertexColors(0))
        {
            var colors = new Vector4[vertexCount];
            for (uint i = 0; i < mesh.NumVertices; i++)
            {
                colors[(int)i] = new Vector4(
                    mesh.GetVertexColorComponent(0, i, 0),
                    mesh.GetVertexColorComponent(0, i, 1),
                    mesh.GetVertexColorComponent(0, i, 2),
                    mesh.GetVertexColorComponent(0, i, 3));
            }

            result.Colors = colors;
        }

        var faceCount = checked((int)mesh.NumFaces);
        var indices = new List<uint>(checked(faceCount * 3));

        for (uint face = 0; face < mesh.NumFaces; face++)
        {
            if (mesh.GetFaceIndexCount(face) != 3)
                continue;

            indices.Add(mesh.GetFaceIndex(face, 0));
            indices.Add(mesh.GetFaceIndex(face, 1));
            indices.Add(mesh.GetFaceIndex(face, 2));
        }

        result.Indices32 = indices.ToArray();
        return result;
    }

    private static void ConvertNode(Node node, Node3D node3D, Model3D model)
    {
        node3D.Name = node.GetName() ?? string.Empty;

        var matrix = new Matrix4x4(
            node.GetTransformationElement(0, 0), node.GetTransformationElement(0, 1),
            node.GetTransformationElement(0, 2), node.GetTransformationElement(0, 3),
            node.GetTransformationElement(1, 0), node.GetTransformationElement(1, 1),
            node.GetTransformationElement(1, 2), node.GetTransformationElement(1, 3),
            node.GetTransformationElement(2, 0), node.GetTransformationElement(2, 1),
            node.GetTransformationElement(2, 2), node.GetTransformationElement(2, 3),
            node.GetTransformationElement(3, 0), node.GetTransformationElement(3, 1),
            node.GetTransformationElement(3, 2), node.GetTransformationElement(3, 3));

        if (Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation))
        {
            node3D.Transform.Position = translation;
            node3D.Transform.Rotation = rotation;
            node3D.Transform.Scale = scale;
        }

        if (node.GetMeshCount() > 0)
            node3D.Model = model;

        for (uint childIndex = 0; childIndex < node.GetChildCount(); childIndex++)
        {
            var child = node.GetChild(childIndex);
            if (child is null)
                continue;

            var childNode = new Node3D();
            childNode.SetParent(node3D);
            ConvertNode(child, childNode, model);
        }
    }
}
#endif
