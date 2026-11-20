using System.ComponentModel;
using System.Windows.Media;

namespace FamVault.App.UI.Models;

public sealed class FamilyLibraryItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public Guid FamilyId { get; set; }

    public Guid RevisionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int RevitVersion { get; set; }

    public int RevisionNumber { get; set; }

    public long FileSize { get; set; }

    public int TypeCount { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string SizeDisplay =>
        FileSize >= 1048576 ? $"{FileSize / 1048576d:F2} MB" : $"{FileSize / 1024d:F1} KB";

    public string DateDisplay =>
        CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string RevisionDisplay =>
        $"Rev. {RevisionNumber}";

    private ImageSource preview;

    public ImageSource Preview
    {
        get => preview;
        set
        {
            if (ReferenceEquals(preview, value))
                return;

            preview = value;
            OnPropertyChanged(nameof(Preview));
        }
    }
}
