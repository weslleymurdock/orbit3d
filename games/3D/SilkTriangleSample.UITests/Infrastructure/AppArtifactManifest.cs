namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Record of application build artifact metadata used to ensure freshness and determinism.
/// </summary>
public sealed record AppArtifactManifest(
    PlatformKind Platform,
    string Configuration,
    string TargetFramework,
    string ArtifactPath,
    string ArtifactType,
    long FileSizeBytes,
    string ContentHash,
    DateTime BuildTimestampUtc,
    string? CommitSha = null
);

