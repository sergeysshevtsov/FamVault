using System.Windows;

namespace FamVault.App.UI.Views;

public partial class EditRevisionCommentDialog : Window
{
    public string Comment =>
        CommentTextBox.Text.Trim();

    public EditRevisionCommentDialog(string comment)
    {
        InitializeComponent();

        CommentTextBox.Text = comment ?? string.Empty;
        CommentTextBox.Focus();
        CommentTextBox.CaretIndex = CommentTextBox.Text.Length;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
