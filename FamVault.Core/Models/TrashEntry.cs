
using FamVault.Core.Models;

public sealed class TrashEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeletionGroupId { get; set; }

    public Guid FamilyId { get; set; }

    public Guid RevisionId { get; set; }

    public string FamilyName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string FamilyDescription { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public DateTime FamilyCreatedAtUtc { get; set; }

    public FamilyRevision Revision { get; set; } = new();

    public string OriginalRelativePath { get; set; } = string.Empty;

    public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;
}
