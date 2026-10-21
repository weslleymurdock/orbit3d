using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace SilkTriangleSample.UITests.Infrastructure;

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
            Console.Error.WriteLine($"[Diagnostics] Failed to take screenshot '{label}': {ex.Message}");
            return null;
        }
    }
}

