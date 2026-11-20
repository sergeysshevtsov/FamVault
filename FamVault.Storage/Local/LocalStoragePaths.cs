namespace FamVault.Storage.Local;

internal sealed class LocalStoragePaths
{
    public string Root { get; }
    public string Families { get; }
    public string Trash { get; }
    public string Catalog { get; }

    public LocalStoragePaths(string root)
    {
        Root = Path.GetFullPath(root);
        Families = Path.Combine(Root, "Families");
        Trash = Path.Combine(Root, ".Trash");
        Catalog = Path.Combine(Root, "catalog.json");
    }

    public static string GetDefaultRoot()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "FamVault");
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Families);
        Directory.CreateDirectory(Trash);
    }

    public string GetRevisionPath(Guid familyId, int revitVersion, Guid revisionId) =>
        Path.Combine(Families, familyId.ToString("N"), revitVersion.ToString(), revisionId.ToString("N") + ".rfa");

    public string GetRelativePath(string fullPath)
    {
        string prefix = Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File is outside the storage root.");

        return fullPath.Substring(prefix.Length).Replace('\\', '/');
    }

    public string ResolveRevisionPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("Invalid relative path.");

        string fullPath = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = Families.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Revision path is outside the Families folder.");

        return fullPath;
    }
}
