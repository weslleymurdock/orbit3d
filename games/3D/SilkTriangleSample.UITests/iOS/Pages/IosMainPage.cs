using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using SilkTriangleSample.UITests.Infrastructure;

namespace SilkTriangleSample.UITests.iOS.Pages;

/// <summary>
/// Page Object encapsulating the iOS startup page (MainPage via AppShell).
/// Note: On iOS, SilkTriangleSample currently initializes AppShell with MainPage.
/// Stage 08 will integrate GraphicSurfacePage into iOS startup.
/// </summary>
public sealed class IosMainPage : BasePage
{
    private static readonly By HeadlineSelector = MobileBy.AccessibilityId("HeadlineLabel");
    private static readonly By SubHeadlineSelector = MobileBy.AccessibilityId("SubHeadlineLabel");
    private static readonly By CounterBtnSelector = MobileBy.AccessibilityId("CounterBtn");
    private static readonly By BotImageSelector = MobileBy.AccessibilityId("DotNetBotImage");

    public IosMainPage(AppiumDriver driver)
        : base(driver, PlatformKind.iOS)
    {
    }

    /// <summary>
    /// Waits until the initial headline label is visible.
    /// </summary>
    public string WaitForHeadline(TimeSpan? timeout = null)
    {
        return GetElementText(HeadlineSelector, timeout);
    }

    /// <summary>
    /// Gets the current text of the counter button.
    /// </summary>
    public string GetCounterButtonText()
    {
        return GetElementText(CounterBtnSelector);
    }

    /// <summary>
    /// Clicks the counter button.
    /// </summary>
    public void ClickCounterButton()
    {
        ClickElement(CounterBtnSelector);
    }

    /// <summary>
    /// Checks whether the dotnet bot image is displayed.
    /// </summary>
    public bool IsBotImageDisplayed() => IsElementDisplayed(BotImageSelector);

    /// <summary>
    /// Checks whether the counter button is displayed.
    /// </summary>
    public bool IsCounterButtonDisplayed() => IsElementDisplayed(CounterBtnSelector);
}

