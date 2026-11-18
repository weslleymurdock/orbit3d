using System.Globalization;
using AnimationSample.UITests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace AnimationSample.UITests.Shared;

/// <summary>
/// Shared Page Object for AnimationSurfacePage across Windows and Android Appium drivers.
/// </summary>
public sealed class AnimationSurfacePageObject : BasePage
{
    private readonly By surfaceSelector;
    private readonly By statusLabelSelector;
    private readonly By diagnosticsLabelSelector;
    private readonly By playBtnSelector;
    private readonly By pauseBtnSelector;
    private readonly By resumeBtnSelector;
    private readonly By stopBtnSelector;
    private readonly By loopBtnSelector;
    private readonly By speedBtnSelector;
    private readonly By recreateContextBtnSelector;

    public AnimationSurfacePageObject(AppiumDriver driver, PlatformKind platform)
        : base(driver, platform)
    {
        surfaceSelector = ByAccessibility(platform, "SilkGraphicsSurface");
        statusLabelSelector = ByAccessibility(platform, "StatusLabel");
        diagnosticsLabelSelector = ByAccessibility(platform, "DiagnosticsLabel");
        playBtnSelector = ByAccessibility(platform, "PlayBtn");
        pauseBtnSelector = ByAccessibility(platform, "PauseBtn");
        resumeBtnSelector = ByAccessibility(platform, "ResumeBtn");
        stopBtnSelector = ByAccessibility(platform, "StopBtn");
        loopBtnSelector = ByAccessibility(platform, "LoopBtn");
        speedBtnSelector = ByAccessibility(platform, "SpeedBtn");
        recreateContextBtnSelector = ByAccessibility(platform, "RecreateContextBtn");
    }

    private static By ByAccessibility(PlatformKind platform, string id) =>
        platform == PlatformKind.Android
            ? MobileBy.Id($"com.remotecodehub.animationsample:id/{id}")
            : MobileBy.AccessibilityId(id);

    public bool WaitForSurfaceVisible(TimeSpan? timeout = null) =>
        WaitForElement(surfaceSelector, timeout).Displayed;

    public string GetStatusText() => GetElementText(statusLabelSelector);

    public string GetDiagnosticsText() => GetElementText(diagnosticsLabelSelector);

    public Dictionary<string, string> GetDiagnostics() => ParseDiagnostics(GetDiagnosticsText());

    public static Dictionary<string, string> ParseDiagnostics(string text)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return dict;

        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx > 0 && idx < pair.Length - 1)
                dict[pair[..idx].Trim()] = pair[(idx + 1)..].Trim();
        }
        return dict;
    }

    public static double ParseDouble(IReadOnlyDictionary<string, string> diag, string key) =>
        diag.TryGetValue(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;

    public static int ParseInt(IReadOnlyDictionary<string, string> diag, string key) =>
        diag.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    public Dictionary<string, string> WaitForRenderingProgress(TimeSpan? timeout = null)
    {
        var text = WaitForText(
            diagnosticsLabelSelector,
            t =>
            {
                var d = ParseDiagnostics(t);
                return d.TryGetValue("Phase", out var p) && p == "Rendering"
                    && d.TryGetValue("Context", out var c) && c == "Ready"
                    && ParseInt(d, "DrawCalls") > 0
                    && ParseInt(d, "Frames") > 0;
            },
            "Phase=Rendering, Context=Ready, DrawCalls>0, Frames>0",
            timeout);
        return ParseDiagnostics(text);
    }

    public Dictionary<string, string> WaitForDiagnostics(
        Func<IReadOnlyDictionary<string, string>, bool> predicate,
        string description,
        TimeSpan? timeout = null)
    {
        var text = WaitForText(
            diagnosticsLabelSelector,
            t => predicate(ParseDiagnostics(t)),
            description,
            timeout);
        return ParseDiagnostics(text);
    }

    public void ClickPlay() => ClickButton(playBtnSelector);
    public void ClickPause() => ClickButton(pauseBtnSelector);
    public void ClickResume() => ClickButton(resumeBtnSelector);
    public void ClickStop() => ClickButton(stopBtnSelector);
    public void ClickLoop() => ClickButton(loopBtnSelector);
    public void ClickSpeed() => ClickButton(speedBtnSelector);
    public void ClickRecreateContext() => ClickButton(recreateContextBtnSelector);

    private void ClickButton(By selector)
    {
        var element = WaitForElement(selector);
        if (Platform == PlatformKind.Windows)
        {
            try
            {
                Driver.ExecuteScript("windows: invoke", new Dictionary<string, object>
                {
                    ["elementId"] = element.Id
                });
                return;
            }
            catch
            {
                // Fall back to standard WebDriver click if invoke pattern is unavailable.
            }
        }

        element.Click();
    }
}
