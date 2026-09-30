using Orbit3D.Engine;
using Orbit3D.Graphics.Silk;

namespace SilkTriangleSample;

public sealed class GraphicSurfacePage : ContentPage
{
    private readonly IModelImporter modelImporter;
    private readonly IAssetCache assetCache = new ModelAssetCache();
    private readonly SilkModelDiagnostic diagnostic;
    private readonly Label statusLabel;
    private bool importStarted;

    public GraphicSurfacePage(IModelImporter modelImporter)
    {
        this.modelImporter = modelImporter;
        var surface = new SilkGraphicsSurface
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        diagnostic = new SilkModelDiagnostic(surface);
        statusLabel = new Label
        {
            Text = "Importando poly.obj...",
            Margin = new Thickness(12),
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            TextColor = Colors.White,
            Background = new SolidColorBrush(Color.FromArgb("#C0000000"))
        };

        var layout = new Grid();
        layout.Children.Add(surface);
        layout.Children.Add(statusLabel);
        Content = layout;
        Console.WriteLine($"[SilkSurface] created handler={surface.Handler?.GetType().FullName ?? "<null>"}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        diagnostic.StatusChanged += OnDiagnosticStatusChanged;
        if (importStarted)
            return;
        importStarted = true;

        try
        {
            byte[] assetData;
            await using (var asset = await FileSystem.OpenAppPackageFileAsync("poly.obj"))
            using (var buffer = new MemoryStream())
            {
                await asset.CopyToAsync(buffer);
                assetData = buffer.ToArray();
            }

            var model = await Task.Run(() =>
            {
                using var stream = new MemoryStream(assetData, writable: false);
                var settings = new AssetImportSettings { GenerateNormals = true, Triangulate = true, JoinIdenticalVertices = true, FlipUVs = true };
                return assetCache.GetOrAdd("poly.obj", () => modelImporter.Import(stream, "obj"), settings);
            });

            var scene = new Scene3D();
            var rootNode = new Node3D { Name = "poly.obj", Model = model };
            rootNode.SetParent(scene.RootNode);
            var renderQueue = scene.BuildRenderQueue();

            diagnostic.SetModel(model);
            statusLabel.Text = $"Runtime scene ready: {renderQueue.Count} queued meshes";
        }
        catch (Exception exception)
        {
            statusLabel.Text = $"Falha ao importar poly.obj: {exception.Message}";
        }
    }

    protected override void OnDisappearing()
    {
        diagnostic.StatusChanged -= OnDiagnosticStatusChanged;
        base.OnDisappearing();
    }

    private void OnDiagnosticStatusChanged(string message)
    {
        var displayText = message.StartsWith("First frame submitted", StringComparison.Ordinal)
            ? "3D mesh rendered"
            : message;
        MainThread.BeginInvokeOnMainThread(() => statusLabel.Text = displayText);
    }
}