
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;

namespace FamVault.App.Services;

internal sealed class RevitFamilyLoadService
{
    private readonly IFamilyStorage storage;

    public RevitFamilyLoadService(IFamilyStorage storage)
    {
        this.storage = storage ??
            throw new ArgumentNullException(nameof(storage));
    }

    public IReadOnlyList<string> LoadFamilies(Document document, IReadOnlyList<FamilyLoadRequest> requests)
    {
        if (document.IsFamilyDocument)
            throw new InvalidOperationException("Loading into family documents is not supported.");

        if (document.IsModifiable || document.IsReadOnly)
            throw new InvalidOperationException("The target project must be editable.");

        int currentVersion = int.Parse(document.Application.VersionNumber);

        List<string> results = [];
        HashSet<string> existingNames = new(
            new FilteredElementCollector(document)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Select(family => family.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (FamilyLoadRequest request in requests)
        {
            List<Family> existingFamilies = [..
                new FilteredElementCollector(document)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(family =>
                    string.Equals(
                        family.Name,
                        request.FamilyName,
                        StringComparison.OrdinalIgnoreCase))];

            TaskDialog.Show("FamVault", $"Existing families: {existingFamilies.Count}");

            if (request.RevitVersion > currentVersion)
            {
                results.Add($"{request.FamilyName}: incompatible version");
                continue;
            }

            if (existingNames.Contains(request.FamilyName))
            {
                results.Add($"{request.FamilyName}: already exists");
                continue;
            }

            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "FamVault", Guid.NewGuid().ToString("N"));
            bool preserveTemporaryFile = false;

            try
            {
                string temporaryFile = CopyFamilyToTemporaryFile(request, temporaryDirectory);

                Family loadedFamily;
                bool loaded = false;

                using (Transaction transaction = new(document, $"FamVault - Load {request.FamilyName}"))
                {
                    transaction.Start();

                    FamilyLoadOptions options = new(overwrite: false);

                    loaded = document.LoadFamily(temporaryFile, options, out loadedFamily);
                    if (loaded && loadedFamily != null)
                    {
                        TransactionStatus status = transaction.Commit();

                        if (status != TransactionStatus.Committed)
                        {
                            loaded = false;
                            loadedFamily = null;
                        }
                    }
                    else
                    {
                        transaction.RollBack();
                    }
                }

                if (loaded && loadedFamily != null)
                {
                    existingNames.Add(loadedFamily.Name);
                    results.Add($"{request.FamilyName}: loaded");
                }
                else
                {
                    preserveTemporaryFile = true;
                    results.Add($"{request.FamilyName}: LoadFamily returned false. Temporary RFA: {temporaryFile}");
                }
            }
            catch (Exception exception)
            {
                results.Add($"{request.FamilyName}: {exception.Message}");
            }
            finally
            {
                if (!preserveTemporaryFile)
                {
                    try
                    {
                        if (Directory.Exists(temporaryDirectory))
                            Directory.Delete(temporaryDirectory, true);
                    }
                    catch (IOException)
                    {
                        // Ignore temporary cleanup errors.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore temporary cleanup errors.
                    }
                }
            }
        }

        return results;
    }

    private string CopyFamilyToTemporaryFile(FamilyLoadRequest request, string temporaryDirectory)
    {
        Directory.CreateDirectory(temporaryDirectory);

        string safeName = string.Concat(request.FamilyName.Split(Path.GetInvalidFileNameChars()));

        if (string.IsNullOrWhiteSpace(safeName))
            safeName = request.FamilyId.ToString("N");

        string temporaryFile = Path.Combine(temporaryDirectory, safeName + ".rfa");
        Task.Run(async () =>
        {
            using Stream source = await storage.OpenReadAsync(request.FamilyId, request.RevisionId, CancellationToken.None).ConfigureAwait(false);
            using FileStream destination = new(
                temporaryFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await source.CopyToAsync(
                destination,
                81920,
                CancellationToken.None).ConfigureAwait(false);
            await destination.FlushAsync(
                CancellationToken.None).ConfigureAwait(false);
        }).GetAwaiter().GetResult();

        FileInfo fileInfo = new(temporaryFile);
        if (!fileInfo.Exists || fileInfo.Length == 0)
            throw new IOException("Temporary family file was not created correctly.");

        return temporaryFile;
    }
}
