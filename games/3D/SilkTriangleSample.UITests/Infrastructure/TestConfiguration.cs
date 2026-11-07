namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Centralized configuration for local and BrowserStack App Automate UI testing sessions.
/// </summary>
public static class TestConfiguration
{
    public static bool UseBrowserStack =>
        string.Equals(Environment.GetEnvironmentVariable("ORBIT3D_USE_BROWSERSTACK"), "true", StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(BrowserStackAppUrl);

    public static Uri AppiumServerUrl
    {
        get
        {
            var customUrl = Environment.GetEnvironmentVariable("ORBIT3D_APPIUM_URL");
            if (!string.IsNullOrWhiteSpace(customUrl))
                return new Uri(customUrl);

            return UseBrowserStack
                ? new Uri("https://hub-cloud.browserstack.com/wd/hub/")
                : new Uri("http://127.0.0.1:4723/");
        }
    }

    public static string Configuration =>
        Environment.GetEnvironmentVariable("ORBIT3D_TEST_CONFIGURATION") ?? "Release";

    public static TimeSpan DefaultTimeout =>
        TimeSpan.FromSeconds(
            double.TryParse(Environment.GetEnvironmentVariable("ORBIT3D_TEST_TIMEOUT_SECONDS"), out var seconds) ? seconds : 45);

    public static bool CaptureScreenshots =>
        !string.Equals(Environment.GetEnvironmentVariable("ORBIT3D_CAPTURE_SCREENSHOTS"), "false", StringComparison.OrdinalIgnoreCase);

    public static string ArtifactsDirectory
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("ORBIT3D_TEST_ARTIFACTS_DIR");
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            var root = AppArtifactLocator.FindRepositoryRoot();
            return Path.Combine(root, "artifacts", "uitests");
        }
    }

    public static string? ArtifactPathOverride =>
        Environment.GetEnvironmentVariable("ORBIT3D_ARTIFACT_PATH");

    public static string? IosArtifactPath =>
        Environment.GetEnvironmentVariable("ORBIT3D_IOS_ARTIFACT_PATH");

    public static string? AndroidDeviceName =>
        Environment.GetEnvironmentVariable("ORBIT3D_ANDROID_DEVICE");

    public static string? IosSimulatorUdid =>
        Environment.GetEnvironmentVariable("ORBIT3D_IOS_SIMULATOR_UDID");

    public static string IosDeviceName =>
        Environment.GetEnvironmentVariable("ORBIT3D_IOS_DEVICE") ?? "iPhone 16";

    public static string IosOsVersion =>
        Environment.GetEnvironmentVariable("ORBIT3D_IOS_OS_VERSION") ?? "18";

    public static string? BrowserStackUsername =>
        Environment.GetEnvironmentVariable("BROWSERSTACK_USERNAME");

    public static string? BrowserStackAccessKey =>
        Environment.GetEnvironmentVariable("BROWSERSTACK_ACCESS_KEY");

    public static string? BrowserStackAppUrl =>
        Environment.GetEnvironmentVariable("ORBIT3D_BROWSERSTACK_APP");

    public static string BrowserStackProjectName =>
        Environment.GetEnvironmentVariable("ORBIT3D_BROWSERSTACK_PROJECT") ?? "Orbit3D";

    public static string BrowserStackBuildName =>
        Environment.GetEnvironmentVariable("ORBIT3D_BROWSERSTACK_BUILD") ?? "Orbit3D Stage 08 iOS Real Device Validation";

    public static string BrowserStackSessionName =>
        Environment.GetEnvironmentVariable("ORBIT3D_BROWSERSTACK_SESSION") ?? "SilkTriangleSample iOS Validation";

    public static void ValidateBrowserStackCredentials()
    {
        if (string.IsNullOrWhiteSpace(BrowserStackUsername) || string.IsNullOrWhiteSpace(BrowserStackAccessKey))
        {
            throw new InvalidOperationException(
                "BrowserStack credentials are not configured. Set BROWSERSTACK_USERNAME and BROWSERSTACK_ACCESS_KEY environment variables before starting a BrowserStack App Automate session.");
        }
    }
}
