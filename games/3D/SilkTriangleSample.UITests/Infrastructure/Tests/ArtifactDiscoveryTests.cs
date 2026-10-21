using SilkTriangleSample.UITests.Infrastructure;

namespace SilkTriangleSample.UITests.Infrastructure.Tests;

[Trait("Category", "Artifact")]
public sealed class ArtifactDiscoveryTests
{
    [Fact]
    public void ResolvesWindowsArtifactWhenBuilt()
    {
        // When built for Windows in Release, the locator should resolve the executable deterministically
        try
        {
            var manifest = AppArtifactLocator.ResolveArtifact(PlatformKind.Windows, "Release");
            Assert.NotNull(manifest);
            Assert.Equal(PlatformKind.Windows, manifest.Platform);
            Assert.Equal("Release", manifest.Configuration);
            Assert.Equal("net10.0-windows10.0.19041.0", manifest.TargetFramework);
            Assert.True(File.Exists(manifest.ArtifactPath), $"Executable should exist at '{manifest.ArtifactPath}'.");
            Assert.EndsWith("SilkTriangleSample.exe", manifest.ArtifactPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(manifest.FileSizeBytes > 0, "Artifact size must be positive.");
            Assert.False(string.IsNullOrWhiteSpace(manifest.ContentHash), "Content SHA256 must be computed.");
        }
        catch (FileNotFoundException ex)
        {
            // If the local environment hasn't compiled Windows Release yet, verify the message has actionable guidance
            Assert.Contains("dotnet build", ex.Message);
        }
    }

    [Fact]
    public void ThrowsWhenArtifactDoesNotExist()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            AppArtifactLocator.ResolveArtifact(PlatformKind.Windows, "NonExistentConfig_XYZ"));

        Assert.Contains("NonExistentConfig_XYZ", ex.Message);
        Assert.Contains("dotnet build", ex.Message);
    }

    [Fact]
    public void RejectsEmptyOrInvalidCustomPath()
    {
        Assert.Throws<FileNotFoundException>(() =>
            AppArtifactLocator.ResolveArtifact(PlatformKind.Windows, "Release", customPath: "C:\\nonexistent\\app.exe"));
    }

    [Fact]
    public void RejectsWrongExtensionForPlatform()
    {
        // Creating a temporary dummy text file
        var tempFile = Path.GetTempFileName();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppArtifactLocator.ResolveArtifact(PlatformKind.Windows, "Release", customPath: tempFile));

            Assert.Contains("does not match expected extension", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ManifestRecordStoresDeterministicEvidence()
    {
        var now = DateTime.UtcNow;
        var manifest = new AppArtifactManifest(
            Platform: PlatformKind.Android,
            Configuration: "Release",
            TargetFramework: "net10.0-android",
            ArtifactPath: "bin/Release/net10.0-android/com.companyname.silktrianglesample-Signed.apk",
            ArtifactType: ".apk",
            FileSizeBytes: 10240,
            ContentHash: "AABBCCDDEEFF",
            BuildTimestampUtc: now,
            CommitSha: "abc1234"
        );

        Assert.Equal(PlatformKind.Android, manifest.Platform);
        Assert.Equal("Release", manifest.Configuration);
        Assert.Equal(".apk", manifest.ArtifactType);
        Assert.Equal("AABBCCDDEEFF", manifest.ContentHash);
        Assert.Equal(now, manifest.BuildTimestampUtc);
    }
}

