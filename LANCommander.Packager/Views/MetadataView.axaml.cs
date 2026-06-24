using Avalonia.Controls;
using LANCommander.Packager.Models;
using LANCommander.SDK.Models.Manifest;
using LANCommander.SDK.Services;

namespace LANCommander.Packager.Views;

public partial class MetadataView : UserControl
{
    private readonly PackageContext _context;
    private readonly MetadataClient? _metadataClient;

    public MetadataView(PackageContext context) : this(context, null) { }

    public MetadataView(PackageContext context, MetadataClient? metadataClient)
    {
        _context = context;
        _metadataClient = metadataClient;
        InitializeComponent();
        ReleasedOnPicker.SelectedDate = DateTime.Today;

        LookupButton.Click += async (_, _) => await OnLookupClick();
    }

    public void SetAuthenticated(bool authenticated)
    {
        LookupButton.IsEnabled = authenticated && _metadataClient != null;
    }

    public void SetDefaultTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(TitleField.Text))
            TitleField.Text = title;
    }

    private async Task OnLookupClick()
    {
        if (_metadataClient == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window)
            return;

        var dialog = new MetadataSearchDialog(_metadataClient, TitleField.Text);
        var result = await dialog.ShowDialog<Game?>(window);

        if (result == null)
            return;

        PopulateFromGame(result);
    }

    private void PopulateFromGame(Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.Title))
            TitleField.Text = game.Title;

        if (!string.IsNullOrWhiteSpace(game.SortTitle))
            SortTitleField.Text = game.SortTitle;

        if (!string.IsNullOrWhiteSpace(game.Description))
            DescriptionField.Text = game.Description;

        if (!string.IsNullOrWhiteSpace(game.Notes))
            NotesField.Text = game.Notes;

        if (game.ReleasedOn != default)
            ReleasedOnPicker.SelectedDate = game.ReleasedOn;

        SingleplayerCheckbox.IsChecked = game.Singleplayer;

        var manifest = _context.Manifest;

        if (game.Genres.Count > 0)
            manifest.Genres = game.Genres;

        if (game.Tags.Count > 0)
            manifest.Tags = game.Tags;

        if (game.Developers.Count > 0)
            manifest.Developers = game.Developers;

        if (game.Publishers.Count > 0)
            manifest.Publishers = game.Publishers;

        if (game.Platforms.Count > 0)
            manifest.Platforms = game.Platforms;

        if (game.MultiplayerModes.Count > 0)
            manifest.MultiplayerModes = game.MultiplayerModes;

        if (game.Collections.Count > 0)
            manifest.Collections = game.Collections;

        if (game.ExternalIds.Count > 0)
            manifest.ExternalIds = game.ExternalIds;

        if (game.Engine != null)
            manifest.Engine = game.Engine;

        if (game.Type != default)
            manifest.Type = game.Type;
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
