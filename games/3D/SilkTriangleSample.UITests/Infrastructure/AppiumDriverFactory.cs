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
    /// Creates and connects a platform-specific Appium driver for the resolved application artifact or BrowserStack app identifier.
    /// </summary>
    public static AppiumDriver CreateDriver(PlatformKind platform, string appPath)
    {
        var applicationId = AppArtifactLocator.GetDeclaredApplicationId();
        var options = BuildAppiumOptions(platform, appPath, applicationId);

        if (!TestConfiguration.UseBrowserStack)
        {
            ValidateServerReachable(TestConfiguration.AppiumServerUrl);
        }

        var commandTimeout = TestConfiguration.UseBrowserStack
            ? TimeSpan.FromSeconds(180)
            : TimeSpan.FromSeconds(120);

        return platform switch
        {
            PlatformKind.Android => new AndroidDriver(TestConfiguration.AppiumServerUrl, options, commandTimeout),
            PlatformKind.Windows => new WindowsDriver(TestConfiguration.AppiumServerUrl, options, commandTimeout),
            PlatformKind.iOS => new IOSDriver(TestConfiguration.AppiumServerUrl, options, commandTimeout),
            PlatformKind.MacCatalyst => new MacDriver(TestConfiguration.AppiumServerUrl, options, commandTimeout),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform for Appium driver creation.")
        };
    }

    /// <summary>
    /// Builds validated W3C / Appium capabilities for local or BrowserStack execution without logging secret values.
    /// </summary>
    public static AppiumOptions BuildAppiumOptions(PlatformKind platform, string appPath, string? applicationId = null)
    {
        var resolvedAppId = applicationId ?? AppArtifactLocator.GetDeclaredApplicationId();
        var options = new AppiumOptions();
        options.AddAdditionalAppiumOption("newCommandTimeout", 180);

        if (TestConfiguration.UseBrowserStack && (platform == PlatformKind.iOS || platform == PlatformKind.Android))
        {
            TestConfiguration.ValidateBrowserStackCredentials();
            var bsAppUrl = !string.IsNullOrWhiteSpace(TestConfiguration.BrowserStackAppUrl)
                ? TestConfiguration.BrowserStackAppUrl!
                : appPath;

            if (!bsAppUrl.StartsWith("bs://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"BrowserStack execution requires an uploaded BrowserStack application identifier ('bs://...'). Set ORBIT3D_BROWSERSTACK_APP after uploading the current Release package.");
            }

            options.PlatformName = platform == PlatformKind.iOS ? "ios" : "android";
            options.AutomationName = platform == PlatformKind.iOS ? "XCUITest" : "UiAutomator2";
            options.App = bsAppUrl;

            var deviceName = platform == PlatformKind.iOS
                ? TestConfiguration.IosDeviceName
                : (TestConfiguration.AndroidDeviceName ?? "Samsung Galaxy S24");
            var osVersion = platform == PlatformKind.iOS
                ? TestConfiguration.IosOsVersion
                : "14.0";

            options.AddAdditionalAppiumOption("deviceName", deviceName);
            options.AddAdditionalAppiumOption("platformVersion", osVersion);

            if (platform == PlatformKind.iOS)
            {
                options.AddAdditionalAppiumOption("bundleId", resolvedAppId);
                options.AddAdditionalAppiumOption("autoAcceptAlerts", true);
            }
            else
            {
                options.AddAdditionalAppiumOption("appPackage", resolvedAppId);
                options.AddAdditionalAppiumOption("autoGrantPermissions", true);
            }

            var bstackOptions = new Dictionary<string, object>
            {
                ["userName"] = TestConfiguration.BrowserStackUsername!,
                ["accessKey"] = TestConfiguration.BrowserStackAccessKey!,
                ["deviceName"] = deviceName,
                ["osVersion"] = osVersion,
                ["projectName"] = TestConfiguration.BrowserStackProjectName,
                ["buildName"] = TestConfiguration.BrowserStackBuildName,
                ["sessionName"] = TestConfiguration.BrowserStackSessionName,
                ["appiumVersion"] = "2.19.0",
                ["realMobile"] = true,
                ["debug"] = true,
                ["networkLogs"] = true,
                ["deviceLogs"] = true,
                ["video"] = true,
                ["idleTimeout"] = 180
            };

            options.AddAdditionalOption("bstack:options", bstackOptions);
            return options;
        }

        switch (platform)
        {
            case PlatformKind.Android:
                options.PlatformName = "Android";
                options.AutomationName = "UiAutomator2";
                options.App = appPath;
                options.AddAdditionalAppiumOption("appPackage", resolvedAppId);
                options.AddAdditionalAppiumOption("appActivity", "crc642279f0ce6b63d917.MainActivity");
                options.AddAdditionalAppiumOption("autoGrantPermissions", true);
                if (!string.IsNullOrEmpty(TestConfiguration.AndroidDeviceName))
                    options.AddAdditionalAppiumOption("deviceName", TestConfiguration.AndroidDeviceName);
                return options;

            case PlatformKind.Windows:
                options.PlatformName = "Windows";
                options.AutomationName = "Windows";
                options.App = appPath;
                options.AddAdditionalAppiumOption("ms:waitForAppLaunch", "15");
                return options;

            case PlatformKind.iOS:
                options.PlatformName = "iOS";
                options.AutomationName = "XCUITest";
                options.App = appPath;
                options.AddAdditionalAppiumOption("bundleId", resolvedAppId);
                if (!string.IsNullOrEmpty(TestConfiguration.IosSimulatorUdid))
                    options.AddAdditionalAppiumOption("udid", TestConfiguration.IosSimulatorUdid);
                return options;

            case PlatformKind.MacCatalyst:
                options.PlatformName = "Mac";
                options.AutomationName = "Mac2";
                options.App = appPath;
                options.AddAdditionalAppiumOption("bundleId", resolvedAppId);
                return options;

            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform for Appium driver creation.");
        }
    }

    /// <summary>
    /// Verifies that the local Appium server is reachable before attempting a session.
    /// </summary>
    public static void ValidateServerReachable(Uri serverUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var statusUrl = new Uri(serverUrl, "status");
            var response = client.GetAsync(statusUrl).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
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
                "Ensure Appium is running: 'appium --address 127.0.0.1 --port 4723' or configure BrowserStack environment variables.\n" +
                $"Underlying error: {ex.Message}", ex);
        }
    }
}
