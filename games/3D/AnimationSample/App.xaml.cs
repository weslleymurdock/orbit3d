namespace AnimationSample;

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
#if ANDROID || IOS || MACCATALYST || WINDOWS
		return new Window(services.GetRequiredService<AnimationSurfacePage>())
		{
			Title = "Orbit3D AnimationSample"
		};
#else
		return new Window(new AppShell());
#endif
	}
}
