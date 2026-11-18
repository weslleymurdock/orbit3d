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
            using var material = scene.GetMaterial(materialIndex);
            if (material is not null)
                model.Materials.Add(ConvertMaterial(material, model));
        }

        var hasBones = false;
        for (uint meshIndex = 0; meshIndex < scene.NumMeshes; meshIndex++)
        {
            using var mesh = scene.GetMesh(meshIndex);
            if (mesh is not null)
            {
                if (mesh.HasBones())
                    hasBones = true;
                model.Meshes.Add(ConvertMesh(mesh));
            }
        }

        using var root = scene.GetRootNode();
        if (root is not null)
        {
            ConvertNode(root, model.RootNode, model);

            if (hasBones || scene.HasAnimations())
                ConvertSkeletonAndSkinning(scene, root, model);
        }

        if (scene.HasAnimations())
            ConvertAnimations(scene, model);

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
            using var child = node.GetChild(childIndex);
            if (child is null)
                continue;

            var childNode = new Node3D();
            childNode.SetParent(node3D);
            ConvertNode(child, childNode, model);
        }
    }

    private static void ConvertSkeletonAndSkinning(Scene scene, Node root, Model3D model)
    {
        var skeletonNodes = new List<SkeletonNode>();
        var nodeNameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        FlattenSkeletonNodes(root, -1, skeletonNodes, nodeNameToIndex);

        if (skeletonNodes.Count == 0)
            return;

        Matrix4x4.Invert(skeletonNodes[0].BindLocalTransform, out var globalInverse);
        if (float.IsNaN(globalInverse.M11))
            globalInverse = Matrix4x4.Identity;

        var bones = new List<Bone3D>();
        var boneNameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        for (uint meshIndex = 0; meshIndex < scene.NumMeshes && (int)meshIndex < model.Meshes.Count; meshIndex++)
        {
            using var assimpMesh = scene.GetMesh(meshIndex);
            if (assimpMesh is null || !assimpMesh.HasBones())
                continue;

            var orbitMesh = model.Meshes[(int)meshIndex];
            var vertexCount = orbitMesh.Positions?.Length ?? 0;
            if (vertexCount <= 0)
                continue;

            var skinning = new SkinningData(vertexCount);
            for (uint boneIdx = 0; boneIdx < assimpMesh.NumBones; boneIdx++)
            {
                using var assimpBone = assimpMesh.GetBone(boneIdx);
                if (assimpBone is null)
                    continue;

                var boneName = assimpBone.GetName() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(boneName))
                    continue;

                if (!boneNameToIndex.TryGetValue(boneName, out var globalBoneIndex))
                {
                    if (!nodeNameToIndex.TryGetValue(boneName, out var nodeIndex))
                        nodeIndex = 0;

                    var offsetMatrix = ReadBoneOffsetMatrixTransposed(assimpBone);
                    var bindLocal = skeletonNodes[nodeIndex].BindLocalTransform;
                    globalBoneIndex = bones.Count;
                    boneNameToIndex.Add(boneName, globalBoneIndex);
                    bones.Add(new Bone3D(globalBoneIndex, boneName, nodeIndex, -1, offsetMatrix, bindLocal));
                }

                var weightCount = assimpBone.GetWeightCount();
                for (uint wi = 0; wi < weightCount; wi++)
                {
                    var vertexId = checked((int)assimpBone.GetWeightVertexId(wi));
                    var weight = assimpBone.GetWeight(wi);
                    if ((uint)vertexId < (uint)vertexCount)
                        skinning.AddInfluence(vertexId, globalBoneIndex, weight);
                }
            }

            orbitMesh.Skinning = skinning;
        }

        // Resolve ParentBoneIndex by walking up SkeletonNode.ParentIndex until a node corresponds to a Bone3D.
        var nodeIndexToBoneIndex = new Dictionary<int, int>(bones.Count);
        for (var i = 0; i < bones.Count; i++)
            nodeIndexToBoneIndex.TryAdd(bones[i].NodeIndex, i);

        for (var i = 0; i < bones.Count; i++)
        {
            var bone = bones[i];
            var parentBoneIndex = -1;
            var parentNodeIdx = skeletonNodes[bone.NodeIndex].ParentIndex;
            while (parentNodeIdx >= 0)
            {
                if (nodeIndexToBoneIndex.TryGetValue(parentNodeIdx, out var candidateBone))
                {
                    parentBoneIndex = candidateBone;
                    break;
                }
                parentNodeIdx = skeletonNodes[parentNodeIdx].ParentIndex;
            }

            if (parentBoneIndex != bone.ParentBoneIndex)
            {
                bones[i] = new Bone3D(
                    bone.Index,
                    bone.Name,
                    bone.NodeIndex,
                    parentBoneIndex,
                    bone.InverseBindMatrix,
                    bone.BindLocalTransform);
            }
        }

        var boneCount = bones.Count;
        foreach (var mesh in model.Meshes)
            mesh.Skinning?.ValidateAndNormalize(boneCount);

        model.Skeleton = new Skeleton3D(root.GetName() ?? "Skeleton", skeletonNodes, bones, globalInverse);
    }

    private static void FlattenSkeletonNodes(
        Node node,
        int parentIndex,
        List<SkeletonNode> nodes,
        Dictionary<string, int> nameToIndex)
    {
        var index = nodes.Count;
        var name = node.GetName() ?? string.Empty;
        var localMatrix = ReadNodeTransformTransposed(node);

        if (!Matrix4x4.Decompose(localMatrix, out var scale, out var rotation, out var position))
        {
            scale = Vector3.One;
            rotation = Quaternion.Identity;
            position = new Vector3(localMatrix.M41, localMatrix.M42, localMatrix.M43);
        }

        nodes.Add(new SkeletonNode(index, name, parentIndex, localMatrix, position, Quaternion.Normalize(rotation), scale));
        if (!string.IsNullOrEmpty(name))
            nameToIndex.TryAdd(name, index);

        for (uint childIndex = 0; childIndex < node.GetChildCount(); childIndex++)
        {
            using var child = node.GetChild(childIndex);
            if (child is not null)
                FlattenSkeletonNodes(child, index, nodes, nameToIndex);
        }
    }

    private static void ConvertAnimations(Scene scene, Model3D model)
    {
        for (uint animIndex = 0; animIndex < scene.NumAnimations; animIndex++)
        {
            using var anim = scene.GetAnimation(animIndex);
            if (anim is null)
                continue;

            var name = anim.GetName();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Animation_{animIndex}";

            var duration = anim.GetDuration();
            var ticksPerSecond = anim.GetTicksPerSecond();
            var channels = new List<AnimationChannel3D>(checked((int)anim.NumChannels));

            for (uint chIndex = 0; chIndex < anim.NumChannels; chIndex++)
            {
                using var ch = anim.GetChannel(chIndex);
                if (ch is null)
                    continue;

                var nodeName = ch.GetNodeName();
                if (string.IsNullOrWhiteSpace(nodeName))
                    continue;

                var posCount = checked((int)ch.GetPositionKeyCount());
                var posKeys = new VectorKeyframe[posCount];
                for (uint i = 0; i < (uint)posCount; i++)
                {
                    posKeys[i] = new VectorKeyframe(
                        ch.GetPositionKeyTime(i),
                        new Vector3(
                            ch.GetPositionKeyComponent(i, 0),
                            ch.GetPositionKeyComponent(i, 1),
                            ch.GetPositionKeyComponent(i, 2)));
                }

                var rotCount = checked((int)ch.GetRotationKeyCount());
                var rotKeys = new QuaternionKeyframe[rotCount];
                for (uint i = 0; i < (uint)rotCount; i++)
                {
                    // Assimp aiQuaternion component order is [0]=w, [1]=x, [2]=y, [3]=z.
                    var w = ch.GetRotationKeyComponent(i, 0);
                    var x = ch.GetRotationKeyComponent(i, 1);
                    var y = ch.GetRotationKeyComponent(i, 2);
                    var z = ch.GetRotationKeyComponent(i, 3);
                    rotKeys[i] = new QuaternionKeyframe(
                        ch.GetRotationKeyTime(i),
                        Quaternion.Normalize(new Quaternion(x, y, z, w)));
                }

                var scaleCount = checked((int)ch.GetScalingKeyCount());
                var scaleKeys = new VectorKeyframe[scaleCount];
                for (uint i = 0; i < (uint)scaleCount; i++)
                {
                    scaleKeys[i] = new VectorKeyframe(
                        ch.GetScalingKeyTime(i),
                        new Vector3(
                            ch.GetScalingKeyComponent(i, 0),
                            ch.GetScalingKeyComponent(i, 1),
                            ch.GetScalingKeyComponent(i, 2)));
                }

                channels.Add(new AnimationChannel3D(nodeName, posKeys, rotKeys, scaleKeys));
            }

            model.Animations.Add(new AnimationClip3D(name, duration, ticksPerSecond, channels));
        }
    }

    private static Matrix4x4 ReadNodeTransformTransposed(Node node) => new(
        node.GetTransformationElement(0, 0), node.GetTransformationElement(1, 0),
        node.GetTransformationElement(2, 0), node.GetTransformationElement(3, 0),
        node.GetTransformationElement(0, 1), node.GetTransformationElement(1, 1),
        node.GetTransformationElement(2, 1), node.GetTransformationElement(3, 1),
        node.GetTransformationElement(0, 2), node.GetTransformationElement(1, 2),
        node.GetTransformationElement(2, 2), node.GetTransformationElement(3, 2),
        node.GetTransformationElement(0, 3), node.GetTransformationElement(1, 3),
        node.GetTransformationElement(2, 3), node.GetTransformationElement(3, 3));

    private static Matrix4x4 ReadBoneOffsetMatrixTransposed(Bone bone) => new(
        bone.GetOffsetMatrixElement(0, 0), bone.GetOffsetMatrixElement(1, 0),
        bone.GetOffsetMatrixElement(2, 0), bone.GetOffsetMatrixElement(3, 0),
        bone.GetOffsetMatrixElement(0, 1), bone.GetOffsetMatrixElement(1, 1),
        bone.GetOffsetMatrixElement(2, 1), bone.GetOffsetMatrixElement(3, 1),
        bone.GetOffsetMatrixElement(0, 2), bone.GetOffsetMatrixElement(1, 2),
        bone.GetOffsetMatrixElement(2, 2), bone.GetOffsetMatrixElement(3, 2),
        bone.GetOffsetMatrixElement(0, 3), bone.GetOffsetMatrixElement(1, 3),
        bone.GetOffsetMatrixElement(2, 3), bone.GetOffsetMatrixElement(3, 3));
}
#endif
