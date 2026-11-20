namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    public async Task<IReadOnlyList<TrashEntry>> GetDeletedItemsAsync(CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<TrashEntry> entries = await ReadTrashEntriesAsync(cancellationToken).ConfigureAwait(false);
            return [.. entries.OrderByDescending(item => item.DeletedAtUtc)];
        }
        finally
        {
            catalogLock.Release();
        }
    }

    private async Task<List<TrashEntry>> ReadTrashEntriesAsync(CancellationToken cancellationToken)
    {
        List<TrashEntry> entries = [];

        if (!Directory.Exists(trashPaths.RootDirectory))
            return entries;

        string[] directories = Directory.GetDirectories(trashPaths.RootDirectory);

        foreach (string directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string directoryName = Path.GetFileName(directory);

            if (!Guid.TryParseExact(directoryName, "N", out Guid trashEntryId))
                continue;

            string metadataPath = trashPaths.GetMetadataPath(trashEntryId);

            if (!File.Exists(metadataPath))
                continue;

            TrashEntry entry = await trashMetadataService.ReadAsync(trashEntryId, cancellationToken).ConfigureAwait(false);
            entries.Add(entry);
        }

        return entries;
    }
    private async Task<TrashEntry> ValidateTrashEntryAsync(Guid trashEntryId, CancellationToken cancellationToken)
    {
        TrashEntry entry = await trashMetadataService.ReadAsync(trashEntryId, cancellationToken).ConfigureAwait(false);

        string trashDirectory = trashPaths.GetEntryDirectory(trashEntryId);

        if (!Directory.Exists(trashDirectory))
            throw new DirectoryNotFoundException($"Trash directory not found: {trashDirectory}");

        string rfaPath = trashPaths.GetFamilyFilePath(trashEntryId);

        if (!File.Exists(rfaPath))
            throw new FileNotFoundException("Trash entry has no RFA file. It may be an incomplete operation.", rfaPath);

        if (entry.Revision == null || entry.Revision.Id != entry.RevisionId || entry.Revision.FamilyId != entry.FamilyId)
            throw new InvalidDataException("Trash metadata is inconsistent.");

        return entry;
    }
}
