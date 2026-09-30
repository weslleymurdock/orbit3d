using Orbit3D.Graphics.Silk;

namespace SilkTriangleSample;

public sealed class GraphicSurfacePage : ContentPage
{
    private readonly SilkTriangleDiagnostic _diagnostic;

    public GraphicSurfacePage()
    {
        
        var surface = new SilkGraphicsSurface
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _diagnostic = new SilkTriangleDiagnostic(surface);
        Content = surface;
    }
}