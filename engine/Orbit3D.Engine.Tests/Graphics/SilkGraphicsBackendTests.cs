using Microsoft.Maui.Hosting;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;
using Orbit3D.Graphics.Silk;

namespace Orbit3D.Engine.Tests.Graphics;

public class SilkGraphicsBackendTests
{
    [Fact]
    public void SilkGraphicsContext_RejectsContextThatIsNotCurrent()
    {
        Assert.Throws<InvalidOperationException>(() => new SilkGraphicsContext(
            _ => 0,
            () => false,
            () => { },
            isOpenGles: false));
    }

    [Fact]
    public void UseOrbit3DEngine_RegistersModelImporter()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseOrbit3DEngine();

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IModelImporter));
    }

    [Fact]
    public void UseSilkGraphics_WindowsRegistersEngineServices()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseSilkGraphics();

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IModelImporter));
    }

    [Fact]
    public void GameSceneView3D_ExposesSurfaceAndSceneOwner()
    {
        var view = new GameSceneView3D();

        Assert.NotNull(view.Surface);
        Assert.Null(view.Scene);
        Assert.Null(view.Device);
        Assert.Null(view.Renderer);
    }
}
