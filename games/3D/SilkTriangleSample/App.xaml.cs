namespace SilkTriangleSample;

public partial class App : Application
{
	private readonly IServiceProvider services;

	public App(IServiceProvider services)
	{
		this.services = services;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
#if ANDROID || WINDOWS
	    return new Window(services.GetRequiredService<GraphicSurfacePage>());
#else
        return new Window(new AppShell());
#endif
	}
}