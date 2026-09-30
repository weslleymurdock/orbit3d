namespace Orbit3D.Engine;

/// <summary>
/// Defines an interface for importing 3D models from streams or files.
/// </summary>
public interface IModelImporter
{
    /// <summary>
    /// Imports a model from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the model file.</param>
    /// <returns>The imported 3D model.</returns>
    Model3D Import(string filePath);

    /// <summary>
    /// Imports a model from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the model data.</param>
    /// <param name="formatHint">A hint about the format of the model data (e.g., "obj", "gltf").</param>
    /// <returns>The imported 3D model.</returns>
    Model3D Import(Stream stream, string formatHint);
}
