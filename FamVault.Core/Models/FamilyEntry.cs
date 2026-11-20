namespace FamVault.Core.Models;

public sealed class FamilyEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<FamilyRevision> Revisions { get; set; } = [];
}