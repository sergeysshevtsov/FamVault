using FamVault.Core.Models;
using System.Text.Json;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    public async Task<IReadOnlyList<FamilyEntry>> GetFamiliesAsync(CancellationToken cancellationToken)
    {
        await catalogLock.WaitAsync(cancellationToken);

        try
        {
            return await ReadCatalogAsync(cancellationToken);
        }
        finally
        {
            catalogLock.Release();
        }
    }

    private async Task<List<FamilyEntry>> ReadCatalogAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.Catalog))
            return [];

        using FileStream stream = new(
            paths.Catalog,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous);


        List<FamilyEntry> families = await JsonSerializer.DeserializeAsync<List<FamilyEntry>>(
            stream,
            jsonOptions,
            cancellationToken)
        .ConfigureAwait(false);
        return families ?? [];
    }

    private async Task WriteCatalogAsync(List<FamilyEntry> catalog, CancellationToken cancellationToken)
    {
        string catalogPath = paths.Catalog;
        string directory = Path.GetDirectoryName(catalogPath);

        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(directory, $"catalog.{Guid.NewGuid():N}.tmp");
        string backupPath = Path.Combine(directory, "catalog.backup.json");

        try
        {
            using (FileStream stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    catalog,
                    jsonOptions,
                    cancellationToken).ConfigureAwait(false);

                await stream.FlushAsync(
                    cancellationToken).ConfigureAwait(false);

                stream.Flush(true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(catalogPath))
                File.Replace(temporaryPath, catalogPath, backupPath);
            else
                File.Move(temporaryPath, catalogPath);
        }
        catch (IOException exception)
        {
            throw new IOException(
                $"Failed to update FamVault catalog.\n" +
                $"Catalog: {catalogPath}\n" +
                $"Temporary: {temporaryPath}\n" +
                $"Backup: {backupPath}\n" +
                $"Catalog exists: {File.Exists(catalogPath)}\n" +
                $"Temporary exists: {File.Exists(temporaryPath)}\n" +
                $"Backup exists: {File.Exists(backupPath)}\n" +
                $"Original error: {exception.Message}",
                exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task UpdateRevisionCommentAsync(Guid familyId, Guid revisionId, string comment, CancellationToken cancellationToken)
    {
        if (familyId == Guid.Empty)
            throw new ArgumentException("Family ID cannot be empty.", nameof(familyId));

        if (revisionId == Guid.Empty)
            throw new ArgumentException("Revision ID cannot be empty.", nameof(revisionId));

        await catalogLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

            FamilyEntry family = catalog.FirstOrDefault(item => item.Id == familyId)
                ?? throw new KeyNotFoundException($"Family '{familyId}' was not found.");

            FamilyRevision revision = family.Revisions.FirstOrDefault(item => item.Id == revisionId)
                ?? throw new KeyNotFoundException($"Revision '{revisionId}' was not found.");

            string newComment = comment ?? string.Empty;

            if (string.Equals(revision.Comment, newComment, StringComparison.Ordinal))
                return;

            revision.Comment = newComment;

            await WriteCatalogAsync(catalog, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            catalogLock.Release();
        }
    }
}
