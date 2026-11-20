using FamVault.Core.Models;

public interface IFamilyStorage
{
    StorageLocation Location { get; }

    Task<IReadOnlyList<FamilyEntry>> GetFamiliesAsync(
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        Guid familyId,
        Guid revisionId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        FamilyEntry family,
        FamilyRevision revision,
        Stream content,
        CancellationToken cancellationToken);

    Task SaveAsync(
        FamilyEntry family,
        FamilyRevision revision,
        Stream content,
        Stream preview,
        CancellationToken cancellationToken);

    Task UpdateRevisionCommentAsync(
        Guid familyId,
        Guid revisionId,
        string comment,
        CancellationToken cancellationToken);

    Task DeleteRevisionAsync(
        Guid familyId,
        Guid revisionId,
        CancellationToken cancellationToken);

    Task DeleteFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrashEntry>> GetDeletedItemsAsync(CancellationToken cancellationToken);

    Task<byte[]> GetPreviewAsync(
        Guid familyId,
        Guid revisionId,
        CancellationToken cancellationToken);

    Task RestoreRevisionAsync(
        Guid trashEntryId,
        CancellationToken cancellationToken);

    Task RestoreDeletionGroupAsync(
        Guid deletionGroupId,
        CancellationToken cancellationToken);

    Task PermanentlyDeleteAsync(
        Guid trashEntryId,
        CancellationToken cancellationToken);

    Task EmptyTrashAsync(
        CancellationToken cancellationToken);
    Task<byte[]> GetTrashPreviewAsync(
        Guid trashEntryId,
        CancellationToken cancellationToken);
}
