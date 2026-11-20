
using FamVault.App.UI.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FamVault.App.UI.ViewModels;

public sealed class TrashGroupViewModel : INotifyPropertyChanged
{
    private bool isExpanded = true;

    public Guid DeletionGroupId { get; }

    public string FamilyName { get; }
    public IEnumerable<TrashItemViewModel> Revisions1 { get; }
    public ObservableCollection<TrashItemViewModel> Revisions
    { get; }

    public int RevisionCount => Revisions.Count;

    public string Summary =>
        $"{RevisionCount} revision" + (RevisionCount == 1 ? "" : "s");

    public ImageSource Preview =>
        Revisions.FirstOrDefault(item => item.Preview != null)?.Preview;

    public bool CanRestoreAll =>
        DeletionGroupId != Guid.Empty && RevisionCount > 1;

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
                return;

            isExpanded = value;
            OnPropertyChanged();
        }
    }

    public TrashGroupViewModel(Guid deletionGroupId, string familyName, System.Collections.Generic.IEnumerable<TrashItemViewModel> revisions)
    {
        DeletionGroupId = deletionGroupId;
        FamilyName = familyName;
        Revisions1 = revisions;
        Revisions = new ObservableCollection<TrashItemViewModel>(revisions.OrderByDescending(item => item.Entry.Revision.RevisionNumber));

        foreach (TrashItemViewModel revision in Revisions)
            revision.PropertyChanged += Revision_PropertyChanged;
    }

    private void Revision_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrashItemViewModel.Preview))
            OnPropertyChanged(nameof(Preview));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
