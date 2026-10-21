using System.Security.Cryptography;
using System.Text.Json;

namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Deterministically resolves and validates the application package or bundle produced by the current build.
/// </summary>
public static class AppArtifactLocator
{
    private const string AppId = "com.companyname.silktrianglesample";

    /// <summary>
    /// Resolves the application artifact for the given platform and configuration.
    /// </summary>
    /// <param name="platform">The target platform.</param>
    /// <param name="configuration">The build configuration (defaults to Release).</param>
    /// <param name="customPath">An optional diagnostic path override (must be validated).</param>
    /// <returns>The validated artifact manifest.</returns>
    public static AppArtifactManifest ResolveArtifact(
        PlatformKind platform,
        string configuration = "Release",
        string? customPath = null)
    {
        var rootDir = FindRepositoryRoot();
        var sampleProjectDir = Path.Combine(rootDir, "games", "3D", "SilkTriangleSample");
        var binDir = Path.Combine(sampleProjectDir, "bin", configuration);

        string resolvedPath;
        string expectedType;
        string targetFramework;

        switch (platform)
        {
            case PlatformKind.Android:
                targetFramework = "net10.0-android";
                expectedType = ".apk";
                resolvedPath = customPath ?? FindAndroidApk(Path.Combine(binDir, targetFramework));
                break;

            case PlatformKind.Windows:
                targetFramework = "net10.0-windows10.0.19041.0";
                expectedType = ".exe";
                resolvedPath = customPath ?? FindWindowsExecutable(Path.Combine(binDir, targetFramework));
                break;

            case PlatformKind.iOS:
                targetFramework = "net10.0-ios";
                expectedType = ".app";
                resolvedPath = customPath ?? FindAppleAppBundle(Path.Combine(binDir, targetFramework));
                break;

            case PlatformKind.MacCatalyst:
                targetFramework = "net10.0-maccatalyst";
                expectedType = ".app";
                resolvedPath = customPath ?? FindAppleAppBundle(Path.Combine(binDir, targetFramework));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.");
        }

        ValidateArtifact(resolvedPath, platform, expectedType, targetFramework, configuration);

        var fileInfo = new FileInfo(resolvedPath);
        var isDirectory = Directory.Exists(resolvedPath);
        var size = isDirectory ? CalculateDirectorySize(new DirectoryInfo(resolvedPath)) : fileInfo.Length;
        var hash = isDirectory ? ComputeDirectoryHash(resolvedPath) : ComputeFileHash(resolvedPath);
        var timestamp = isDirectory ? Directory.GetLastWriteTimeUtc(resolvedPath) : fileInfo.LastWriteTimeUtc;

        return new AppArtifactManifest(
            Platform: platform,
            Configuration: configuration,
            TargetFramework: targetFramework,
            ArtifactPath: resolvedPath,
            ArtifactType: expectedType,
            FileSizeBytes: size,
            ContentHash: hash,
            BuildTimestampUtc: timestamp
        );
    }

    private static void ValidateArtifact(string path, PlatformKind platform, string expectedType, string tfm, string config)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new FileNotFoundException(
                $"No artifact path provided for {platform} ({config}). " +
                $"Run: dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration {config} -f {tfm}");
        }

        if (expectedType == ".app")
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(
                    $"Apple bundle directory not found at: '{path}'. " +
                    $"Run: dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration {config} -f {tfm}");
            }

            if (!path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Resolved artifact '{path}' is not a valid Apple .app bundle.");
            }
        }
        else
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Artifact file not found at: '{path}'. " +
                    $"Run: dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration {config} -f {tfm}");
            }

            if (!path.EndsWith(expectedType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Resolved artifact '{path}' does not match expected extension '{expectedType}'.");
            }

            var length = new FileInfo(path).Length;
            if (length == 0)
            {
                throw new InvalidOperationException(
                    $"Artifact file '{path}' is 0 bytes (corrupt or unbuilt).");
            }
        }
    }

    private static string FindAndroidApk(string targetDir)
    {
        if (!Directory.Exists(targetDir))
            return Path.Combine(targetDir, $"{AppId}-Signed.apk");

        var signed = Path.Combine(targetDir, $"{AppId}-Signed.apk");
        if (File.Exists(signed))
            return signed;

        var unsigned = Path.Combine(targetDir, $"{AppId}.apk");
        if (File.Exists(unsigned))
            return unsigned;

        var publishDir = Path.Combine(targetDir, "publish");
        if (Directory.Exists(publishDir))
        {
            var apks = Directory.GetFiles(publishDir, "*.apk");
            if (apks.Length > 0)
                return apks[0];
        }

        var anyApk = Directory.GetFiles(targetDir, "*.apk", SearchOption.AllDirectories);
        return anyApk.Length > 0 ? anyApk[0] : signed;
    }

    private static string FindWindowsExecutable(string targetDir)
    {
        var winX64 = Path.Combine(targetDir, "win-x64", "SilkTriangleSample.exe");
        if (File.Exists(winX64))
            return winX64;

        var direct = Path.Combine(targetDir, "SilkTriangleSample.exe");
        if (File.Exists(direct))
            return direct;

        var publish = Path.Combine(targetDir, "win-x64", "publish", "SilkTriangleSample.exe");
        if (File.Exists(publish))
            return publish;

        return winX64;
    }

    private static string FindAppleAppBundle(string targetDir)
    {
        if (!Directory.Exists(targetDir))
            return Path.Combine(targetDir, "SilkTriangleSample.app");

        var direct = Path.Combine(targetDir, "SilkTriangleSample.app");
        if (Directory.Exists(direct))
            return direct;

        var subDirs = Directory.GetDirectories(targetDir, "SilkTriangleSample.app", SearchOption.AllDirectories);
        if (subDirs.Length > 0)
            return subDirs[0];

        return direct;
    }

    public static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "global.json")) ||
                File.Exists(Path.Combine(current, "Directory.Build.props")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        // Fallback to working directory
        return Directory.GetCurrentDirectory();
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(bytes);
    }

    private static string ComputeDirectoryHash(string dirPath)
    {
        using var sha256 = SHA256.Create();
        var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
        Array.Sort(files);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(dirPath, file);
            var pathBytes = System.Text.Encoding.UTF8.GetBytes(relativePath);
            sha256.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);

            var fileBytes = File.ReadAllBytes(file);
            sha256.TransformBlock(fileBytes, 0, fileBytes.Length, null, 0);
        }
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
    }

    private static long CalculateDirectorySize(DirectoryInfo dir)
    {
        long size = 0;
        foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
        {
            size += file.Length;
        }
        return size;
    }
}

