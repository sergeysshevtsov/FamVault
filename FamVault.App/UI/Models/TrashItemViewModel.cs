using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FamVault.App.UI.Models;

public sealed class TrashItemViewModel : INotifyPropertyChanged
{
    private ImageSource preview;

    public TrashEntry Entry { get; }

    public Guid Id => Entry.Id;

    public Guid DeletionGroupId => Entry.DeletionGroupId;

    public string FamilyName => Entry.FamilyName;

    public string Comment => Entry.Revision.Comment;

    public string RevisionDisplay =>
        $"Revit {Entry.Revision.RevitVersion} · " +
        $"Revision {Entry.Revision.RevisionNumber}";

    public string DeletedDisplay =>
        Entry.DeletedAtUtc.ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm");

    public ImageSource Preview
    {
        get => preview;
        set
        {
            if (ReferenceEquals(preview, value))
                return;

            preview = value;
            OnPropertyChanged();
        }
    }

    public TrashItemViewModel(TrashEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(
            nameof(entry));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
