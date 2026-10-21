namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Centralized configuration for UI testing sessions.
/// </summary>
public static class TestConfiguration
{
    public static Uri AppiumServerUrl =>
        new(Environment.GetEnvironmentVariable("ORBIT3D_APPIUM_URL") ?? "http://127.0.0.1:4723/");

    public static string Configuration =>
        Environment.GetEnvironmentVariable("ORBIT3D_TEST_CONFIGURATION") ?? "Release";

    public static TimeSpan DefaultTimeout =>
        TimeSpan.FromSeconds(
            double.TryParse(Environment.GetEnvironmentVariable("ORBIT3D_TEST_TIMEOUT_SECONDS"), out var seconds) ? seconds : 30);

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

    public static string? AndroidDeviceName =>
        Environment.GetEnvironmentVariable("ORBIT3D_ANDROID_DEVICE");

    public static string? IosSimulatorUdid =>
        Environment.GetEnvironmentVariable("ORBIT3D_IOS_SIMULATOR_UDID");
}

