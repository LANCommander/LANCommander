using Avalonia.Controls;
using LANCommander.Packager.Models;

namespace LANCommander.Packager.Views;

public partial class MetadataView : UserControl
{
    private readonly PackageContext _context;

    public MetadataView(PackageContext context)
    {
        _context = context;
        InitializeComponent();
        ReleasedOnPicker.SelectedDate = DateTime.Today;
    }

    public void SetDefaultTitle(string title)
    {
        TitleField.Text = title;
    }

    public void ApplyMetadata()
    {
        var manifest = _context.Manifest;
        manifest.Title = TitleField.Text ?? string.Empty;
        manifest.SortTitle = string.IsNullOrWhiteSpace(SortTitleField.Text)
            ? manifest.Title
            : SortTitleField.Text;
        manifest.Version = VersionField.Text ?? "1.0";
        manifest.ReleasedOn = ReleasedOnPicker.SelectedDate ?? DateTime.Today;
        manifest.Singleplayer = SingleplayerCheckbox.IsChecked == true;
        manifest.Description = DescriptionField.Text ?? string.Empty;
        manifest.Notes = NotesField.Text ?? string.Empty;
        manifest.DirectoryName = Path.GetFileName(_context.InstallDirectory);
    }
}
