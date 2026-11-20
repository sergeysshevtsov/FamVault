public sealed class FamilyLoadRequest
{
    public Guid FamilyId { get; set; }

    public Guid RevisionId { get; set; }

    public string FamilyName { get; set; } = string.Empty;

    public int RevitVersion { get; set; }
}