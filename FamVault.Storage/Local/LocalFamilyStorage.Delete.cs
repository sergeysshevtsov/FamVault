using FamVault.Core.Models;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    public async Task DeleteRevisionAsync(Guid familyId, Guid revisionId, CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

            FamilyEntry family = catalog.FirstOrDefault(item => item.Id == familyId)
                ?? throw new KeyNotFoundException("Family not found.");

            FamilyRevision revision = family.Revisions.FirstOrDefault(item => item.Id == revisionId)
                ?? throw new KeyNotFoundException("Revision not found.");

            Guid deletionGroupId = Guid.NewGuid();

            TrashEntry trashEntry = await MoveRevisionToTrashAsync(
                family,
                revision,
                deletionGroupId,
                cancellationToken).ConfigureAwait(false);

            family.Revisions.Remove(revision);

            if (family.Revisions.Count == 0)
                catalog.Remove(family);

            try
            {
                await WriteCatalogAsync(catalog, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                RollbackTrashEntry(trashEntry);
                throw;
            }
        }
        finally
        {
            catalogLock.Release();
        }
    }

    public async Task DeleteFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

            FamilyEntry family = catalog.FirstOrDefault(item => item.Id == familyId)
                ?? throw new KeyNotFoundException($"Family '{familyId}' was not found.");

            foreach (FamilyRevision revision in family.Revisions)
            {
                string sourcePath = paths.ResolveRevisionPath(revision.RelativePath);

                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("Revision file was not found.", sourcePath);
            }

            cancellationToken.ThrowIfCancellationRequested();

            Guid deletionGroupId = Guid.NewGuid();

            List<TrashEntry> movedEntries = [];

            try
            {
                foreach (FamilyRevision revision in family.Revisions)
                {
                    TrashEntry entry =
                        await MoveRevisionToTrashAsync(
                            family,
                            revision,
                            deletionGroupId,
                            CancellationToken.None)
                        .ConfigureAwait(false);

                    movedEntries.Add(entry);
                }

                catalog.Remove(family);

                await WriteCatalogAsync(catalog, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception originalException)
            {
                List<Exception> rollbackErrors = [];

                for (int index = movedEntries.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        RollbackTrashEntry(movedEntries[index]);
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackErrors.Add(rollbackException);
                    }
                }

                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, originalException);
                    throw new AggregateException("Deleting the family failed, and some revisions could not be restored.", rollbackErrors);
                }

                throw;
            }
        }
        finally
        {
            catalogLock.Release();
        }
    }

    private void RollbackTrashEntry(TrashEntry entry)
    {
        string trashFile = trashPaths.GetFamilyFilePath(entry.Id);
        string originalFile = paths.ResolveRevisionPath(entry.OriginalRelativePath);
        string trashPreview = trashPaths.GetPreviewFilePath(entry.Id);
        string originalPreview = Path.ChangeExtension(originalFile, ".png");

        if (File.Exists(trashPreview))
            MoveFileIfExists(trashPreview, originalPreview);

        if (File.Exists(trashFile))
            MoveFileIfExists(trashFile, originalFile);
        else if (!File.Exists(originalFile))
            throw new FileNotFoundException("Neither Trash nor active RFA exists.", originalFile);

        string trashDirectory = trashPaths.GetEntryDirectory(entry.Id);
        if (Directory.Exists(trashDirectory))
            Directory.Delete(trashDirectory, true);
    }
}
