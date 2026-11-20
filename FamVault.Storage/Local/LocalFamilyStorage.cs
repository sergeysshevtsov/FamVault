using FamVault.Core.Models;
using System.Text.Json;

namespace FamVault.Storage.Local;

public sealed partial class LocalFamilyStorage : IFamilyStorage
{
    private readonly LocalStoragePaths paths;
    private readonly SemaphoreSlim catalogLock = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly TrashPaths trashPaths;
    private readonly TrashMetadataService trashMetadataService;

    public StorageLocation Location { get; }

    public LocalFamilyStorage(StorageLocation location)
    {
        Location = location ??
            throw new ArgumentNullException(nameof(location));

        if (!string.Equals(location.ProviderId, "local", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Storage provider must be local.", nameof(location));

        string root = string.IsNullOrWhiteSpace(location.Location)
            ? LocalStoragePaths.GetDefaultRoot()
            : location.Location;

        paths = new LocalStoragePaths(root);
        paths.EnsureCreated();
        trashPaths = new TrashPaths(paths.Root);
        trashMetadataService = new TrashMetadataService(trashPaths);
    }
}
