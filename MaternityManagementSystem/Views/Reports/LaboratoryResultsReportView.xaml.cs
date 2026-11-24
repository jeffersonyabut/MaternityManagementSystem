
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels.Reports;

namespace MaternityManagementSystem.Views.Reports;

public partial class LaboratoryResultsReportView : UserControl
{
    private readonly LaboratoryResultsReportViewModel _viewModel;

    private const double PageWidth = 1056;
    private const double PageHeight = 816;
    private const double MarginSize = 40;
    private const double ContentWidth = PageWidth - (MarginSize * 2);
    private const double FooterTop = PageHeight - 42;
    private const double ContentBottom = FooterTop - 18;

    private static readonly Brush PrimaryBrush =
        new SolidColorBrush(Color.FromRgb(121, 82, 179));

    private static readonly Brush LightPurpleBrush =
        new SolidColorBrush(Color.FromRgb(245, 242, 250));

    private static readonly Brush BorderBrush =
        new SolidColorBrush(Color.FromRgb(225, 221, 233));

    public event Action? BackRequested;

    public LaboratoryResultsReportView(
        LaboratoryResultsReportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private async void PrintPreviewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel.Report is null ||
            !_viewModel.HasLaboratoryResults)
        {
            return;
        }

        try
        {
            var app = (App)Application.Current;

            var systemSettingService = new SystemSettingService(
                new SystemSettingRepository(),
                new AuthorizationService(app.SessionService),
                app.SessionService);

            var clinicSetting =
                await systemSettingService.GetByKeyAsync(
                    SystemSettingKeys.Clinic.Name);

            var clinicName = string.IsNullOrWhiteSpace(
                clinicSetting?.Value)
                ? SystemSettingDefaults.Values[
                    SystemSettingKeys.Clinic.Name]
                : clinicSetting.Value;

            var document = CreatePrintDocument(
                _viewModel.Report,
                clinicName,
                app.RegionalSettings);

            var viewer = new DocumentViewer
            {
                Document = document,
                Background = Brushes.White
            };

            var printButton = new Button
            {
                Content = "Print",
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(10),
                Background = Brushes.DarkSlateBlue,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };

            printButton.Click += (_, _) =>
            {
                var printDialog = new PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintDocument(
                        document.DocumentPaginator,
                        "Golddie - Laboratory Results Report");
                }
            };

            var pdfButton = new Button
            {
                Content = "Save as PDF",
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(10),
                Background = Brushes.White,
                Foreground = Brushes.DarkSlateBlue,
                BorderBrush = Brushes.DarkSlateBlue,
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };

