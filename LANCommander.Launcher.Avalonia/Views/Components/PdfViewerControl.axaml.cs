using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PDFtoImage;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class PdfViewerControl : UserControl
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<PdfViewerControl, string?>(nameof(Source));

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private int _currentPage;
    private int _totalPages;
    private int _dpi = 150;
    private const int DpiStep = 25;
    private const int MinDpi = 72;
    private const int MaxDpi = 300;

    public PdfViewerControl()
    {
        InitializeComponent();
        
        PrevPageButton.Click += (_, _) => NavigatePage(-1);
        NextPageButton.Click += (_, _) => NavigatePage(1);
        ZoomInButton.Click += (_, _) => ChangeDpi(DpiStep);
        ZoomOutButton.Click += (_, _) => ChangeDpi(-DpiStep);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == SourceProperty)
        {
            LoadPdf();
        }
    }

    private void LoadPdf()
    {
        if (string.IsNullOrEmpty(Source) || !File.Exists(Source))
        {
            _totalPages = 0;
            _currentPage = 0;
            UpdatePageIndicator();
            PdfImage.Source = null;
            return;
        }

        try
        {
            using var pdfStream = File.OpenRead(Source);
            _totalPages = Conversion.GetPageCount(pdfStream);
            _currentPage = 0;
            RenderCurrentPage();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading PDF: {ex.Message}");
            _totalPages = 0;
            _currentPage = 0;
            UpdatePageIndicator();
        }
    }

    private void RenderCurrentPage()
    {
        if (string.IsNullOrEmpty(Source) || !File.Exists(Source) || _totalPages == 0)
            return;

        try
        {
            using var pdfStream = File.OpenRead(Source);
            using var imageStream = new MemoryStream();
            
            var options = new RenderOptions(Dpi: _dpi);
            Conversion.SavePng(imageStream, pdfStream, _currentPage, options: options);
            imageStream.Position = 0;
            
            var bitmap = new Bitmap(imageStream);
            PdfImage.Source = bitmap;
            
            UpdatePageIndicator();
            UpdateZoomIndicator();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering page: {ex.Message}");
        }
    }

    private void NavigatePage(int delta)
    {
        var newPage = _currentPage + delta;
        if (newPage >= 0 && newPage < _totalPages)
        {
            _currentPage = newPage;
            RenderCurrentPage();
        }
    }

    private void ChangeDpi(int delta)
    {
        var newDpi = _dpi + delta;
        if (newDpi >= MinDpi && newDpi <= MaxDpi)
        {
            _dpi = newDpi;
            RenderCurrentPage();
        }
    }

    private void UpdatePageIndicator()
    {
        PageIndicator.Text = _totalPages > 0 
            ? $"Page {_currentPage + 1} of {_totalPages}" 
            : "No pages";
        
        PrevPageButton.IsEnabled = _currentPage > 0;
        NextPageButton.IsEnabled = _currentPage < _totalPages - 1;
    }

    private void UpdateZoomIndicator()
    {
        ZoomIndicator.Text = $"{_dpi} DPI";
        ZoomOutButton.IsEnabled = _dpi > MinDpi;
        ZoomInButton.IsEnabled = _dpi < MaxDpi;
    }
}
