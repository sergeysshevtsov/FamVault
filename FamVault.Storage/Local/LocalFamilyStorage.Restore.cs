using FamVault.Core.Models;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    public Task RestoreRevisionAsync(Guid trashEntryId, CancellationToken cancellationToken)
    {
        if (trashEntryId == Guid.Empty)
            throw new ArgumentException("Trash entry ID cannot be empty.", nameof(trashEntryId));

        return RestoreEntriesAsync(
            entries => [.. entries.Where(entry => entry.Id == trashEntryId)],
            cancellationToken);
    }

    public Task RestoreDeletionGroupAsync(Guid deletionGroupId, CancellationToken cancellationToken)
    {
        if (deletionGroupId == Guid.Empty)
            throw new ArgumentException("Deletion group ID cannot be empty.", nameof(deletionGroupId));

        return RestoreEntriesAsync(
            entries => [.. entries.Where(entry => entry.DeletionGroupId == deletionGroupId)],
            cancellationToken);
    }

    private async Task RestoreEntriesAsync(Func<List<TrashEntry>, List<TrashEntry>> selectEntries, CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<TrashEntry> availableEntries = await ReadTrashEntriesAsync(cancellationToken).ConfigureAwait(false);

            List<TrashEntry> entries = selectEntries(availableEntries);

            if (entries.Count == 0)
                throw new KeyNotFoundException("No matching entries were found in Trash.");

            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);
            List<RestoreFileOperation> operations = [];
            HashSet<Guid> incomingRevisionIds = [];

            foreach (TrashEntry entry in entries)
            {
                if (entry.Revision == null || entry.Revision.Id != entry.RevisionId || entry.Revision.FamilyId != entry.FamilyId)
                    throw new InvalidDataException("Trash revision metadata is inconsistent.");

                if (!string.Equals(entry.OriginalRelativePath, entry.Revision.RelativePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Trash revision path does not match metadata.");

                if (!incomingRevisionIds.Add(entry.RevisionId))
                    throw new InvalidDataException("Duplicate revision IDs in Trash selection.");

                bool revisionExists = catalog
                    .SelectMany(family => family.Revisions)
                    .Any(revision => revision.Id == entry.RevisionId);

                if (revisionExists)
                    throw new InvalidOperationException($"Revision {entry.RevisionId} already exists.");

                string originalRfa = paths.ResolveRevisionPath(entry.OriginalRelativePath);
                string originalPreview = Path.ChangeExtension(originalRfa, ".png");
                string trashRfa = trashPaths.GetFamilyFilePath(entry.Id);
                string trashPreview = trashPaths.GetPreviewFilePath(entry.Id);

                if (!File.Exists(trashRfa))
                    throw new FileNotFoundException("RFA file is missing from Trash.", trashRfa);

                if (File.Exists(originalRfa))
                    throw new IOException($"RFA destination already exists: {originalRfa}");

                if (File.Exists(originalPreview))
                    throw new IOException($"Preview destination already exists: {originalPreview}");

                operations.Add(new RestoreFileOperation
                {
                    Entry = entry,
                    TrashRfaPath = trashRfa,
                    OriginalRfaPath = originalRfa,
                    TrashPreviewPath = trashPreview,
                    OriginalPreviewPath = originalPreview
                });
            }

            if (operations.Select(item => item.OriginalRfaPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != operations.Count)
                throw new InvalidDataException("Multiple revisions have the same destination path.");

            if (operations.Select(item => item.OriginalPreviewPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != operations.Count)
                throw new InvalidDataException("Multiple previews have the same destination path.");

            foreach (IGrouping<Guid, TrashEntry> group in entries.GroupBy(entry => entry.FamilyId))
            {
                TrashEntry firstEntry = group.First();

                FamilyEntry family = catalog.FirstOrDefault(item => item.Id == group.Key);

                if (family == null)
                {
                    family = new FamilyEntry
                    {
                        Id = firstEntry.FamilyId,
                        Name = firstEntry.FamilyName,
                        Category = firstEntry.Category,
                        Description = firstEntry.FamilyDescription,
                        IsFavorite = firstEntry.IsFavorite,
                        CreatedAtUtc = firstEntry.FamilyCreatedAtUtc,
                        Revisions = new List<FamilyRevision>()
                    };

                    catalog.Add(family);
                }

                foreach (TrashEntry entry in group)
                    family.Revisions.Add(entry.Revision);
            }

            cancellationToken.ThrowIfCancellationRequested();

            List<RestoreFileOperation> startedOperations = [];

            try
            {
                foreach (RestoreFileOperation operation in operations)
                {
                    startedOperations.Add(operation);

                    string directory = Path.GetDirectoryName(operation.OriginalRfaPath);

                    Directory.CreateDirectory(directory);

                    File.Move(operation.TrashRfaPath, operation.OriginalRfaPath);

                    operation.RfaMoved = true;

                    if (File.Exists(operation.TrashPreviewPath))
                    {
                        File.Move(operation.TrashPreviewPath, operation.OriginalPreviewPath);
                        operation.PreviewMoved = true;
                    }
                }
                await WriteCatalogAsync(catalog, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception originalException)
            {
                List<Exception> rollbackErrors = [];

                for (int index = startedOperations.Count - 1; index >= 0; index--)
                {
                    RestoreFileOperation operation = startedOperations[index];

                    try
                    {
                        if (operation.PreviewMoved)
                        {
                            File.Move(operation.OriginalPreviewPath, operation.TrashPreviewPath);
                        }

                        if (operation.RfaMoved)
                        {
                            File.Move(operation.OriginalRfaPath, operation.TrashRfaPath);
                        }
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackErrors.Add(rollbackException);
                    }
                }

                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, originalException);
                    throw new AggregateException("Restore failed and rollback was incomplete.", rollbackErrors);
                }

                throw;
            }

            foreach (RestoreFileOperation operation in operations)
            {
                try
                {
                    string directory = trashPaths.GetEntryDirectory(operation.Entry.Id);

                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch (IOException exception)
                {
                    System.Diagnostics.Debug.WriteLine($"FamVault Trash cleanup: {exception}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    System.Diagnostics.Debug.WriteLine($"FamVault Trash cleanup: {exception}");
                }
            }
        }
        finally
        {
            catalogLock.Release();
        }
    }

    private sealed class RestoreFileOperation
    {
        public TrashEntry Entry { get; set; }

        public string TrashRfaPath { get; set; }
        public string OriginalRfaPath { get; set; }

        public string TrashPreviewPath { get; set; }
        public string OriginalPreviewPath { get; set; }

        public bool RfaMoved { get; set; }
        public bool PreviewMoved { get; set; }
    }
}
