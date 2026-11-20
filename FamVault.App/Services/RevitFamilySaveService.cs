using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamVault.Core.Models;
using System.IO;

namespace FamVault.App.Services;

internal sealed class RevitFamilySaveService(IFamilyStorage storage)
{
    private readonly IFamilyStorage storage = storage ??
            throw new ArgumentNullException(nameof(storage));

    public IReadOnlyList<Family> GetSelectedFamilies(UIDocument uiDocument)
    {
        Document document = uiDocument.Document;

        HashSet<ElementId> familyIds = [];
        List<Family> families = [];

        foreach (ElementId elementId in uiDocument.Selection.GetElementIds())
        {
            Element element = document.GetElement(elementId);
            Family family = null;

            if (element is FamilyInstance instance)
                family = instance.Symbol?.Family;
            else if (element is FamilySymbol symbol)
                family = symbol.Family;
            else if (element is Family selectedFamily)
                family = selectedFamily;

            if (family == null)
                continue;

            if (family.IsInPlace || !family.IsEditable)
                continue;

            if (familyIds.Add(family.Id))
                families.Add(family);
        }

        return families;
    }

    public IReadOnlyList<string> SaveSelected(UIDocument uiDocument)
    {
        IReadOnlyList<Family> families = GetSelectedFamilies(uiDocument);
        return SaveSelected(uiDocument, families, new Dictionary<ElementId, string>());
    }

    public IReadOnlyList<string> SaveSelected(UIDocument uiDocument, IReadOnlyList<Family> families, IReadOnlyDictionary<ElementId, string> comments)
    {
        Document document = uiDocument.Document;

        if (document.IsModifiable || document.IsReadOnly)
            throw new InvalidOperationException("The document must be editable and have no open transaction.");

        List<string> results = [];

        if (families.Count == 0)
            return results;

        int revitVersion = int.Parse(document.Application.VersionNumber);

        IReadOnlyList<FamilyEntry> existingFamilies =
            Task.Run(() => storage.GetFamiliesAsync(CancellationToken.None)).GetAwaiter().GetResult();

        foreach (Family family in families)
        {
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "FamVault", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            string temporaryFile = Path.Combine(temporaryDirectory, "family.rfa");
            Document familyDocument = null;

            try
            {
                familyDocument = document.EditFamily(family);
                SaveAsOptions options = new()
                {
                    OverwriteExistingFile = false,
                    MaximumBackups = 1
                };

                familyDocument.SaveAs(temporaryFile, options);
            }
            catch (Exception exception)
            {
                results.Add($"{family.Name}: export failed - " + exception.Message);
                continue;
            }
            finally
            {
                familyDocument?.Close(false);
            }

            try
            {
                string category = family.FamilyCategory?.Name ?? string.Empty;

                List<FamilyEntry> matches = [.. existingFamilies
                    .Where(item =>
                        string.Equals(
                            item.Name,
                            family.Name,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            item.Category,
                            category,
                            StringComparison.OrdinalIgnoreCase))];

                if (matches.Count > 1)
                {
                    results.Add($"{family.Name}: multiple catalog matches; skipped");
                    continue;
                }

                FamilyEntry entry = matches.Count == 1
                    ? matches[0]
                    : new FamilyEntry
                    {
                        Name = family.Name,
                        Category = category,
                        IsFavorite = true
                    };

                string comment = comments.TryGetValue(family.Id, out string value) ?
                    value ?? string.Empty : string.Empty;


                FamilyRevision revision = new()
                {
                    FamilyId = entry.Id,
                    RevitVersion = revitVersion,
                    TypeCount = family.GetFamilySymbolIds().Count,
                    Comment = comment,
                    CreatedAtUtc = DateTime.UtcNow
                };

                byte[] previewBytes = FamilyPreviewService.CreatePreview(family);

                using FileStream stream = new(temporaryFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                using MemoryStream previewStream = new(previewBytes, writable: false);

                Task.Run(async () =>
                {
                    await storage.SaveAsync(entry, revision, stream, previewStream, CancellationToken.None).ConfigureAwait(false);
                })
                .GetAwaiter()
                .GetResult();

                results.Add($"{family.Name}: saved");
            }
            catch (Exception exception)
            {
                results.Add($"{family.Name}: storage failed - " + exception.Message);
            }
            finally
            {
                try
                {
                    Directory.Delete(temporaryDirectory, true);
                }
                catch (IOException)
                {
                    // Temporary files can be cleaned later.
                }
                catch (UnauthorizedAccessException)
                {
                    // Do not hide the operation result.
                }
            }
        }

        return results;
    }


}
