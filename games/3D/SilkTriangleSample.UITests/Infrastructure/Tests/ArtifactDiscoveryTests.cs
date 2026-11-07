using System.IO.Compression;
using System.Text;
using SilkTriangleSample.UITests.Infrastructure;
using SilkTriangleSample.UITests.iOS.Pages;

namespace SilkTriangleSample.UITests.Infrastructure.Tests;

[Trait("Category", "Artifact")]
public sealed class ArtifactDiscoveryTests
{
    [Fact]
    public void GetDeclaredApplicationId_MatchesMauiProjectConfiguration()
    {
        var appId = AppArtifactLocator.GetDeclaredApplicationId();
        Assert.Equal("com.remotecodehub.silktrianglesample", appId);
    }

    [Fact]
    public void ResolvesWindowsArtifactWhenBuilt()
    {
        try
        {
            var manifest = AppArtifactLocator.ResolveArtifact(PlatformKind.Windows, "Release");
            Assert.NotNull(manifest);
            Assert.Equal(PlatformKind.Windows, manifest.Platform);
            Assert.Equal("Release", manifest.Configuration);
            Assert.Equal("net10.0-windows10.0.19041.0", manifest.TargetFramework);
            Assert.Equal("com.remotecodehub.silktrianglesample", manifest.ApplicationId);
            Assert.True(File.Exists(manifest.ArtifactPath), $"Executable should exist at '{manifest.ArtifactPath}'.");
            Assert.EndsWith("SilkTriangleSample.exe", manifest.ArtifactPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(manifest.FileSizeBytes > 0, "Artifact size must be positive.");
            Assert.False(string.IsNullOrWhiteSpace(manifest.ContentHash), "Content SHA256 must be computed.");
        }
        catch (FileNotFoundException ex)
        {
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
    public void InspectIosIpaArchive_ValidatesValidIpaAndResolvesManifest()
    {
        var tempIpa = Path.Combine(Path.GetTempPath(), $"SilkTriangleSample_{Guid.NewGuid():N}.ipa");
        try
        {
            CreateTestIpaArchive(
                tempIpa,
                bundleId: "com.remotecodehub.silktrianglesample",
                executableName: "SilkTriangleSample",
                includeProvisioningProfile: true);

            var metadata = AppArtifactLocator.InspectIosIpaArchive(tempIpa);
            Assert.Equal("SilkTriangleSample.app", metadata.AppBundleName);
            Assert.Equal("com.remotecodehub.silktrianglesample", metadata.BundleIdentifier);
            Assert.Equal("SilkTriangleSample", metadata.ExecutableName);
            Assert.Equal("15.0", metadata.MinimumOsVersion);
            Assert.True(metadata.HasProvisioningProfile);
            Assert.True(metadata.UncompressedExecutableSizeBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(metadata.Sha256Hash));

            var manifest = AppArtifactLocator.ResolveArtifact(
                PlatformKind.iOS,
                "Release",
                customPath: tempIpa,
                requireIosIpa: true);

            Assert.Equal(PlatformKind.iOS, manifest.Platform);
            Assert.Equal(".ipa", manifest.ArtifactType);
            Assert.Equal("ios-arm64", manifest.RuntimeIdentifier);
            Assert.Equal("com.remotecodehub.silktrianglesample", manifest.ApplicationId);
            Assert.Equal(metadata.Sha256Hash, manifest.ContentHash);
        }
        finally
        {
            if (File.Exists(tempIpa))
                File.Delete(tempIpa);
        }
    }

    [Fact]
    public void InspectIosIpaArchive_RejectsMismatchedBundleIdentifier()
    {
        var tempIpa = Path.Combine(Path.GetTempPath(), $"SilkTriangleSample_Mismatch_{Guid.NewGuid():N}.ipa");
        try
        {
            CreateTestIpaArchive(
                tempIpa,
                bundleId: "com.companyname.silktrianglesample",
                executableName: "SilkTriangleSample",
                includeProvisioningProfile: false);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppArtifactLocator.InspectIosIpaArchive(tempIpa));

            Assert.Contains("bundle identifier mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("com.companyname.silktrianglesample", ex.Message);
            Assert.Contains("com.remotecodehub.silktrianglesample", ex.Message);
        }
        finally
        {
            if (File.Exists(tempIpa))
                File.Delete(tempIpa);
        }
    }

    [Fact]
    public void ParseDiagnostics_ExtractsStructuredMetricsAndCameraState()
    {
        const string sample =
            "Phase=Rendering;Context=Ready;Device=Silk;Assets=Loaded;Meshes=1;Texture=1008x752;Items=1;DrawCalls=1;Triangles=20000;FPS=60;Frames=128;Viewport=1170x2532;Yaw=0.45;Pitch=-0.18;Dist=5.50";

        var parsed = IosGraphicSurfacePage.ParseDiagnostics(sample);
        Assert.Equal("Rendering", parsed["Phase"]);
        Assert.Equal("Ready", parsed["Context"]);
        Assert.Equal("Silk", parsed["Device"]);
        Assert.Equal("Loaded", parsed["Assets"]);
        Assert.Equal("1", parsed["Meshes"]);
        Assert.Equal("1008x752", parsed["Texture"]);
        Assert.Equal("1", parsed["DrawCalls"]);
        Assert.Equal("20000", parsed["Triangles"]);
        Assert.Equal("128", parsed["Frames"]);
        Assert.Equal("1170x2532", parsed["Viewport"]);
        Assert.Equal("0.45", parsed["Yaw"]);
        Assert.Equal("-0.18", parsed["Pitch"]);
    }

    [Fact]
    public void ManifestRecordStoresDeterministicEvidence()
    {
        var now = DateTime.UtcNow;
        var manifest = new AppArtifactManifest(
            Platform: PlatformKind.Android,
            Configuration: "Release",
            TargetFramework: "net10.0-android",
            ArtifactPath: "bin/Release/net10.0-android/com.remotecodehub.silktrianglesample-Signed.apk",
            ArtifactType: ".apk",
            FileSizeBytes: 10240,
            ContentHash: "AABBCCDDEEFF",
            BuildTimestampUtc: now,
            CommitSha: "abc1234",
            ApplicationId: "com.remotecodehub.silktrianglesample",
            RuntimeIdentifier: "android-arm64"
        );

        Assert.Equal(PlatformKind.Android, manifest.Platform);
        Assert.Equal("Release", manifest.Configuration);
        Assert.Equal(".apk", manifest.ArtifactType);
        Assert.Equal("AABBCCDDEEFF", manifest.ContentHash);
        Assert.Equal("com.remotecodehub.silktrianglesample", manifest.ApplicationId);
        Assert.Equal(now, manifest.BuildTimestampUtc);
    }

    private static void CreateTestIpaArchive(
        string ipaPath,
        string bundleId,
        string executableName,
        bool includeProvisioningProfile)
    {
        var plistXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>CFBundleIdentifier</key>
                <string>{bundleId}</string>
                <key>CFBundleExecutable</key>
                <string>{executableName}</string>
                <key>MinimumOSVersion</key>
                <string>15.0</string>
            </dict>
            </plist>
            """;

        using var archive = ZipFile.Open(ipaPath, ZipArchiveMode.Create);

        var plistEntry = archive.CreateEntry("Payload/SilkTriangleSample.app/Info.plist");
        using (var writer = new StreamWriter(plistEntry.Open(), Encoding.UTF8))
        {
            writer.Write(plistXml);
        }

        var exeEntry = archive.CreateEntry($"Payload/SilkTriangleSample.app/{executableName}");
        using (var stream = exeEntry.Open())
        {
            var machOHeaderStub = new byte[] { 0xCF, 0xFA, 0xED, 0xFE, 0x0C, 0x00, 0x00, 0x01 };
            stream.Write(machOHeaderStub, 0, machOHeaderStub.Length);
        }

        if (includeProvisioningProfile)
        {
            var provEntry = archive.CreateEntry("Payload/SilkTriangleSample.app/embedded.mobileprovision");
            using var stream = provEntry.Open();
            var profileStub = Encoding.UTF8.GetBytes("provision-profile-metadata");
            stream.Write(profileStub, 0, profileStub.Length);
        }
    }
}
