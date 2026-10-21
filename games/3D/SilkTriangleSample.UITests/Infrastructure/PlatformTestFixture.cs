using OpenQA.Selenium.Appium;

namespace SilkTriangleSample.UITests.Infrastructure;

/// <summary>
/// Lifecycle fixture that manages Appium session initialization, artifact resolution, and deterministic disposal.
/// </summary>
public abstract class PlatformTestFixture : IAsyncLifetime
{
    public PlatformKind Platform { get; }
    public AppiumDriver? Driver { get; private set; }
    public AppArtifactManifest? Manifest { get; private set; }

    protected PlatformTestFixture(PlatformKind platform)
    {
        Platform = platform;
    }

    public virtual ValueTask InitializeAsync()
    {
        try
        {
            Manifest = AppArtifactLocator.ResolveArtifact(Platform, TestConfiguration.Configuration);
            Driver = AppiumDriverFactory.CreateDriver(Platform, Manifest.ArtifactPath);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Fixture] Failed to initialize {Platform} driver session: {ex.Message}");
            throw;
        }
    }

    public virtual ValueTask DisposeAsync()
    {
        if (Driver is not null)
        {
            try
            {
                Driver.Quit();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Fixture] Warning: Driver.Quit() threw on {Platform}: {ex.Message}");
            }
            finally
            {
                Driver.Dispose();
                Driver = null;
            }
        }

        return ValueTask.CompletedTask;
    }
}

