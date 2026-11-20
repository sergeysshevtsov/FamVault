using Autodesk.Revit.DB;
using FamVault.App.UI.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace FamVault.App.UI.Views;

public partial class SaveFamilyDialog : Window
{
    public ObservableCollection<SaveFamilyItemViewModel> Families { get; } = [];
    public IReadOnlyList<SaveFamilyItemViewModel> SelectedFamilies => [.. Families.Where(item => item.IsSelected)];

    public SaveFamilyDialog(IReadOnlyList<Family> families)
    {
        InitializeComponent();

        foreach (Family family in families)
        {
            SaveFamilyItemViewModel item = new(family);
            item.PropertyChanged += FamilyItem_PropertyChanged;
            Families.Add(item);
        }

        DataContext = this;
        UpdateSummary();
    }

    private void FamilyItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SaveFamilyItemViewModel.IsSelected))
            UpdateSummary();
    }

    private void UpdateSummary()
    {
        int selectedCount = Families.Count(item => item.IsSelected);
        SelectionSummary.Text = $"{selectedCount} of {Families.Count} selected";
        FooterSummary.Text = $"{selectedCount} families ready to save";
    }

    private void ApplyCommonComment_Click(object sender, RoutedEventArgs e)
    {
        string commonComment = CommonCommentTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(commonComment))
            return;

        foreach (SaveFamilyItemViewModel family in Families)
            if (family.IsSelected)
                family.Comment = commonComment;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFamilies.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one family.",
                "FamVault",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (SaveFamilyItemViewModel item in Families)
            item.PropertyChanged -= FamilyItem_PropertyChanged;

        base.OnClosed(e);
    }
}
