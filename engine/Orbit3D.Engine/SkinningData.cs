using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Represents CPU-side skeletal skinning influences for a <see cref="Mesh3D"/> (up to 4 bone influences per vertex).
/// </summary>
/// <remarks>
/// Normalization policy:
/// - Bone indices outside <c>[0, boneCount - 1]</c> or weights &lt;= 0 are discarded (zeroed).
/// - If a vertex has more than 4 influences, the 4 largest weights are retained.
/// - Remaining positive weights are normalized so their sum equals <c>1.0</c>.
/// - Vertices with zero total weight default to bone index 0 with weight 0 (or 1 if identity fallback is desired;
///   storing weight 0 keeps unskinned vertices at bind position when shaders handle zero-weight sum safely).
/// </remarks>
public sealed class SkinningData
{
    /// <summary>Maximum number of bone influences stored per vertex.</summary>
    public const int MaxInfluencesPerVertex = 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkinningData"/> class.
    /// </summary>
    public SkinningData(int vertexCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(vertexCount);
        VertexCount = vertexCount;
        BoneIndices = new int[checked(vertexCount * MaxInfluencesPerVertex)];
        BoneWeights = new Vector4[vertexCount];
        InfluenceCounts = new int[vertexCount];
    }

    /// <summary>Gets the number of vertices described by this skinning data.</summary>
    public int VertexCount { get; }

    /// <summary>Gets the flat array of bone indices (<see cref="VertexCount"/> * 4).</summary>
    public int[] BoneIndices { get; }

    /// <summary>Gets the per-vertex normalized weights (<c>X, Y, Z, W</c>).</summary>
    public Vector4[] BoneWeights { get; }

    /// <summary>Gets the number of active influences for each vertex (0..4).</summary>
    public int[] InfluenceCounts { get; }

    /// <summary>
    /// Adds a single bone influence to <paramref name="vertexIndex"/>, keeping up to 4 strongest influences.
    /// Call <see cref="ValidateAndNormalize(int)"/> after all influences have been added.
    /// </summary>
    public void AddInfluence(int vertexIndex, int boneIndex, float weight)
    {
        if ((uint)vertexIndex >= (uint)VertexCount)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex));
        if (boneIndex < 0 || !float.IsFinite(weight) || weight <= 0f)
            return;

        var baseOffset = vertexIndex * MaxInfluencesPerVertex;
        var count = InfluenceCounts[vertexIndex];
        var weights = BoneWeights[vertexIndex];

        if (count < MaxInfluencesPerVertex)
        {
            BoneIndices[baseOffset + count] = boneIndex;
            SetWeightComponent(ref weights, count, weight);
            BoneWeights[vertexIndex] = weights;
            InfluenceCounts[vertexIndex] = count + 1;
            return;
        }

        // Replace the smallest existing weight if the new weight is larger.
        var minSlot = 0;
        var minWeight = GetWeightComponent(weights, 0);
        for (var slot = 1; slot < MaxInfluencesPerVertex; slot++)
        {
            var candidate = GetWeightComponent(weights, slot);
            if (candidate < minWeight)
            {
                minWeight = candidate;
                minSlot = slot;
            }
        }

        if (weight > minWeight)
        {
            BoneIndices[baseOffset + minSlot] = boneIndex;
            SetWeightComponent(ref weights, minSlot, weight);
            BoneWeights[vertexIndex] = weights;
        }
    }

    /// <summary>
    /// Validates bone indices against <paramref name="boneCount"/> and normalizes per-vertex weights.
    /// </summary>
    public void ValidateAndNormalize(int boneCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(boneCount);

        for (var v = 0; v < VertexCount; v++)
        {
            var baseOffset = v * MaxInfluencesPerVertex;
            var weights = BoneWeights[v];
            var activeCount = Math.Clamp(InfluenceCounts[v], 0, MaxInfluencesPerVertex);

            float sum = 0f;
            var validCount = 0;

            for (var s = 0; s < activeCount; s++)
            {
                var boneIdx = BoneIndices[baseOffset + s];
                var w = GetWeightComponent(weights, s);
                if (boneIdx < 0 || boneIdx >= boneCount || !float.IsFinite(w) || w <= 0f)
                {
                    BoneIndices[baseOffset + s] = 0;
                    SetWeightComponent(ref weights, s, 0f);
                }
                else
                {
                    sum += w;
                    validCount++;
                }
            }

            for (var s = activeCount; s < MaxInfluencesPerVertex; s++)
            {
                BoneIndices[baseOffset + s] = 0;
                SetWeightComponent(ref weights, s, 0f);
            }

            if (sum > 1e-6f)
            {
                var invSum = 1f / sum;
                weights *= invSum;
            }
            else
            {
                weights = Vector4.Zero;
                validCount = 0;
            }

            BoneWeights[v] = weights;
            InfluenceCounts[v] = validCount;
        }
    }

    /// <summary>
    /// Validates that all bone indices are within <paramref name="boneCount"/> and weights are finite and non-negative.
    /// </summary>
    public bool IsValid(int boneCount)
    {
        if (BoneIndices.Length != VertexCount * MaxInfluencesPerVertex)
            return false;
        if (BoneWeights.Length != VertexCount || InfluenceCounts.Length != VertexCount)
            return false;

        for (var v = 0; v < VertexCount; v++)
        {
            var count = InfluenceCounts[v];
            if (count < 0 || count > MaxInfluencesPerVertex)
                return false;

            var baseOffset = v * MaxInfluencesPerVertex;
            var w = BoneWeights[v];
            for (var s = 0; s < MaxInfluencesPerVertex; s++)
            {
                var boneIdx = BoneIndices[baseOffset + s];
                if (boneIdx < 0 || (boneCount > 0 && boneIdx >= boneCount))
                    return false;

                var weight = GetWeightComponent(w, s);
                if (!float.IsFinite(weight) || weight < 0f)
                    return false;
            }
        }

        return true;
    }

    private static float GetWeightComponent(Vector4 v, int index) => index switch
    {
        0 => v.X,
        1 => v.Y,
        2 => v.Z,
        3 => v.W,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    private static void SetWeightComponent(ref Vector4 v, int index, float value)
    {
        switch (index)
        {
            case 0: v.X = value; break;
            case 1: v.Y = value; break;
            case 2: v.Z = value; break;
            case 3: v.W = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
