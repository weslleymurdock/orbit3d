using AnimationSample.UITests.Infrastructure;

namespace AnimationSample.UITests.Infrastructure.Tests;

[Trait("Category", "Artifact")]
public sealed class ArtifactDiscoveryTests
{
    [Fact]
    public void GetDeclaredApplicationId_MatchesMauiProjectConfiguration()
    {
        var appId = AppArtifactLocator.GetDeclaredApplicationId();
        Assert.Equal("com.remotecodehub.animationsample", appId);
    }

    [Fact]
    public void ResolvesWindowsArtifactWhenBuilt()
    {
        var manifest = AppArtifactLocator.ResolveArtifact(PlatformKind.Windows, "Release");
        Assert.NotNull(manifest);
        Assert.Equal(PlatformKind.Windows, manifest.Platform);
        Assert.Equal("Release", manifest.Configuration);
        Assert.Equal("net11.0-windows10.0.19041.0", manifest.TargetFramework);
        Assert.Equal("com.remotecodehub.animationsample", manifest.ApplicationId);
        Assert.True(File.Exists(manifest.ArtifactPath), $"Executable should exist at '{manifest.ArtifactPath}'.");
        Assert.EndsWith("AnimationSample.exe", manifest.ArtifactPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(manifest.FileSizeBytes > 0, "Artifact size must be positive.");
        Assert.False(string.IsNullOrWhiteSpace(manifest.ContentHash), "Content SHA256 must be computed.");
    }

    [Fact]
    public void ResolvesAndroidArtifactWhenBuilt()
    {
        var manifest = AppArtifactLocator.ResolveArtifact(PlatformKind.Android, "Release");
        Assert.NotNull(manifest);
        Assert.Equal(PlatformKind.Android, manifest.Platform);
        Assert.Equal("Release", manifest.Configuration);
        Assert.Equal("net11.0-android", manifest.TargetFramework);
        Assert.Equal("com.remotecodehub.animationsample", manifest.ApplicationId);
        Assert.True(File.Exists(manifest.ArtifactPath), $"APK should exist at '{manifest.ArtifactPath}'.");
        Assert.EndsWith(".apk", manifest.ArtifactPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(manifest.FileSizeBytes > 0, "Artifact size must be positive.");
    }

    [Fact]
    public void ThrowsWhenArtifactDoesNotExist()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            AppArtifactLocator.ResolveArtifact(PlatformKind.Windows, "NonExistentConfig_XYZ"));

        Assert.Contains("NonExistentConfig_XYZ", ex.Message);
        Assert.Contains("dotnet build", ex.Message);
    }
}
