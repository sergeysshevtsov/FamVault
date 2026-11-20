using Autodesk.Revit.DB;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FamVault.App.Services;

internal static class FamilyPreviewService
{
    private const int PreviewSize = 256;

    public static byte[] CreatePreview(Family family)
    {
        if (family == null)
            throw new ArgumentNullException(nameof(family));

        Document document = family.Document;

        List<FamilySymbol> symbols = [.. family.GetFamilySymbolIds()
            .Select(id => document.GetElement(id))
            .OfType<FamilySymbol>()
            .OrderBy(symbol => symbol.Name)];

        foreach (FamilySymbol symbol in symbols)
        {
            try
            {
                using (Bitmap bitmap = symbol.GetPreviewImage(new Size(PreviewSize, PreviewSize)))
                {
                    if (bitmap == null)
                        continue;

                    using (MemoryStream stream = new MemoryStream())
                    {
                        bitmap.Save(stream, ImageFormat.Png);
                        return stream.ToArray();
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }
        }

        return [];
    }
}
