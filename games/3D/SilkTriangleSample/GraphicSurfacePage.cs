using Orbit3D.Engine;
using Orbit3D.Graphics.Silk;

namespace SilkTriangleSample;

public sealed class GraphicSurfacePage : ContentPage
{
    private readonly IModelImporter modelImporter;
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
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            TextColor = Colors.White,
            Background = new SolidColorBrush(Color.FromArgb("#C0000000"))
        };

        var layout = new Grid();
        layout.Children.Add(surface);
        layout.Children.Add(statusLabel);
        Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
                return modelImporter.Import(stream, "obj");
            });

            diagnostic.SetModel(model);
            statusLabel.Text = $"poly.obj: {model.Meshes.Count} mesh";
        }
        catch (Exception exception)
        {
            statusLabel.Text = $"Falha ao importar poly.obj: {exception.Message}";
        }
    }
}