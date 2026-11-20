using System.IO;
using System.Windows.Media.Imaging;

namespace FamVault.App.Services;

internal static class FamilyPreviewImageService
{
    public static BitmapImage FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;

        using (MemoryStream stream = new MemoryStream(bytes))
        {
            BitmapImage image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();

            image.Freeze();

            return image;
        }
    }
}
