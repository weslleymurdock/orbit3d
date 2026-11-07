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
    string? CommitSha = null,
    string? ApplicationId = null,
    string? RuntimeIdentifier = null
);

/// <summary>
/// Metadata extracted from a packaged iOS .ipa archive.
/// </summary>
public sealed record IosIpaPackageMetadata(
    string IpaPath,
    string AppBundleName,
    string BundleIdentifier,
    string? ExecutableName,
    string? MinimumOsVersion,
    bool HasProvisioningProfile,
    long UncompressedExecutableSizeBytes,
    string Sha256Hash
);
