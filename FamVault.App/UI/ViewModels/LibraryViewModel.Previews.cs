using FamVault.App.UI.Models;
using System.Windows.Media;

namespace FamVault.App.UI.ViewModels;

public sealed partial class LibraryViewModel
{
    public bool TryGetCachedPreview(Guid revisionId, out ImageSource preview) =>
        previewCache.TryGetValue(revisionId, out preview);

    public void CachePreview(Guid revisionId, ImageSource preview)
    {
        if (revisionId == Guid.Empty || preview == null)
            return;
        previewCache[revisionId] = preview;
    }

    private void RestoreCachedPreviews()
    {
        foreach (FamilyVersionGroupViewModel versionGroup in VersionGroups)
            foreach (FamilyGroupViewModel family in versionGroup.Families)
                foreach (FamilyLibraryItem revision in family.Revisions)
                    if (previewCache.TryGetValue(revision.RevisionId, out ImageSource preview))
                        revision.Preview = preview;
    }


    private void RemoveInvalidCachedPreviews()
    {
        HashSet<Guid> availableRevisionIds = [.. allItems.Select(item => item.RevisionId)];

        List<Guid> invalidIds = [.. previewCache.Keys.Where(id => !availableRevisionIds.Contains(id))];

        foreach (Guid revisionId in invalidIds)
            previewCache.Remove(revisionId);
    }
}
