using FamVault.Core.Models;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    private async Task<TrashEntry> MoveRevisionToTrashAsync(FamilyEntry family, FamilyRevision revision, Guid deletionGroupId, CancellationToken cancellationToken)
    {
        TrashEntry entry = new()
        {
            DeletionGroupId = deletionGroupId,

            FamilyId = family.Id,
            RevisionId = revision.Id,
            FamilyName = family.Name,
            Category = family.Category,
            FamilyDescription = family.Description,
            IsFavorite = family.IsFavorite,
            FamilyCreatedAtUtc = family.CreatedAtUtc,
            Revision = revision,
            OriginalRelativePath = revision.RelativePath,
            DeletedAtUtc = DateTime.UtcNow
        };

        string sourcePath = paths.ResolveRevisionPath(revision.RelativePath);
        string trashDirectory = trashPaths.GetEntryDirectory(entry.Id);
        string trashFile = trashPaths.GetFamilyFilePath(entry.Id);

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Revision file not found.", sourcePath);

        string sourcePreviewPath = Path.ChangeExtension(sourcePath, ".png");
        string trashPreviewPath = trashPaths.GetPreviewFilePath(entry.Id);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await trashMetadataService.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(sourcePath, trashFile);
            MoveFileIfExists(sourcePreviewPath, trashPreviewPath);
            return entry;
        }
        catch
        {
            if (File.Exists(trashPreviewPath))
                MoveFileIfExists(trashPreviewPath, sourcePreviewPath);

            if (File.Exists(trashFile))
                MoveFileIfExists(trashFile, sourcePath);

            if (File.Exists(sourcePath) && Directory.Exists(trashDirectory))
                Directory.Delete(trashDirectory, true);

            throw;
        }
    }

    private static void MoveFileIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
            return;

        if (File.Exists(destinationPath))
            throw new IOException($"Destination file already exists: {destinationPath}");

        string destinationDirectory = Path.GetDirectoryName(destinationPath) ?? throw new InvalidOperationException("Invalid destination path.");

        Directory.CreateDirectory(destinationDirectory);

        File.Move(sourcePath, destinationPath);
    }
}
