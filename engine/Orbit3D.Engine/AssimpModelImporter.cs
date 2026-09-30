#if ANDROID || IOS || MACCATALYST || WINDOWS
using System.Collections.Generic;
using System.Numerics;
using Assimp.Maui;
using Orbit3D.Engine;
using System.IO;
using System.Collections.Generic;
using SysIO = System.IO;

namespace Orbit3D.Engine;

/// <summary>
/// Implements IModelImporter using Assimp.MAUI to load 3D assets.
/// Implements <see cref="IModelImporter"/> using Assimp.MAUI to load 3D assets at runtime.
/// All Assimp/native types are fully converted before this method returns;
/// no Assimp objects escape the import boundary.
/// </summary>
public sealed class AssimpModelImporter : IModelImporter
{
    // Deliberate post-process selection:
    //   Triangulate               – ensure all faces are triangles
    //   JoinIdenticalVertices     – reduce vertex count
    //   GenNormals                – create missing normals
    //   CalcTangentSpace          – create tangents when UVs are present
    //   FlipUVs                   – flip V to match D3D/Metal/Vulkan convention
    private const uint DefaultFlags =
        (uint)PostProcessSteps.Process_Triangulate |
        (uint)PostProcessSteps.Process_JoinIdenticalVertices |
        (uint)PostProcessSteps.Process_GenNormals |
        (uint)PostProcessSteps.Process_CalcTangentSpace |
        (uint)PostProcessSteps.Process_FlipUVs;

