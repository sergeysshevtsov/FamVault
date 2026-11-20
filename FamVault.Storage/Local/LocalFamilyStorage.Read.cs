using FamVault.Core.Models;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    public async Task<Stream> OpenReadAsync(Guid familyId, Guid revisionId, CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

            FamilyRevision revision = catalog
                .Where(family => family.Id == familyId)
                .SelectMany(family => family.Revisions)
                .FirstOrDefault(item => item.Id == revisionId)
                ?? throw new FileNotFoundException(
                    "Family revision not found.");

            string filePath = paths.ResolveRevisionPath(
                revision.RelativePath);

            cancellationToken.ThrowIfCancellationRequested();

            return new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous);
        }
        finally
        {
            catalogLock.Release();
        }
    }

    public async Task<byte[]> GetPreviewAsync(Guid familyId, Guid revisionId, CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

            FamilyEntry family = catalog.FirstOrDefault(item => item.Id == familyId);

            FamilyRevision revision = family.Revisions.FirstOrDefault(item => item.Id == revisionId);

            if (revision == null)
                return [];

            string rfaPath = paths.ResolveRevisionPath(revision.RelativePath);
            string previewPath = Path.ChangeExtension(rfaPath, ".png");

            if (!File.Exists(previewPath))
                return [];

            using (FileStream stream = new FileStream(
                 previewPath,
                 FileMode.Open,
                 FileAccess.Read,
                 FileShare.Read,
                 81920,
                 FileOptions.Asynchronous))
            using (MemoryStream memory = new MemoryStream())
            {
                await stream.CopyToAsync(
                    memory,
                    81920,
                    cancellationToken).ConfigureAwait(false);

                return memory.ToArray();
            }
        }
        finally
        {
            catalogLock.Release();
        }
    }

    public async Task<byte[]> GetTrashPreviewAsync(Guid trashEntryId, CancellationToken cancellationToken)
    {
        if (trashEntryId == Guid.Empty)
            return [];

        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string previewPath = trashPaths.GetPreviewFilePath(trashEntryId);

            if (!File.Exists(previewPath))
                return [];

            await trashMetadataService.ReadAsync(trashEntryId, cancellationToken).ConfigureAwait(false);

            using (FileStream stream = new FileStream(
                previewPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous))
            using (MemoryStream memory = new MemoryStream())
            {
                await stream.CopyToAsync(
                    memory,
                    81920,
                    cancellationToken).ConfigureAwait(false);

                return memory.ToArray();
            }
        }
        finally
        {
            catalogLock.Release();
        }
    }
}
