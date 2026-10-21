using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Abstract base class for Page Objects with explicit waits, stable interaction helpers, and diagnostics.
/// </summary>
public abstract class BasePage
{
    protected readonly AppiumDriver Driver;
    protected readonly PlatformKind Platform;

    protected BasePage(AppiumDriver driver, PlatformKind platform)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Platform = platform;
    }

    /// <summary>
    /// Explicitly waits until an element matching the selector is present and visible.
    /// </summary>
    public AppiumElement WaitForElement(By by, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TestConfiguration.DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < limit)
        {
            try
            {
                var elements = Driver.FindElements(by);
                if (elements.Count > 0 && elements[0].Displayed)
                    return (AppiumElement)elements[0];
            }
            catch (WebDriverException)
            {
                // Element not attached or DOM being updated; retry within bounded timeout
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Element matching '{by}' was not visible after {limit.TotalSeconds} seconds on {Platform}.");
    }

    /// <summary>
    /// Explicitly waits until an element's text satisfies the predicate.
    /// </summary>
    public string WaitForText(By by, Func<string, bool> predicate, string conditionDescription, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TestConfiguration.DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();
        string lastText = string.Empty;

        while (stopwatch.Elapsed < limit)
        {
            try
            {
                var element = WaitForElement(by, TimeSpan.FromSeconds(2));
                lastText = element.Text;
                if (predicate(lastText))
                    return lastText;
            }
            catch (Exception)
            {
                // Continue waiting until bounded timeout
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException(
            $"Element matching '{by}' did not satisfy condition '{conditionDescription}' within {limit.TotalSeconds}s on {Platform}. " +
            $"Last observed text: '{lastText}'.");
    }

    /// <summary>
    /// Checks whether an element matching the selector is currently visible without throwing.
    /// </summary>
    public bool IsElementDisplayed(By by)
    {
        try
        {
            var elements = Driver.FindElements(by);
            return elements.Count > 0 && elements[0].Displayed;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the text of the element matching the selector.
    /// </summary>
    public string GetElementText(By by, TimeSpan? timeout = null)
    {
        var element = WaitForElement(by, timeout);
        return element.Text;
    }

    /// <summary>
    /// Clicks the element matching the selector.
    /// </summary>
    public void ClickElement(By by, TimeSpan? timeout = null)
    {
        var element = WaitForElement(by, timeout);
        element.Click();
    }

    /// <summary>
    /// Captures a named visual screenshot.
    /// </summary>
    public string? CaptureScreenshot(string label) =>
        DiagnosticsCollector.CaptureScreenshot(Driver, Platform, label);
}

