using FamVault.Core.Models;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    public async Task PermanentlyDeleteAsync(Guid trashEntryId, CancellationToken cancellationToken)
    {
        if (trashEntryId == Guid.Empty)
            throw new ArgumentException("Trash entry ID cannot be empty.", nameof(trashEntryId));

        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

            await PermanentlyDeleteCoreAsync(trashEntryId, catalog, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            catalogLock.Release();
        }
    }

    private async Task PermanentlyDeleteCoreAsync(Guid trashEntryId, List<FamilyEntry> catalog, CancellationToken cancellationToken)
    {
        TrashEntry entry = await ValidateTrashEntryAsync(trashEntryId, cancellationToken).ConfigureAwait(false);

        bool isActive = catalog
            .SelectMany(family => family.Revisions)
            .Any(revision => revision.Id == entry.RevisionId);

        if (isActive)
            throw new InvalidOperationException($"Revision {entry.RevisionId} is still active.");

        cancellationToken.ThrowIfCancellationRequested();

        string directory = trashPaths.GetEntryDirectory(trashEntryId);
        string rfaPath = trashPaths.GetFamilyFilePath(trashEntryId);
        string previewPath = trashPaths.GetPreviewFilePath(trashEntryId);
        string metadataPath = trashPaths.GetMetadataPath(trashEntryId);

        if (File.Exists(rfaPath))
            File.Delete(rfaPath);

        if (File.Exists(previewPath))
            File.Delete(previewPath);

        if (File.Exists(metadataPath))
            File.Delete(metadataPath);

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
            Directory.Delete(directory);
    }

    public async Task EmptyTrashAsync(CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<TrashEntry> entries = await ReadTrashEntriesAsync(cancellationToken).ConfigureAwait(false);

            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

            foreach (TrashEntry entry in entries)
            {
                await ValidateTrashEntryAsync(entry.Id, cancellationToken).ConfigureAwait(false);

                bool isActive = catalog
                    .SelectMany(family => family.Revisions)
                    .Any(revision => revision.Id == entry.RevisionId);

                if (isActive)
                    throw new InvalidOperationException($"Revision {entry.RevisionId} is still active.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            foreach (TrashEntry entry in entries)
                await PermanentlyDeleteCoreAsync(entry.Id, catalog, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            catalogLock.Release();
        }
    }
}
