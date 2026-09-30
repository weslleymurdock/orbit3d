using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;

namespace SilkTriangleSample;

internal static class SampleTextureLoader
{
#if WINDOWS
    public static async Task<Texture2D> LoadRgbaAsync(byte[] encodedImage)
    {
        ArgumentNullException.ThrowIfNull(encodedImage);
        if (encodedImage.Length == 0)
            throw new ArgumentException("Image data must not be empty.", nameof(encodedImage));

        using var stream = new MemoryStream(encodedImage, writable: false);
        using var randomAccessStream = stream.AsRandomAccessStream();
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Rgba8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Straight);

        var width = checked((int)bitmap.PixelWidth);
        var height = checked((int)bitmap.PixelHeight);
        var buffer = new Windows.Storage.Streams.Buffer(checked((uint)(width * height * 4)));
        bitmap.CopyToBuffer(buffer);
        var pixels = new byte[buffer.Length];
        using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
            reader.ReadBytes(pixels);

        return CreateTexture(width, height, pixels);
    }
#elif ANDROID
    public static Task<Texture2D> LoadRgbaAsync(byte[] encodedImage)
    {
        ArgumentNullException.ThrowIfNull(encodedImage);
        if (encodedImage.Length == 0)
            throw new ArgumentException("Image data must not be empty.", nameof(encodedImage));

        using var bitmap = Android.Graphics.BitmapFactory.DecodeByteArray(encodedImage, 0, encodedImage.Length)
            ?? throw new InvalidOperationException("Android could not decode the sample texture.");

        var width = bitmap.Width;
        var height = bitmap.Height;
        var argbPixels = new int[checked(width * height)];
        bitmap.GetPixels(argbPixels, 0, width, 0, 0, width, height);
        var pixels = new byte[checked(argbPixels.Length * 4)];
        for (var i = 0; i < argbPixels.Length; i++)
        {
            var argb = argbPixels[i];
            var offset = i * 4;
            pixels[offset] = (byte)(argb >> 16);
            pixels[offset + 1] = (byte)(argb >> 8);
            pixels[offset + 2] = (byte)argb;
            pixels[offset + 3] = (byte)(argb >> 24);
        }

        return Task.FromResult(CreateTexture(width, height, pixels));
    }
#else
    public static Task<Texture2D> LoadRgbaAsync(byte[] encodedImage) =>
        Task.FromException<Texture2D>(new PlatformNotSupportedException("The sample texture decoder is configured for Windows and Android."));
#endif

    private static Texture2D CreateTexture(int width, int height, byte[] pixels) => new()
    {
        Name = "toyota-gazoo-racing-wrt-gr-yaris-1-10.png",
        Usage = TextureUsage.BaseColor,
        Width = width,
        Height = height,
        PixelFormat = TextureFormat.Rgba8Srgb,
        Data = pixels
    };
}
