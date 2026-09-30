using System.Collections.Concurrent;

namespace Orbit3D.Engine;

/// <summary>
/// Provides a stable identity for asset import operations that influence converted CPU data.
/// </summary>
public sealed record AssetImportSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the import should triangulate faces.
    /// </summary>
    public bool Triangulate { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether identical vertices should be joined.
    /// </summary>
    public bool JoinIdenticalVertices { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether normals should be generated when absent.
    /// </summary>
    public bool GenerateNormals { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether tangent space should be calculated.
    /// </summary>
    public bool CalculateTangentSpace { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether UV orientation should be flipped.
    /// </summary>
    public bool FlipUVs { get; init; } = true;

    /// <summary>
    /// Gets or sets a format hint used during reconstruction of or import of the asset.
    /// </summary>
    public string? FormatHint { get; init; }

    /// <summary>
    /// Creates a cache identity string that is stable across equivalent import settings.
    /// </summary>
    public string ToCacheKeySuffix()
    {
        return string.Join("|", new[]
        {
            Triangulate.ToString(),
            JoinIdenticalVertices.ToString(),
            GenerateNormals.ToString(),
            CalculateTangentSpace.ToString(),
            FlipUVs.ToString(),
            FormatHint ?? string.Empty
        });
    }
}

/// <summary>
/// Represents the stable key under which imported models are cached.
/// </summary>
public sealed record AssetCacheKey(string AssetPath, AssetImportSettings? Settings)
{
    /// <summary>
    /// Returns the canonical cache key.
    /// </summary>
    public override string ToString()
    {
        var path = string.IsNullOrWhiteSpace(AssetPath) ? string.Empty : AssetPath.Trim();
        var suffix = Settings is null ? "default" : Settings.ToCacheKeySuffix();
        return $"{path}:{suffix}";
    }
}

/// <summary>
/// Caches imported CPU-side models and avoids duplicate imports for equivalent assets.
/// </summary>
public interface IAssetCache
{
    /// <summary>
    /// Attempts to get a cached model using the supplied asset identity.
    /// </summary>
    bool TryGet(string assetPath, AssetImportSettings? settings, out Model3D model);

    /// <summary>
    /// Gets or imports a model for the specified asset path.
    /// </summary>
    Model3D GetOrAdd(string assetPath, Func<Model3D> importer, AssetImportSettings? settings = null);

    /// <summary>
    /// Gets or imports a model asynchronously, allowing import work to remain off the render thread.
    /// </summary>
    Task<Model3D> GetOrAddAsync(string assetPath, Func<Task<Model3D>> importer, AssetImportSettings? settings = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached asset if present.
    /// </summary>
    bool Remove(string assetPath, AssetImportSettings? settings = null);

    /// <summary>
    /// Clears the cache.
    /// </summary>
    void Clear();
}

/// <summary>
/// Default asset cache implementation for CPU-side model data.
/// </summary>
public sealed class ModelAssetCache : IAssetCache
{
    private readonly ConcurrentDictionary<string, Lazy<Model3D>> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task<Model3D>>> asyncCache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool TryGet(string assetPath, AssetImportSettings? settings, out Model3D model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        var key = CreateKey(assetPath, settings);
        if (cache.TryGetValue(key, out var entry))
        {
            model = entry.Value;
            return true;
        }

        if (asyncCache.TryGetValue(key, out var asyncEntry))
        {
            try
            {
                model = asyncEntry.Value.GetAwaiter().GetResult();
                var resolved = model;
                cache.TryAdd(key, new Lazy<Model3D>(() => resolved, LazyThreadSafetyMode.ExecutionAndPublication));
                return true;
            }
            catch
            {
                asyncCache.TryRemove(key, out _);
                model = null!;
                return false;
            }
        }

        model = null!;
        return false;
    }

    /// <inheritdoc />
    public Model3D GetOrAdd(string assetPath, Func<Model3D> importer, AssetImportSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        ArgumentNullException.ThrowIfNull(importer);

        var key = CreateKey(assetPath, settings);

        if (cache.TryGetValue(key, out var cached))
            return cached.Value;

        if (asyncCache.TryGetValue(key, out var pending))
            return pending.Value.GetAwaiter().GetResult();

        var lazy = cache.GetOrAdd(key, _ => new Lazy<Model3D>(importer, LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    /// <inheritdoc />
    public async Task<Model3D> GetOrAddAsync(string assetPath, Func<Task<Model3D>> importer, AssetImportSettings? settings = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        ArgumentNullException.ThrowIfNull(importer);

        var key = CreateKey(assetPath, settings);

        if (cache.TryGetValue(key, out var cached))
            return cached.Value;

        if (asyncCache.TryGetValue(key, out var existing))
            return await existing.Value.WaitAsync(cancellationToken).ConfigureAwait(false);

        var taskFactory = new Lazy<Task<Model3D>>(() => importer(), LazyThreadSafetyMode.ExecutionAndPublication);
        var created = asyncCache.GetOrAdd(key, _ => taskFactory);

        try
        {
            var model = await created.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            cache.TryAdd(key, new Lazy<Model3D>(() => model, LazyThreadSafetyMode.ExecutionAndPublication));
            return model;
        }
        catch
        {
            asyncCache.TryRemove(key, out _);
            throw;
        }
    }

    /// <inheritdoc />
    public bool Remove(string assetPath, AssetImportSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        var key = CreateKey(assetPath, settings);
        var removed = cache.TryRemove(key, out _);
        asyncCache.TryRemove(key, out _);
        return removed;
    }

    /// <inheritdoc />
    public void Clear()
    {
        cache.Clear();
        asyncCache.Clear();
    }

    /// <summary>
    /// Gets the canonical cache key for a supplied asset path and import settings.
    /// </summary>
    public static string CreateKey(string assetPath, AssetImportSettings? settings = null)
    {
        return new AssetCacheKey(assetPath, settings).ToString();
    }
}
