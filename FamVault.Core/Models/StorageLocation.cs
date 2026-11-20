namespace FamVault.Core.Models;

public sealed class StorageLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string ProviderId { get; set; } = "local";

    public string Location { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool IsDefault { get; set; }
}