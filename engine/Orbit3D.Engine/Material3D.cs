using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Specifies the blending mode for a material.
/// </summary>
public enum BlendMode
{
    /// <summary>Opaque material.</summary>
    Opaque,
    /// <summary>Masked material.</summary>
    Mask,
    /// <summary>Blended material.</summary>
    Blend
}

/// <summary>
/// Represents a backend-neutral 3D material.
/// </summary>
public class Material3D
{
    /// <summary>
    /// Gets or sets the name of the material.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base or albedo color.
    /// </summary>
    public Vector4 BaseColor { get; set; } = Vector4.One;

    /// <summary>
    /// Gets or sets the base color (albedo) texture reference.
    /// </summary>
    public Texture2D? BaseColorTexture { get; set; }

    /// <summary>
    /// Gets or sets the normal map texture reference.
    /// </summary>
    public Texture2D? NormalTexture { get; set; }

    /// <summary>
    /// Gets or sets the metallic/roughness texture reference.
    /// </summary>
    public Texture2D? MetallicRoughnessTexture { get; set; }

    /// <summary>
    /// Gets or sets the metallic factor.
    /// </summary>
    public float MetallicFactor { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the roughness factor.
    /// </summary>
    public float RoughnessFactor { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the opacity (alpha) level.
    /// </summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the blend mode.
    /// </summary>
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    /// <summary>
    /// Gets or sets a value indicating whether backface culling is disabled (i.e. double-sided).
    /// </summary>
    public bool IsDoubleSided { get; set; } = false;
}
