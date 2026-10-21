using System.Net.Http;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Mac;
using OpenQA.Selenium.Appium.Windows;

namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Factory that constructs platform-specific Appium drivers with verified capabilities and connection validation.
/// </summary>
public static class AppiumDriverFactory
{
    /// <summary>
    /// Creates and connects a platform-specific Appium driver for the resolved application artifact.
    /// </summary>
    public static AppiumDriver CreateDriver(PlatformKind platform, string appPath)
    {
        ValidateServerReachable(TestConfiguration.AppiumServerUrl);

        var options = new AppiumOptions();
        options.AddAdditionalAppiumOption("newCommandTimeout", 120);

        switch (platform)
        {
            case PlatformKind.Android:
                options.PlatformName = "Android";
                options.AutomationName = "UiAutomator2";
                options.App = appPath;
                options.AddAdditionalAppiumOption("appPackage", "com.companyname.silktrianglesample");
                options.AddAdditionalAppiumOption("appActivity", "crc642279f0ce6b63d917.MainActivity");
                options.AddAdditionalAppiumOption("autoGrantPermissions", true);
                if (!string.IsNullOrEmpty(TestConfiguration.AndroidDeviceName))
                    options.AddAdditionalAppiumOption("deviceName", TestConfiguration.AndroidDeviceName);
                return new AndroidDriver(TestConfiguration.AppiumServerUrl, options, TimeSpan.FromSeconds(120));

            case PlatformKind.Windows:
                options.PlatformName = "Windows";
                options.AutomationName = "Windows";
                options.App = appPath;
                options.AddAdditionalAppiumOption("ms:waitForAppLaunch", "15");
                return new WindowsDriver(TestConfiguration.AppiumServerUrl, options, TimeSpan.FromSeconds(120));

            case PlatformKind.iOS:
                options.PlatformName = "iOS";
                options.AutomationName = "XCUITest";
                options.App = appPath;
                options.AddAdditionalAppiumOption("bundleId", "com.companyname.silktrianglesample");
                if (!string.IsNullOrEmpty(TestConfiguration.IosSimulatorUdid))
                    options.AddAdditionalAppiumOption("udid", TestConfiguration.IosSimulatorUdid);
                return new IOSDriver(TestConfiguration.AppiumServerUrl, options, TimeSpan.FromSeconds(120));

            case PlatformKind.MacCatalyst:
                options.PlatformName = "Mac";
                options.AutomationName = "Mac2";
                options.App = appPath;
                options.AddAdditionalAppiumOption("bundleId", "com.companyname.silktrianglesample");
                return new MacDriver(TestConfiguration.AppiumServerUrl, options, TimeSpan.FromSeconds(120));

            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform for Appium driver creation.");
        }
    }

    /// <summary>
    /// Verifies that the Appium server is reachable before attempting a session.
    /// </summary>
    public static void ValidateServerReachable(Uri serverUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var statusUrl = new Uri(serverUrl, "status");
            var response = client.GetAsync(statusUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                // Some Appium versions use /wd/hub/status
                var hubStatusUrl = new Uri(serverUrl, "wd/hub/status");
                var hubResponse = client.GetAsync(hubStatusUrl).GetAwaiter().GetResult();
                if (!hubResponse.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Appium server returned status {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Appium server is not reachable at '{serverUrl}'.\n" +
                "Ensure Appium is running: 'appium --address 127.0.0.1 --port 4723'.\n" +
                $"Underlying error: {ex.Message}", ex);
        }
    }
}