    /// <inheritdoc />
    public Model3D Import(string filePath)
    {
        uint postProcessSteps = (uint)(PostProcessSteps.aiProcess_Triangulate |
                                       PostProcessSteps.aiProcess_JoinIdenticalVertices |
                                       PostProcessSteps.aiProcess_GenNormals |
                                       PostProcessSteps.aiProcess_CalcTangentSpace |
                                       PostProcessSteps.aiProcess_FlipUVs);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path must not be null or empty.", nameof(filePath));
        if (!SysIO.File.Exists(filePath))
            throw new SysIO.FileNotFoundException("Asset file not found.", filePath);

        var scene = Assimp.Maui.Assimp.ImportFile(filePath, postProcessSteps);
        
        var scene = Assimp.Maui.Assimp.ImportFile(filePath, DefaultFlags);
        if (scene == null)
            throw new Exception($"Failed to import file: {filePath}");
            throw new InvalidOperationException($"Assimp failed to import '{filePath}'.");

        return ConvertScene(scene);
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
    public Model3D Import(System.IO.Stream stream, string formatHint)
    {
        var tempFile = Path.GetTempFileName() + "." + formatHint;
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (string.IsNullOrWhiteSpace(formatHint))
            throw new ArgumentException("Format hint must not be null or empty.", nameof(formatHint));

        string ext = formatHint.TrimStart('.').ToLowerInvariant();
        string tempFile = SysIO.Path.Combine(SysIO.Path.GetTempPath(), $"orbit3d_import_{System.Guid.NewGuid():N}.{ext}");
        try
        {
            using (var fileStream = File.Create(tempFile))
            {
                stream.CopyTo(fileStream);
            }
            using (var fs = SysIO.File.Create(tempFile))
                stream.CopyTo(fs);

            return Import(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            if (SysIO.File.Exists(tempFile))
                SysIO.File.Delete(tempFile);
        }
    }

    // ── Private conversion ───────────────────────────────────────────────────
    private Model3D ConvertScene(Scene scene)
    {
        if (scene == null || !scene.HasMeshes())
        {
            throw new Exception("Scene is null or has no meshes.");
        }
        if (!scene.HasMeshes())
            throw new InvalidOperationException("Imported scene contains no meshes.");

        var model = new Model3D();

        // Convert Materials
        if (scene.HasMaterials())
        // 1. Materials (converted first – meshes reference them by index)
        for (uint i = 0; i < scene.NumMaterials; i++)
        {
            for (uint i = 0; i < scene.NumMaterials; i++)
            {
                var mat = scene.GetMaterial(i);
                model.Materials.Add(ConvertMaterial(mat, model));
            }
            using var mat = scene.GetMaterial(i);
            model.Materials.Add(ConvertMaterial(mat, model));
        }

        // Convert Meshes
        // 2. Meshes
        for (uint i = 0; i < scene.NumMeshes; i++)
        {
            var mesh = scene.GetMesh(i);
            using var mesh = scene.GetMesh(i);
            model.Meshes.Add(ConvertMesh(mesh));
        }

        // Convert Hierarchy
        var root = scene.GetRootNode();
        if (root != null)
        {
            ConvertNode(root, model.RootNode, model);
        }
        // 3. Node hierarchy
        var rootNode = scene.GetRootNode();
        if (rootNode != null)
            ConvertNode(rootNode, model.RootNode);

        return model;
    }

    private Material3D ConvertMaterial(Material mat, Model3D model)
    {
        var nameStr = mat.GetName();
        var mat3D = new Material3D
        {
            Name = mat.GetName()?.data ?? string.Empty
        };
        nameStr?.Dispose();

        // Note: Assimp.MAUI SWIG wrappers for getting properties are complex,
        // we'll fetch diffuse texture as an example for now.
        uint diffuseCount = mat.GetTextureCount(TextureType.aiTextureType_DIFFUSE);
        if (diffuseCount > 0)
        // Diffuse / base-color texture
        ReadTexture(mat, TextureType.TextureType_DIFFUSE, TextureUsage.BaseColor, model, out var baseTex);
        mat3D.BaseColorTexture = baseTex;

        // Normal-map texture
        ReadTexture(mat, TextureType.TextureType_NORMALS, TextureUsage.Normal, model, out var normalTex);
        mat3D.NormalTexture = normalTex;

        return mat3D;
    }

    private static void ReadTexture(
        Material mat,
        TextureType texType,
        TextureUsage usage,
        Model3D model,
        out Texture2D? result)
    {
        result = null;
        if (mat.GetTextureCount(texType) == 0) return;

        var pathStr = new Assimp.Maui.String();
        try
        {
            var pathStr = new Assimp.Maui.String();
            // Simplified texture fetching; ideally we pass mapping, etc., but they might be null-able.
            Assimp.Maui.Assimp.GetMaterialTexture(mat, TextureType.aiTextureType_DIFFUSE, 0, pathStr, null, null, null, null, null, null);
            
            if (!string.IsNullOrEmpty(pathStr.data))
            var ret = Assimp.Maui.Assimp.GetMaterialTexture(
                mat, texType, 0, pathStr, null, null, null, null, null, null);

            if (ret == Return.Return_SUCCESS && !string.IsNullOrEmpty(pathStr.data))
            {
                var tex = new Texture2D
                {
                    Name = SysIO.Path.GetFileName(pathStr.data),
                    FilePath = pathStr.data,
                    Usage = TextureUsage.BaseColor,
                    Usage = usage
                };
                mat3D.BaseColorTexture = tex;
                result = tex;
                model.Textures.Add(tex);
            }
        }
        finally
        {
            pathStr.Dispose();
        }
        return mat3D;
    }

    private Mesh3D ConvertMesh(Mesh mesh)
    {
        var mesh3D = new Mesh3D
        {
            Name = mesh.GetName() ?? string.Empty,
            MaterialIndex = (int)mesh.GetMaterialIndex()
        };

        uint numVertices = mesh.NumVertices;
        uint n = mesh.NumVertices;

        if (mesh.HasPositions())
        {
            var positions = new Vector3[numVertices];
            for (uint i = 0; i < numVertices; i++)
                positions[i] = new Vector3(mesh.GetVertexComponent(i, 0), mesh.GetVertexComponent(i, 1), mesh.GetVertexComponent(i, 2));
            mesh3D.Positions = positions;
            var buf = new Vector3[n];
            for (uint i = 0; i < n; i++)
                buf[i] = new Vector3(
                    mesh.GetVertexComponent(i, 0),
                    mesh.GetVertexComponent(i, 1),
                    mesh.GetVertexComponent(i, 2));
            mesh3D.Positions = buf;
        }

        if (mesh.HasNormals())
        {
            var normals = new Vector3[numVertices];
            for (uint i = 0; i < numVertices; i++)
                normals[i] = new Vector3(mesh.GetNormalComponent(i, 0), mesh.GetNormalComponent(i, 1), mesh.GetNormalComponent(i, 2));
            mesh3D.Normals = normals;
            var buf = new Vector3[n];
            for (uint i = 0; i < n; i++)
                buf[i] = new Vector3(
                    mesh.GetNormalComponent(i, 0),
                    mesh.GetNormalComponent(i, 1),
                    mesh.GetNormalComponent(i, 2));
            mesh3D.Normals = buf;
        }

        if (mesh.HasTangentsAndBitangents())
        {
            var tangents = new Vector3[numVertices];
            for (uint i = 0; i < numVertices; i++)
                tangents[i] = new Vector3(mesh.GetTangentComponent(i, 0), mesh.GetTangentComponent(i, 1), mesh.GetTangentComponent(i, 2));
            mesh3D.Tangents = tangents;
            var buf = new Vector3[n];
            for (uint i = 0; i < n; i++)
                buf[i] = new Vector3(
                    mesh.GetTangentComponent(i, 0),
                    mesh.GetTangentComponent(i, 1),
                    mesh.GetTangentComponent(i, 2));
            mesh3D.Tangents = buf;
        }

        if (mesh.HasTextureCoords(0))
        {
            var uvs = new Vector2[numVertices];
            for (uint i = 0; i < numVertices; i++)
                uvs[i] = new Vector2(mesh.GetTextureCoordinateComponent(0, i, 0), mesh.GetTextureCoordinateComponent(0, i, 1));
            mesh3D.TextureCoordinates = uvs;
            var buf = new Vector2[n];
            for (uint i = 0; i < n; i++)
                buf[i] = new Vector2(
                    mesh.GetTextureCoordinateComponent(0, i, 0),
                    mesh.GetTextureCoordinateComponent(0, i, 1));
            mesh3D.TextureCoordinates = buf;
        }

        if (mesh.HasVertexColors(0))
        {
            var buf = new Vector4[n];
            for (uint i = 0; i < n; i++)
                buf[i] = new Vector4(
                    mesh.GetVertexColorComponent(0, i, 0),
                    mesh.GetVertexColorComponent(0, i, 1),
                    mesh.GetVertexColorComponent(0, i, 2),
                    mesh.GetVertexColorComponent(0, i, 3));
            mesh3D.Colors = buf;
        }

        if (mesh.HasFaces())
        {
            var indices = new List<uint>();
            uint numFaces = mesh.NumFaces;
            for (uint i = 0; i < numFaces; i++)
            var indices = new List<uint>((int)(numFaces * 3));
            for (uint f = 0; f < numFaces; f++)
            {
                if (mesh.GetFaceIndexCount(i) == 3)
                if (mesh.GetFaceIndexCount(f) == 3)
                {
                    indices.Add(mesh.GetFaceIndex(i, 0));
                    indices.Add(mesh.GetFaceIndex(i, 1));
                    indices.Add(mesh.GetFaceIndex(i, 2));
                    indices.Add(mesh.GetFaceIndex(f, 0));
                    indices.Add(mesh.GetFaceIndex(f, 1));
                    indices.Add(mesh.GetFaceIndex(f, 2));
                }
            }
            mesh3D.Indices32 = indices.ToArray();
            mesh3D.IndexFormat = IndexFormat.UInt32;
        }

        return mesh3D;
    }

    private void ConvertNode(Node node, Node3D node3D, Model3D model)
    {
        node3D.Name = node.GetName()?.data ?? string.Empty;
        
        // Convert matrix
        // We need to fetch matrix components from the SWIG node if possible
        // Actually for now, let's just initialize it to Identity to avoid SWIG pointer complexity,
        // or check how node matrix is exposed.
        node3D.Transform.Position = Vector3.Zero;
        node3D.Transform.Rotation = Quaternion.Identity;
        node3D.Transform.Scale = Vector3.One;
        node3D.Name = node.GetName() ?? string.Empty;

        uint numChildren = node.NumChildren;
        for (uint i = 0; i < numChildren; i++)
        // GetTransformationElement(row, col) returns the logical (row, col) value
        // from the node's 4x4 local-to-parent matrix (column-major in Assimp, but the
        // helper exposes it row-by-row matching System.Numerics row-major convention).
        var m = new Matrix4x4(
            node.GetTransformationElement(0, 0), node.GetTransformationElement(0, 1),
            node.GetTransformationElement(0, 2), node.GetTransformationElement(0, 3),
            node.GetTransformationElement(1, 0), node.GetTransformationElement(1, 1),
            node.GetTransformationElement(1, 2), node.GetTransformationElement(1, 3),
            node.GetTransformationElement(2, 0), node.GetTransformationElement(2, 1),
            node.GetTransformationElement(2, 2), node.GetTransformationElement(2, 3),
            node.GetTransformationElement(3, 0), node.GetTransformationElement(3, 1),
            node.GetTransformationElement(3, 2), node.GetTransformationElement(3, 3));

        if (Matrix4x4.Decompose(m, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
        {
            node3D.Transform.Position = translation;
            node3D.Transform.Rotation = rotation;
            node3D.Transform.Scale = scale;
        }

        uint childCount = node.GetChildCount();
        for (uint i = 0; i < childCount; i++)
        {
            var child = node.GetChild(i);
            if (child != null)
            {
                var childNode3D = new Node3D();
                childNode3D.SetParent(node3D);
                ConvertNode(child, childNode3D, model);
            }
            if (child == null) continue;
            var childNode3D = new Node3D();
            childNode3D.SetParent(node3D);
            ConvertNode(child, childNode3D);
        }
    }
}
#endif
