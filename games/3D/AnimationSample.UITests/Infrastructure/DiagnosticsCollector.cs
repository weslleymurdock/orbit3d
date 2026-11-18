using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace AnimationSample.UITests.Infrastructure;

/// <summary>
/// Collects visual and textual diagnostics upon test execution or failure.
/// </summary>
public static class DiagnosticsCollector
{
    /// <summary>
    /// Captures screenshot and page source to the configured artifacts directory.
    /// </summary>
    public static void CaptureFailureDiagnostics(
        AppiumDriver? driver,
        PlatformKind platform,
        string testName,
        Exception? exception = null)
    {
        if (driver is null)
            return;

        try
        {
            var outputDir = Path.Combine(TestConfiguration.ArtifactsDirectory, platform.ToString());
            Directory.CreateDirectory(outputDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var safeTestName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));

            // Capture screenshot
            try
            {
                var screenshot = driver.GetScreenshot();
                var screenshotPath = Path.Combine(outputDir, $"{safeTestName}_{timestamp}.png");
                screenshot.SaveAsFile(screenshotPath);
                Console.WriteLine($"[Diagnostics] Saved screenshot: {screenshotPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Diagnostics] Failed to capture screenshot: {ex.Message}");
            }

            // Capture page source
            try
            {
                var pageSource = driver.PageSource;
                if (!string.IsNullOrEmpty(pageSource))
                {
                    var sourcePath = Path.Combine(outputDir, $"{safeTestName}_{timestamp}_pagesource.xml");
                    File.WriteAllText(sourcePath, pageSource);
                    Console.WriteLine($"[Diagnostics] Saved page source: {sourcePath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Diagnostics] Failed to capture page source: {ex.Message}");
            }

            // Record failure details
            if (exception is not null)
            {
                var errorPath = Path.Combine(outputDir, $"{safeTestName}_{timestamp}_error.txt");
                File.WriteAllText(errorPath, exception.ToString());
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Diagnostics] Diagnostics collection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures a named visual screenshot.
    /// </summary>
    public static string? CaptureScreenshot(AppiumDriver driver, PlatformKind platform, string label)
    {
        try
        {
            var outputDir = Path.Combine(TestConfiguration.ArtifactsDirectory, platform.ToString(), "screenshots");
            Directory.CreateDirectory(outputDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var safeLabel = string.Join("_", label.Split(Path.GetInvalidFileNameChars()));
            var screenshotPath = Path.Combine(outputDir, $"{safeLabel}_{timestamp}.png");

            var screenshot = driver.GetScreenshot();
            screenshot.SaveAsFile(screenshotPath);
            Console.WriteLine($"[Diagnostics] Screenshot saved: {screenshotPath}");
            return screenshotPath;
        }
        catch (Exception ex)
        {
            if (platform == PlatformKind.Windows && TryCaptureViaSilkReadback(platform, label, out var fallbackPath))
            {
                return fallbackPath;
            }

            Console.Error.WriteLine($"[Diagnostics] Failed to take screenshot '{label}': {ex.Message}");
            return null;
        }
    }

    private static bool TryCaptureViaSilkReadback(PlatformKind platform, string label, out string? screenshotPath)
    {
        screenshotPath = null;
        var captureDir = Environment.GetEnvironmentVariable("ORBIT3D_SILK_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(captureDir))
            return false;

        try
        {
            Directory.CreateDirectory(captureDir);
            var safeLabel = string.Join("_", label.Split(Path.GetInvalidFileNameChars()));
            var requestName = $"{safeLabel}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var capturedPng = Path.Combine(captureDir, requestName + ".png");
            var requestFile = Path.Combine(captureDir, "capture.request");
            File.WriteAllText(requestFile, requestName);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(capturedPng) && new FileInfo(capturedPng).Length > 0)
                {
                    var outputDir = Path.Combine(TestConfiguration.ArtifactsDirectory, platform.ToString(), "screenshots");
                    Directory.CreateDirectory(outputDir);
                    var destPath = Path.Combine(outputDir, Path.GetFileName(capturedPng));
                    File.Copy(capturedPng, destPath, overwrite: true);
                    screenshotPath = destPath;
                    Console.WriteLine($"[Diagnostics] Screenshot saved via Silk readback fallback: {destPath}");
                    return true;
                }
                Thread.Sleep(50);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Diagnostics] Silk readback screenshot fallback failed: {ex.Message}");
        }

        return false;
    }
}

