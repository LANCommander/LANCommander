using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ByteSizeLib;
using LANCommander.Launcher.Controls;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Views;

/// <summary>A file picked for an issue report, not yet uploaded.</summary>
public sealed class ReportIssueAttachment(string path, long size)
{
    public string Path { get; } = path;
    // Either separator, so fixtures' Windows paths also name correctly on Linux
    public string Name { get; } = System.IO.Path.GetFileName(path.Replace('\\', '/'));
    public long Size { get; } = size;
    public string SizeText => ByteSize.FromBytes(Size).ToString("0.#");
    public string IconName => IssueAttachmentIcons.For(Name);
}

/// <summary>Picks a Phosphor icon for an attachment by its file name.</summary>
public static class IssueAttachmentIcons
{
    public static string For(string fileName) =>
        IssueAttachmentRules.IsImage(fileName) ? "Image"
        : System.IO.Path.GetExtension(fileName).ToLowerInvariant() is ".zip" or ".7z" ? "FileZip"
        : System.IO.Path.GetExtension(fileName).ToLowerInvariant() is ".dmp" ? "File"
        : "FileText";
}

/// <summary>
/// Lets the user describe a problem with a game, attach screenshots or logs, and send it to the
/// server. The dialog stays open while the report is sent and if it fails, so nothing is lost.
/// </summary>
public partial class ReportIssueOverlay : UserControl
{
    /// <summary>Sends the title, description and attached file paths; returns null on success, otherwise a message to show the user.</summary>
    public delegate Task<string?> SubmitHandler(string title, string description, IReadOnlyList<string> attachmentPaths);

    /// <summary>Raised when the dialog closes; the argument is true if a report was sent.</summary>
    public event EventHandler<bool>? DialogClosed;

    private readonly SubmitHandler? _submit;
    private IssueAttachmentLimits _limits = IssueAttachmentLimits.Default;
    private readonly ObservableCollection<ReportIssueAttachment> _attachments = new();
    private bool _isSubmitting;

