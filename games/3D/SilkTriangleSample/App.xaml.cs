namespace SilkTriangleSample;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
#if ANDROID
        return new Window(new GraphicSurfacePage());
#else
        return new Window(new AppShell());
#endif
	}
}