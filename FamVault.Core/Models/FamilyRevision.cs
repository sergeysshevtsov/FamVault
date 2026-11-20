namespace FamVault.Core.Models;

public sealed class FamilyRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FamilyId { get; set; }

    public int RevitVersion { get; set; }

    public int RevisionNumber { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public int TypeCount { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}