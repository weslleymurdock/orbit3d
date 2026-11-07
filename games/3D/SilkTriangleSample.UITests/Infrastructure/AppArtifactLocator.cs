using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Deterministically resolves and validates the application package or bundle produced by the current build or downloaded CI artifacts.
/// </summary>
public static class AppArtifactLocator
{
    public const string DefaultApplicationId = "com.remotecodehub.silktrianglesample";

    /// <summary>
    /// Reads the canonical ApplicationId directly from <c>SilkTriangleSample.csproj</c>.
    /// </summary>
    public static string GetDeclaredApplicationId(string? repositoryRoot = null)
    {
        var rootDir = repositoryRoot ?? FindRepositoryRoot();
        var csprojPath = Path.Combine(rootDir, "games", "3D", "SilkTriangleSample", "SilkTriangleSample.csproj");
        if (!File.Exists(csprojPath))
            return DefaultApplicationId;

        var document = XDocument.Load(csprojPath);
        var appId = document
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "ApplicationId", StringComparison.Ordinal))
            ?.Value
            ?.Trim();

        return !string.IsNullOrWhiteSpace(appId) ? appId : DefaultApplicationId;
    }

    /// <summary>
    /// Resolves the application artifact for the given platform and configuration.
    /// </summary>
    /// <param name="platform">The target platform.</param>
    /// <param name="configuration">The build configuration (defaults to Release).</param>
    /// <param name="customPath">An optional diagnostic path override (must be validated).</param>
    /// <param name="requireIosIpa">When true for iOS, requires a physical-device .ipa package instead of a simulator .app bundle.</param>
    /// <returns>The validated artifact manifest.</returns>
    public static AppArtifactManifest ResolveArtifact(
        PlatformKind platform,
        string configuration = "Release",
        string? customPath = null,
        bool requireIosIpa = false)
    {
        var rootDir = FindRepositoryRoot();
        var declaredAppId = GetDeclaredApplicationId(rootDir);
        var sampleProjectDir = Path.Combine(rootDir, "games", "3D", "SilkTriangleSample");
        var binDir = Path.Combine(sampleProjectDir, "bin", configuration);

        string resolvedPath;
        string expectedType;
        string targetFramework;
        string? runtimeIdentifier = null;

        switch (platform)
        {
            case PlatformKind.Android:
                targetFramework = "net10.0-android";
                expectedType = ".apk";
                resolvedPath = customPath
                    ?? TestConfiguration.ArtifactPathOverride
                    ?? FindAndroidApk(Path.Combine(binDir, targetFramework), rootDir, declaredAppId);
                runtimeIdentifier = "android-arm64";
                break;

            case PlatformKind.Windows:
                targetFramework = "net10.0-windows10.0.19041.0";
                expectedType = ".exe";
                resolvedPath = customPath
                    ?? TestConfiguration.ArtifactPathOverride
                    ?? FindWindowsExecutable(Path.Combine(binDir, targetFramework), rootDir);
                runtimeIdentifier = "win-x64";
                break;

            case PlatformKind.iOS:
                targetFramework = "net10.0-ios";
                var effectiveCustom = customPath ?? TestConfiguration.IosArtifactPath ?? TestConfiguration.ArtifactPathOverride;
                resolvedPath = effectiveCustom ?? FindIosArtifact(Path.Combine(binDir, targetFramework), rootDir, requireIosIpa);
                if (requireIosIpa || resolvedPath.EndsWith(".ipa", StringComparison.OrdinalIgnoreCase))
                {
                    expectedType = ".ipa";
                    runtimeIdentifier = "ios-arm64";
                }
                else
                {
                    expectedType = ".app";
                    runtimeIdentifier = resolvedPath.Contains("iossimulator-x64", StringComparison.OrdinalIgnoreCase)
                        ? "iossimulator-x64"
                        : "iossimulator-arm64";
                }
                break;

            case PlatformKind.MacCatalyst:
                targetFramework = "net10.0-maccatalyst";
                var macCandidate = customPath ?? TestConfiguration.ArtifactPathOverride ?? FindMacCatalystArtifact(Path.Combine(binDir, targetFramework), rootDir);
                expectedType = macCandidate.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase)
                    ? ".pkg"
                    : macCandidate.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                        ? ".zip"
                        : ".app";
                resolvedPath = macCandidate;
                runtimeIdentifier = "maccatalyst-x64";
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.");
        }

        ValidateArtifact(resolvedPath, platform, expectedType, targetFramework, configuration, declaredAppId);

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
            BuildTimestampUtc: timestamp,
            CommitSha: Environment.GetEnvironmentVariable("GITHUB_SHA"),
            ApplicationId: declaredAppId,
            RuntimeIdentifier: runtimeIdentifier
        );
    }

    /// <summary>
    /// Inspects a physical-device iOS <c>.ipa</c> package, verifying its archive structure, Info.plist metadata, and bundle identifier.
    /// </summary>
    public static IosIpaPackageMetadata InspectIosIpaArchive(string ipaPath, string? expectedBundleId = null)
    {
        if (string.IsNullOrWhiteSpace(ipaPath))
            throw new ArgumentException("IPA path must not be empty.", nameof(ipaPath));
        if (!File.Exists(ipaPath))
            throw new FileNotFoundException($"iOS IPA file not found at '{ipaPath}'.", ipaPath);
        if (!ipaPath.EndsWith(".ipa", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"File '{ipaPath}' does not have the required '.ipa' extension.");

        var fileInfo = new FileInfo(ipaPath);
        if (fileInfo.Length == 0)
            throw new InvalidOperationException($"iOS IPA file '{ipaPath}' is 0 bytes.");

        using var archive = ZipFile.OpenRead(ipaPath);
        var infoPlistEntry = archive.Entries.FirstOrDefault(entry =>
            Regex.IsMatch(entry.FullName.Replace('\\', '/'), @"^Payload/[^/]+\.app/Info\.plist$", RegexOptions.IgnoreCase));

        if (infoPlistEntry is null)
        {
            throw new InvalidOperationException(
                $"IPA archive '{ipaPath}' does not contain a valid 'Payload/<App>.app/Info.plist' entry.");
        }

        var normalizedPlistPath = infoPlistEntry.FullName.Replace('\\', '/');
        var parts = normalizedPlistPath.Split('/');
        var appBundleName = parts[1];
        var bundlePrefix = $"Payload/{appBundleName}/";

        string plistContent;
        using (var stream = infoPlistEntry.Open())
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            plistContent = DecodePlistTextOrXml(memory.ToArray());
        }

        var bundleId = ExtractPlistStringValue(plistContent, "CFBundleIdentifier");
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            throw new InvalidOperationException(
                $"Could not extract CFBundleIdentifier from '{normalizedPlistPath}' inside '{ipaPath}'.");
        }

        var requiredBundleId = expectedBundleId ?? GetDeclaredApplicationId();
        if (!string.Equals(bundleId, requiredBundleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"IPA bundle identifier mismatch in '{ipaPath}': Info.plist declares '{bundleId}', expected '{requiredBundleId}'.");
        }

        var executableName = ExtractPlistStringValue(plistContent, "CFBundleExecutable")
            ?? Path.GetFileNameWithoutExtension(appBundleName);
        var minOsVersion = ExtractPlistStringValue(plistContent, "MinimumOSVersion");

        var executableEntry = archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName.Replace('\\', '/'), $"{bundlePrefix}{executableName}", StringComparison.Ordinal));
        if (executableEntry is null || executableEntry.Length == 0)
        {
            throw new InvalidOperationException(
                $"IPA archive '{ipaPath}' does not contain a non-empty executable '{bundlePrefix}{executableName}'.");
        }

        var hasProvisioningProfile = archive.Entries.Any(entry =>
            string.Equals(entry.FullName.Replace('\\', '/'), $"{bundlePrefix}embedded.mobileprovision", StringComparison.OrdinalIgnoreCase));

        var sha256 = ComputeFileHash(ipaPath);

        return new IosIpaPackageMetadata(
            IpaPath: ipaPath,
            AppBundleName: appBundleName,
            BundleIdentifier: bundleId,
            ExecutableName: executableName,
            MinimumOsVersion: minOsVersion,
            HasProvisioningProfile: hasProvisioningProfile,
            UncompressedExecutableSizeBytes: executableEntry.Length,
            Sha256Hash: sha256);
    }

    private static void ValidateArtifact(
        string path,
        PlatformKind platform,
        string expectedType,
        string tfm,
        string config,
        string declaredAppId)
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

            if (platform == PlatformKind.iOS && expectedType == ".ipa")
            {
                _ = InspectIosIpaArchive(path, declaredAppId);
            }
        }
    }

    private static string FindAndroidApk(string targetDir, string rootDir, string appId)
    {
        var ciDirs = new[]
        {
            Path.Combine(rootDir, "artifacts", "ci-builds", "silk-triangle-sample-android"),
            Path.Combine(rootDir, "artifacts", "downloads", "silk-triangle-sample-android")
        };

        foreach (var ciDir in ciDirs)
        {
            if (Directory.Exists(ciDir))
            {
                var ciSigned = Path.Combine(ciDir, $"{appId}-Signed.apk");
                if (File.Exists(ciSigned))
                    return ciSigned;
                var ciApks = Directory.GetFiles(ciDir, "*.apk", SearchOption.AllDirectories);
                if (ciApks.Length > 0)
                    return ciApks[0];
            }
        }

        if (!Directory.Exists(targetDir))
            return Path.Combine(targetDir, $"{appId}-Signed.apk");

        var signed = Path.Combine(targetDir, $"{appId}-Signed.apk");
        if (File.Exists(signed))
            return signed;

        var unsigned = Path.Combine(targetDir, $"{appId}.apk");
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

    private static string FindWindowsExecutable(string targetDir, string rootDir)
    {
        var ciDirs = new[]
        {
            Path.Combine(rootDir, "artifacts", "ci-builds", "silk-triangle-sample-windows"),
            Path.Combine(rootDir, "artifacts", "downloads", "silk-triangle-sample-windows")
        };

        foreach (var ciDir in ciDirs)
        {
            if (Directory.Exists(ciDir))
            {
                var exes = Directory.GetFiles(ciDir, "SilkTriangleSample.exe", SearchOption.AllDirectories);
                if (exes.Length > 0)
                    return exes[0];
            }
        }

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

    private static string FindIosArtifact(string targetDir, string rootDir, bool requireIosIpa)
    {
        var ciDirs = new[]
        {
            Path.Combine(rootDir, "artifacts", "ci-builds", "silk-triangle-sample-ios"),
            Path.Combine(rootDir, "artifacts", "downloads", "silk-triangle-sample-ios")
        };

        foreach (var ciDir in ciDirs)
        {
            if (Directory.Exists(ciDir))
            {
                var ipas = Directory.GetFiles(ciDir, "*.ipa", SearchOption.AllDirectories);
                if (ipas.Length > 0)
                    return ipas[0];
            }
        }

        if (Directory.Exists(targetDir))
        {
            var ipas = Directory.GetFiles(targetDir, "*.ipa", SearchOption.AllDirectories);
            if (ipas.Length > 0)
                return ipas[0];
        }

        if (requireIosIpa || TestConfiguration.UseBrowserStack)
        {
            return Path.Combine(targetDir, "ios-arm64", "publish", "SilkTriangleSample.ipa");
        }

        return FindAppleAppBundle(targetDir);
    }

    private static string FindMacCatalystArtifact(string targetDir, string rootDir)
    {
        var ciDirs = new[]
        {
            Path.Combine(rootDir, "artifacts", "ci-builds", "silk-triangle-sample-maccatalyst"),
            Path.Combine(rootDir, "artifacts", "downloads", "silk-triangle-sample-maccatalyst")
        };

        foreach (var ciDir in ciDirs)
        {
            if (Directory.Exists(ciDir))
            {
                var appDirs = Directory.GetDirectories(ciDir, "SilkTriangleSample.app", SearchOption.AllDirectories);
                if (appDirs.Length > 0)
                    return appDirs[0];
                var pkgs = Directory.GetFiles(ciDir, "*.pkg", SearchOption.AllDirectories);
                if (pkgs.Length > 0)
                    return pkgs[0];
                var zips = Directory.GetFiles(ciDir, "*.zip", SearchOption.AllDirectories);
                if (zips.Length > 0)
                    return zips[0];
            }
        }

        return FindAppleAppBundle(targetDir);
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

        return Directory.GetCurrentDirectory();
    }

    public static string ComputeFileHash(string filePath)
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
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            sha256.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);

            var fileBytes = File.ReadAllBytes(file);
            sha256.TransformBlock(fileBytes, 0, fileBytes.Length, null, 0);
        }
        sha256.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha256.Hash ?? []);
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

    private static string DecodePlistTextOrXml(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Contains("<plist", StringComparison.OrdinalIgnoreCase))
            return text;

        // Binary plist ("bplist00") still stores ASCII keys and values as contiguous UTF-8/ASCII strings
        return Encoding.Latin1.GetString(bytes);
    }

    private static string? ExtractPlistStringValue(string plistContent, string key)
    {
        if (plistContent.Contains("<plist", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var doc = XDocument.Parse(plistContent);
                var dict = doc.Root?.Element("dict");
                if (dict is not null)
                {
                    var elements = dict.Elements().ToList();
                    for (var i = 0; i < elements.Count - 1; i++)
                    {
                        if (elements[i].Name.LocalName == "key" &&
                            string.Equals(elements[i].Value.Trim(), key, StringComparison.Ordinal) &&
                            elements[i + 1].Name.LocalName == "string")
                        {
                            return elements[i + 1].Value.Trim();
                        }
                    }
                }
            }
            catch
            {
                // Fall back to regex extraction below
            }
        }

        var xmlMatch = Regex.Match(
            plistContent,
            $"<key>\\s*{Regex.Escape(key)}\\s*</key>\\s*<string>\\s*([^<]+?)\\s*</string>",
            RegexOptions.Singleline);
        if (xmlMatch.Success)
            return xmlMatch.Groups[1].Value.Trim();

        if (key == "CFBundleIdentifier")
        {
            var reverseDnsMatch = Regex.Match(plistContent, @"com\.[a-zA-Z0-9_\-]+(?:\.[a-zA-Z0-9_\-]+)+");
            if (reverseDnsMatch.Success)
                return reverseDnsMatch.Value;
        }

        return null;
    }
}
