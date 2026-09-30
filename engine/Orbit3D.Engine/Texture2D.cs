namespace Orbit3D.Engine;

/// <summary>
/// Specifies the usage or type of a texture.
/// </summary>
public enum TextureUsage
{
    /// <summary>Unknown usage.</summary>
    Unknown,
    /// <summary>Base color usage.</summary>
    BaseColor,
    /// <summary>Normal map usage.</summary>
    Normal,
    /// <summary>Metallic roughness usage.</summary>
    MetallicRoughness,
    /// <summary>Emissive usage.</summary>
    Emissive
}

/// <summary>
/// Represents a backend-neutral 2D texture.
/// </summary>
public class Texture2D
{
    /// <summary>
    /// Gets or sets the name of the texture.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the intended usage of the texture.
    /// </summary>
    public TextureUsage Usage { get; set; } = TextureUsage.Unknown;

    /// <summary>
    /// Gets or sets the file path to the texture, if loaded from disk.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the raw image data, if embedded or loaded into memory.
    /// </summary>
    public byte[]? Data { get; set; }
}
