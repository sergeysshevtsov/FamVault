using System.Text.Json;

namespace FamVault.Storage.Local;

internal sealed class TrashMetadataService(TrashPaths paths)
{
    private readonly TrashPaths paths = paths
            ?? throw new ArgumentNullException(nameof(paths));

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(TrashEntry entry, CancellationToken cancellationToken)
    {
        string directory = paths.GetEntryDirectory(entry.Id);

        Directory.CreateDirectory(directory);

        string metadataPath = paths.GetMetadataPath(entry.Id);

        using FileStream stream = new(
            metadataPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(
            stream,
            entry,
            jsonOptions,
            cancellationToken).ConfigureAwait(false);

        await stream.FlushAsync(
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<TrashEntry> ReadAsync(Guid trashEntryId, CancellationToken cancellationToken)
    {
        string metadataPath = paths.GetMetadataPath(trashEntryId);

        using FileStream stream = new(
            metadataPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous);

        TrashEntry entry = await JsonSerializer.DeserializeAsync<TrashEntry>(stream, jsonOptions, cancellationToken).ConfigureAwait(false) ??
            throw new InvalidDataException("Trash metadata is invalid.");

        if (entry.Id != trashEntryId)
            throw new InvalidDataException("Trash entry identifier does not match.");

        return entry;
    }
}
