using Orbit3D.Engine.Graphics;

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
    /// Gets or sets the texture width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the texture height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the texture format.
    /// </summary>
    public TextureFormat PixelFormat { get; set; } = TextureFormat.Rgba8Unorm;

    /// <summary>
    /// Gets or sets the file path to the texture, if loaded from disk.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the raw image data, if embedded or loaded into memory.
    /// </summary>
    public byte[]? Data { get; set; }

    /// <summary>
    /// Gets a value indicating whether the texture contains valid pixel data ready for GPU upload.
    /// </summary>
    public bool HasPixelData => Width > 0 && Height > 0 && Data is not null && Data.Length > 0;
}
