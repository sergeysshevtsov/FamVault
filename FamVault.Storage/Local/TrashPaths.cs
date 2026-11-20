namespace FamVault.Storage.Local;

internal sealed class TrashPaths
{
    private readonly string rootDirectory;

    public TrashPaths(string libraryDirectory)
    {
        if (string.IsNullOrWhiteSpace(libraryDirectory))
            throw new ArgumentException("Library directory cannot be empty.", nameof(libraryDirectory));

        rootDirectory = Path.Combine(libraryDirectory, ".Trash");
    }

    public string RootDirectory => rootDirectory;

    public string GetEntryDirectory(Guid trashEntryId) =>
        Path.Combine(rootDirectory, trashEntryId.ToString("N"));

    public string GetMetadataPath(Guid trashEntryId) =>
        Path.Combine(GetEntryDirectory(trashEntryId), "metadata.json");

    public string GetFamilyFilePath(Guid trashEntryId) =>
        Path.Combine(GetEntryDirectory(trashEntryId), "family.rfa");
    public string GetPreviewFilePath(Guid trashEntryId) =>
        Path.Combine(GetEntryDirectory(trashEntryId), "preview.png");
}
