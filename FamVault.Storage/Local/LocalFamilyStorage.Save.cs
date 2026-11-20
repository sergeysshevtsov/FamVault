using FamVault.Core.Models;
using FamVault.Storage.Services;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage
{
    public async Task SaveAsync(FamilyEntry family, FamilyRevision revision, Stream content, CancellationToken cancellationToken)
    {
        if (family == null)
            throw new ArgumentNullException(nameof(family));

        if (revision == null)
            throw new ArgumentNullException(nameof(revision));

        if (content == null || !content.CanRead)
            throw new ArgumentException("Content stream must be readable.", nameof(content));

        if (family.Id == Guid.Empty || revision.Id == Guid.Empty || revision.RevitVersion < 2000)
            throw new ArgumentException("Invalid family or revision metadata.");

        if (revision.FamilyId != Guid.Empty && revision.FamilyId != family.Id)
            throw new InvalidOperationException("Revision belongs to another family.");

        await catalogLock.WaitAsync(cancellationToken);

        string destinationPath = paths.GetRevisionPath(family.Id, revision.RevitVersion, revision.Id);
        string temporaryPath = destinationPath + ".tmp";
        bool fileCommitted = false;

        try
        {
            List<FamilyEntry> catalog = await ReadCatalogAsync(cancellationToken);
            FamilyEntry existing = catalog.FirstOrDefault(item => item.Id == family.Id);

            if (existing == null)
            {
                existing = new FamilyEntry
                {
                    Id = family.Id,
                    Name = family.Name,
                    Category = family.Category,
                    Description = family.Description,
                    IsFavorite = family.IsFavorite,
                    CreatedAtUtc = family.CreatedAtUtc
                };

                catalog.Add(existing);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            using (FileStream destination = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await content.CopyToAsync(destination, 81920, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            FileInfo fileInfo = new(temporaryPath);

            if (fileInfo.Length == 0)
                throw new InvalidDataException("Cannot save an empty family file.");

            string hash = await FileHashService.ComputeSha256Async(temporaryPath, cancellationToken);
            FamilyRevision? existingRevision = existing.Revisions.FirstOrDefault(item =>
                item.RevitVersion == revision.RevitVersion && string.Equals(item.Sha256, hash, StringComparison.OrdinalIgnoreCase));
            if (existingRevision != null)
            {
                revision.Id = existingRevision.Id;
                revision.FamilyId = existingRevision.FamilyId;
                revision.RelativePath = existingRevision.RelativePath;
                revision.FileName = existingRevision.FileName;
                revision.RevisionNumber = existingRevision.RevisionNumber;
                if (!string.IsNullOrWhiteSpace(revision.Comment))
                {
                    existingRevision.Comment = revision.Comment;
                    await WriteCatalogAsync(catalog, CancellationToken.None);
                }
                File.Delete(temporaryPath);
                return;
            }

            if (existing.Revisions.Any(item => item.Id == revision.Id))
                throw new InvalidOperationException("Revision ID already exists.");

            int nextRevisionNumber = existing.Revisions
                .Where(item =>
                    item.RevitVersion == revision.RevitVersion)
                .Select(item => item.RevisionNumber)
                .DefaultIfEmpty(0)
                .Max() + 1;

            revision.FamilyId = family.Id;
            revision.RevisionNumber = nextRevisionNumber;
            revision.FileSize = fileInfo.Length;
            revision.Sha256 = hash;
            revision.FileName = revision.Id.ToString("N") + ".rfa";
            revision.RelativePath = paths.GetRelativePath(destinationPath);

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(temporaryPath, destinationPath);
            fileCommitted = true;

            existing.Revisions.Add(revision);

            await WriteCatalogAsync(catalog, CancellationToken.None);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            if (fileCommitted && File.Exists(destinationPath))
                File.Delete(destinationPath);

            throw;
        }
        finally
        {
            catalogLock.Release();
        }
    }

    public async Task SaveAsync(FamilyEntry family, FamilyRevision revision, Stream content, Stream preview, CancellationToken cancellationToken)
    {
        await SaveAsync(family, revision, content, cancellationToken).ConfigureAwait(false);

        if (preview == null || !preview.CanRead || (preview.CanSeek && preview.Length == 0))
            return;

        if (string.IsNullOrWhiteSpace(revision.RelativePath))
            return;

        string rfaPath = paths.ResolveRevisionPath(revision.RelativePath);
        string previewPath = Path.ChangeExtension(rfaPath, ".png");
        if (File.Exists(previewPath))
            return;

        string temporaryPreviewPath = previewPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream destination = new FileStream(
                temporaryPreviewPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await preview.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(previewPath))
                File.Move(temporaryPreviewPath, previewPath);
        }
        catch (IOException exception)
        {
            System.Diagnostics.Debug.WriteLine($"FamVault preview save: {exception}");
        }
        catch (UnauthorizedAccessException exception)
        {
            System.Diagnostics.Debug.WriteLine($"FamVault preview save: {exception}");
        }
        finally
        {
            if (File.Exists(temporaryPreviewPath))
                File.Delete(temporaryPreviewPath);
        }
    }
}