    public ReportIssueOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, () => Close(false));

        AttachmentList.ItemsSource = _attachments;
        ApplyLimits();

        TitleBox.TextChanged += (_, _) => UpdateSubmitEnabled();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => TitleBox.Focus());
    }

    /// <param name="limits">The server's attachment limits; the defaults if not given.</param>
    public ReportIssueOverlay(string gameTitle, SubmitHandler submit, IssueAttachmentLimits? limits = null) : this()
    {
        TitleBar.Title = string.IsNullOrWhiteSpace(gameTitle) ? "Report Issue" : $"Report Issue: {gameTitle}";
        _submit = submit;
        _limits = limits ?? IssueAttachmentLimits.Default;

        ApplyLimits();
    }

    /// <summary>Describes the limits beside the attach button, or hides it if the server takes no files.</summary>
    private void ApplyLimits()
    {
        AttachRow.IsVisible = _limits.Enabled;

        var count = _limits.MaxFiles == 1 ? "1 file" : $"up to {_limits.MaxFiles} files";

        AttachHint.Text = $"Screenshots, logs or crash dumps: {count}, {IssueAttachmentRules.FormatSize(_limits.MaxFileSize)} each.";

        UpdateAttachEnabled();
    }

    /// <summary>Adds a file as if it had been picked. Used by visual fixtures.</summary>
    internal void AddAttachment(ReportIssueAttachment attachment)
    {
        _attachments.Add(attachment);
        UpdateAttachEnabled();
    }

    private void UpdateSubmitEnabled()
        => SubmitButton.IsEnabled = !_isSubmitting && !string.IsNullOrWhiteSpace(TitleBox.Text);

    private void UpdateAttachEnabled()
        => AttachButton.IsEnabled = !_isSubmitting && _attachments.Count < _limits.MaxFiles;

    private void ShowError(string? message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = !string.IsNullOrEmpty(message);
    }

    private async void Attach_Click(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storageProvider == null)
            return;

        var picked = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Attach Files",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Screenshots, logs and crash dumps")
                {
                    Patterns = IssueAttachmentRules.AllowedExtensions.Select(ext => $"*{ext}").ToList(),
                },
            ],
        });

        var added = new List<ReportIssueAttachment>();

        foreach (var file in picked)
        {
            var path = file.TryGetLocalPath();

            // Platforms without file paths (Android) hand over a stream; keep a copy to upload from
            path ??= await CopyToTempAsync(file);

            if (path == null || _attachments.Any(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            added.Add(new ReportIssueAttachment(path, new FileInfo(path).Length));
        }

        if (added.Count == 0)
            return;

        var problem = IssueAttachmentRules.Validate(_attachments.Concat(added).Select(a => (a.Name, a.Size)).ToList(), _limits);

        ShowError(problem);

        if (problem != null)
            return;

        foreach (var attachment in added)
            _attachments.Add(attachment);

        UpdateAttachEnabled();
    }

    private static async Task<string?> CopyToTempAsync(IStorageFile file)
    {
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "LANCommander", "IssueUploads", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, Path.GetFileName(file.Name));

            await using var source = await file.OpenReadAsync();
            await using var destination = File.Create(path);
            await source.CopyToAsync(destination);

            return path;
        }
        catch
        {
            return null;
        }
    }

    private void RemoveAttachment_Click(object? sender, RoutedEventArgs e)
    {
        if (_isSubmitting || (sender as Control)?.DataContext is not ReportIssueAttachment attachment)
            return;

        _attachments.Remove(attachment);
        ShowError(null);
        UpdateAttachEnabled();
    }

    private async void Submit_Click(object? sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text?.Trim();
        var description = DescriptionBox.Text?.Trim() ?? string.Empty;

        if (_submit == null || _isSubmitting || string.IsNullOrEmpty(title))
            return;

        SetSubmitting(true);

        string? error;

        try
        {
            error = await _submit(title, description, _attachments.Select(a => a.Path).ToList());
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        SetSubmitting(false);

        if (error == null)
        {
            Close(true);
            return;
        }

        ShowError(error);
    }

    private void SetSubmitting(bool submitting)
    {
        _isSubmitting = submitting;

        if (submitting)
            ShowError(null);

        TitleBox.IsEnabled = !submitting;
        DescriptionBox.IsEnabled = !submitting;
        AttachmentList.IsEnabled = !submitting;
        SubmitButton.Content = submitting
            ? (_attachments.Count > 0 ? "Uploading..." : "Submitting...")
            : "Submit";

        UpdateSubmitEnabled();
        UpdateAttachEnabled();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close(false);

    private void Close(bool submitted)
    {
        // Closing mid-request would hide the outcome, so wait for it.
        if (_isSubmitting)
            return;

        var layer = OverlayLayer.GetOverlayLayer(this);
        DialogClosed?.Invoke(this, submitted);
        layer?.Children.Remove(this);
    }

    /// <summary>
    /// Shows the dialog over the main window. Returns true once a report has been sent, false if
    /// the user cancelled.
    /// </summary>
    public static async Task<bool> ShowAsync(string gameTitle, SubmitHandler submit, IssueAttachmentLimits? limits = null)
    {
        var tcs = new TaskCompletionSource<bool>();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new ReportIssueOverlay(gameTitle, submit, limits)
            {
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.DialogClosed += (_, submitted) => tcs.TrySetResult(submitted);

            var mainWindow = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            var layer = OverlayLayer.GetOverlayLayer(mainWindow);

            if (layer is not null)
            {
                overlay.Bind(WidthProperty, new Binding("Bounds.Width") { Source = layer });
                overlay.Bind(HeightProperty, new Binding("Bounds.Height") { Source = layer });
                layer.Children.Add(overlay);
            }
            else
            {
                tcs.TrySetResult(false);
            }
        });

        return await tcs.Task;
    }
}