            pdfButton.Click += (_, _) =>
            {
                try
                {
                    var printServer = new LocalPrintServer();

                    var pdfQueue = printServer.GetPrintQueue(
                        "Microsoft Print to PDF");

                    pdfQueue.Refresh();

                    var printDialog = new PrintDialog
                    {
                        PrintQueue = pdfQueue
                    };

                    var printTicket = pdfQueue.DefaultPrintTicket
                        ?? new PrintTicket();

                    printTicket.PageMediaSize =
                        new PageMediaSize(
                            PageMediaSizeName.NorthAmericaLetter);

                    printTicket.PageOrientation =
                        PageOrientation.Landscape;

                    printDialog.PrintTicket = printTicket;

                    printDialog.PrintDocument(
                        document.DocumentPaginator,
                        "Golddie - Laboratory Results Report");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to export using Microsoft Print to PDF.\n\n" +
                        "Make sure Microsoft Print to PDF is installed and enabled.\n\n" +
                        ex.Message,
                        "PDF Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            toolbar.Children.Add(pdfButton);
            toolbar.Children.Add(printButton);

            var layout = new DockPanel();

            DockPanel.SetDock(toolbar, Dock.Top);

            layout.Children.Add(toolbar);
            layout.Children.Add(viewer);

            var owner = Window.GetWindow(this);

            var previewWindow = new Window
            {
                Title = "Laboratory Results Report - Print Preview",
                Content = layout,
                Width = 1100,
                Height = 750,
                WindowStartupLocation = owner is null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Owner = owner
            };

            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Print Preview Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static FixedDocument CreatePrintDocument(
        LaboratoryResultsReport report,
        string clinicName,
        RegionalSettingsService regionalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        var document = new FixedDocument();

        document.DocumentPaginator.PageSize =
            new Size(PageWidth, PageHeight);

        var composer = new ReportPageComposer(
            report,
            clinicName,
            regionalSettingsService);

        composer.AddReportSummary();

        if (report.LaboratoryResults.Count == 0)
        {
            composer.AddText(
                "NO LABORATORY RESULTS FOUND",
                13,
                FontWeights.Bold,
                PrimaryBrush,
                TextAlignment.Center,
                35);
        }
        else
        {
            composer.AddLaboratoryResults();
        }

        composer.AddText(
            "END OF LABORATORY RESULTS REPORT",
            11,
            FontWeights.Bold,
            PrimaryBrush,
            TextAlignment.Center,
            25);

        var pages = composer.Pages;

        for (var i = 0; i < pages.Count; i++)
        {
            AddPageFooter(
                pages[i],
                i + 1,
                pages.Count,
                clinicName);

            document.Pages.Add(
                new PageContent
                {
                    Child = pages[i]
                });
        }

        return document;
    }

    private static void AddPageFooter(
        FixedPage page,
        int pageNumber,
        int totalPages,
        string clinicName)
    {
        var separator = new Border
        {
            Width = ContentWidth,
            Height = 0.7,
            Background = Brushes.LightGray
        };

        FixedPage.SetLeft(separator, MarginSize);
        FixedPage.SetTop(separator, FooterTop);

        page.Children.Add(separator);

        AddTextToPage(
            page,
            clinicName,
            MarginSize,
            FooterTop + 5,
            ContentWidth / 2,
            18,
            8,
            Brushes.Gray,
            FontWeights.Normal);

        AddTextToPage(
            page,
            $"Page {pageNumber} of {totalPages}",
            MarginSize + ContentWidth / 2,
            FooterTop + 5,
            ContentWidth / 2,
            18,
            8,
            Brushes.Gray,
            FontWeights.Normal,
            TextAlignment.Right);
    }

    private static void AddTextToPage(
        FixedPage page,
        string text,
        double left,
        double top,
        double width,
        double height,
        double fontSize,
        Brush foreground,
        FontWeight fontWeight,
        TextAlignment alignment = TextAlignment.Left)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Width = width,
            Height = height,
            FontFamily = new FontFamily("Arial"),
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = foreground,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        FixedPage.SetLeft(textBlock, left);
        FixedPage.SetTop(textBlock, top);

        page.Children.Add(textBlock);
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string FormatDate(
        DateOnly date,
        RegionalSettingsService regionalSettingsService)
    {
        return regionalSettingsService.FormatDate(date);
    }

    private static string FormatDate(
        DateOnly? date,
        RegionalSettingsService regionalSettingsService)
    {
        return date.HasValue
            ? FormatDate(date.Value, regionalSettingsService)
            : "Any";
    }

    private static string FormatDateTime(
        DateTime utc,
        RegionalSettingsService regionalSettingsService)
    {
        return regionalSettingsService.FormatDateTime(utc);
    }

    private sealed class ReportPageComposer
    {
        private static readonly string[] TableHeaders =
        [
            "Test Date",
            "Patient No.",
            "Patient Name",
            "Test Name",
            "Result",
            "Unit",
            "Reference Range",
            "Notes"
        ];

        private static readonly double[] ColumnWidths =
        [
            90,
            85,
            130,
            130,
            130,
            55,
            130,
            226
        ];

        private const double TableRowHeight = 42;

        public List<FixedPage> Pages { get; } = [];

        private readonly LaboratoryResultsReport _report;
        private readonly string _clinicName;
        private readonly RegionalSettingsService _regionalSettingsService;

        private FixedPage _currentPage = null!;
        private double _currentTop;

        public ReportPageComposer(
            LaboratoryResultsReport report,
            string clinicName,
            RegionalSettingsService regionalSettingsService)
        {
            _report = report;
            _clinicName = clinicName;

            _regionalSettingsService =
                regionalSettingsService
                ?? throw new ArgumentNullException(
                    nameof(regionalSettingsService));

            CreateNewPage();
        }

        private void CreateNewPage()
        {
            _currentPage = new FixedPage
            {
                Width = PageWidth,
                Height = PageHeight,
                Background = Brushes.White
            };

            Pages.Add(_currentPage);

            AddTextToPage(
                _currentPage,
                _clinicName,
                MarginSize,
                24,
                ContentWidth,
                18,
                10,
                PrimaryBrush,
                FontWeights.Bold);

            AddTextToPage(
                _currentPage,
                "LABORATORY RESULTS REPORT",
                MarginSize,
                45,
                ContentWidth,
                25,
                16,
                Brushes.Black,
                FontWeights.Bold);

            AddTextToPage(
                _currentPage,
                $"Generated: {FormatDateTime(
                    _report.GeneratedAtUtc,
                    _regionalSettingsService)}",
                MarginSize,
                73,
                ContentWidth,
                18,
                9,
                Brushes.Gray,
                FontWeights.Normal);

            _currentTop = 110;
        }

        private void EnsureSpace(double requiredHeight)
        {
            if (_currentTop + requiredHeight > ContentBottom)
            {
                CreateNewPage();
            }
        }

        public void AddText(
            string text,
            double fontSize,
            FontWeight fontWeight,
            Brush foreground,
            TextAlignment alignment = TextAlignment.Left,
            double height = 24)
        {
            EnsureSpace(height + 5);

            AddTextToPage(
                _currentPage,
                text,
                MarginSize,
                _currentTop,
                ContentWidth,
                height,
                fontSize,
                foreground,
                fontWeight,
                alignment);

            _currentTop += height + 5;
        }

        private void AddSectionTitle(string title)
        {
            EnsureSpace(42);

            var border = new Border
            {
                Width = ContentWidth,
                Height = 30,
                Background = LightPurpleBrush,
                BorderBrush = BorderBrush,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(8, 4, 8, 4),
                Child = new TextBlock
                {
                    Text = title,
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = PrimaryBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            FixedPage.SetLeft(border, MarginSize);
            FixedPage.SetTop(border, _currentTop);

            _currentPage.Children.Add(border);

            _currentTop += 38;
        }

        public void AddReportSummary()
        {
            AddSectionTitle("REPORT SUMMARY");

            var rows = new List<string[]>
            {
                new[]
                {
                    "Total Results",
                    _report.TotalResults.ToString(),
                    "Unique Patients",
                    _report.LaboratoryResults
                        .Select(result => result.PatientId)
                        .Distinct()
                        .Count()
                        .ToString()
                },
                new[]
                {
                    "Test Date From",
                    FormatDate(
                        _report.StartDate,
                        _regionalSettingsService),
                    "Test Date To",
                    FormatDate(
                        _report.EndDate,
                        _regionalSettingsService)
                },
                new[]
                {
                    "Patient Search",
                    Display(_report.SearchTerm),
                    "Laboratory Test",
                    Display(_report.TestName)
                }
            };

            AddTable(
                ["Filter / Metric", "Value", "Filter / Metric", "Value"],
                rows,
                [145, 343, 145, 343],
                30);

            _currentTop += 16;
        }

        public void AddLaboratoryResults()
        {
            AddSectionTitle("LABORATORY RESULTS");

            AddTableHeader();

            foreach (var result in _report.LaboratoryResults)
            {
                if (_currentTop + TableRowHeight > ContentBottom)
                {
                    CreateNewPage();

                    AddSectionTitle("LABORATORY RESULTS · CONTINUED");

                    AddTableHeader();
                }

                var patient = result.Patient;

                var patientName = patient is null
                    ? "Unknown patient"
                    : string.Join(
                        " ",
                        new[]
                        {
                            patient.FirstName,
                            patient.MiddleName,
                            patient.LastName
                        }.Where(value =>
                            !string.IsNullOrWhiteSpace(value)));

                var values = new[]
                {
                    FormatDate(
                        result.TestDate,
                        _regionalSettingsService),
                    Display(patient?.PatientNumber),
                    Display(patientName),
                    Display(result.TestName),
                    Display(result.Result),
                    Display(result.Unit),
                    Display(result.ReferenceRange),
                    Display(result.Notes)
                };

                AddTableRow(values, TableRowHeight);
            }

            _currentTop += 14;
        }

        private void AddTable(
            string[] headers,
            List<string[]> rows,
            double[] columnWidths,
            double rowHeight)
        {
            AddSimpleTableHeader(headers, columnWidths);

            foreach (var row in rows)
            {
                if (_currentTop + rowHeight > ContentBottom)
                {
                    CreateNewPage();
                    AddSimpleTableHeader(headers, columnWidths);
                }

                AddSimpleTableRow(
                    row,
                    columnWidths,
                    rowHeight);
            }
        }

        private void AddSimpleTableHeader(
            string[] headers,
            double[] columnWidths)
        {
            EnsureSpace(36);

            var left = MarginSize;

            for (var i = 0; i < headers.Length; i++)
            {
                AddCell(
                    headers[i],
                    left,
                    _currentTop,
                    columnWidths[i],
                    30,
                    PrimaryBrush,
                    Brushes.White,
                    true);

                left += columnWidths[i];
            }

            _currentTop += 30;
        }

        private void AddSimpleTableRow(
            string[] values,
            double[] columnWidths,
            double height)
        {
            var left = MarginSize;

            for (var i = 0; i < values.Length; i++)
            {
                AddCell(
                    values[i],
                    left,
                    _currentTop,
                    columnWidths[i],
                    height,
                    Brushes.White,
                    Brushes.Black,
                    false);

                left += columnWidths[i];
            }

            _currentTop += height;
        }

        private void AddTableHeader()
        {
            EnsureSpace(36);

            var left = MarginSize;

            for (var i = 0; i < TableHeaders.Length; i++)
            {
                AddCell(
                    TableHeaders[i],
                    left,
                    _currentTop,
                    ColumnWidths[i],
                    32,
                    PrimaryBrush,
                    Brushes.White,
                    true);

                left += ColumnWidths[i];
            }

            _currentTop += 32;
        }

        private void AddTableRow(
            string[] values,
            double height)
        {
            var left = MarginSize;

            for (var i = 0; i < values.Length; i++)
            {
                AddCell(
                    values[i],
                    left,
                    _currentTop,
                    ColumnWidths[i],
                    height,
                    Brushes.White,
                    Brushes.Black,
                    false);

                left += ColumnWidths[i];
            }

            _currentTop += height;
        }

        private void AddCell(
            string text,
            double left,
            double top,
            double width,
            double height,
            Brush background,
            Brush foreground,
            bool isHeader)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Arial"),
                FontSize = isHeader ? 8 : 7.5,
                FontWeight = isHeader
                    ? FontWeights.Bold
                    : FontWeights.Normal,
                Foreground = foreground,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                Width = width,
                Height = height,
                Background = background,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(4, 2, 4, 2),
                Child = textBlock
            };

            FixedPage.SetLeft(border, left);
            FixedPage.SetTop(border, top);

            _currentPage.Children.Add(border);
        }
    }
}
