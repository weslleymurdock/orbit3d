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
    public void GameSceneView3D_ExposesSurfaceAndSceneOwner()
    {
        var view = new GameSceneView3D();

        Assert.NotNull(view.Surface);
        Assert.Null(view.Scene);
        Assert.Null(view.Device);
        Assert.Null(view.Renderer);
    }
}
