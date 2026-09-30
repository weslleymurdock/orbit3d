using System.Numerics;

namespace Orbit3D.Engine;

/// <summary>
/// Specifies the type of a 3D light.
/// </summary>
public enum LightType
{
    /// <summary>Directional light.</summary>
    Directional,
    /// <summary>Point light.</summary>
    Point,
    /// <summary>Spot light.</summary>
    Spot
}

/// <summary>
/// Represents a backend-independent 3D light source.
/// </summary>
public class Light3D
{
    /// <summary>
    /// Gets or sets the name of the light.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the light.
    /// </summary>
    public LightType Type { get; set; } = LightType.Directional;

    /// <summary>
    /// Gets or sets the color of the light.
    /// </summary>
    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>
    /// Gets or sets the intensity of the light.
    /// </summary>
    public float Intensity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the position of the light (for Point and Spot lights).
    /// </summary>
    public Vector3 Position { get; set; } = Vector3.Zero;

    /// <summary>
    /// Gets or sets the direction of the light (for Directional and Spot lights).
    /// </summary>
    public Vector3 Direction { get; set; } = -Vector3.UnitZ;

    /// <summary>
    /// Gets or sets the range or radius of the light (for Point and Spot lights).
    /// </summary>
    public float Range { get; set; } = 100f;

    /// <summary>
    /// Gets or sets the inner cone angle in radians (for Spot lights).
    /// </summary>
    public float InnerConeAngle { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the outer cone angle in radians (for Spot lights).
    /// </summary>
    public float OuterConeAngle { get; set; } = MathF.PI / 4f;
}
