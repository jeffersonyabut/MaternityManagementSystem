
using System;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels.Reports;

namespace MaternityManagementSystem.Views.Reports;

public partial class PrenatalVisitReportView : UserControl
{
    public event Action? BackRequested;

    private const double PageWidth = 816;
    private const double PageHeight = 1056;
    private const double MarginSize = 45;
    private const double ContentWidth = PageWidth - (MarginSize * 2);

    private static readonly Brush PrimaryBrush =
        new SolidColorBrush(Color.FromRgb(121, 82, 179));

    private static readonly Brush LightPurpleBrush =
        new SolidColorBrush(Color.FromRgb(245, 242, 250));

    public PrenatalVisitReportView()
    {
        InitializeComponent();
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private void PrintPreviewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not PrenatalVisitReportViewModel viewModel ||
                viewModel.Report is null)
            {
                MessageBox.Show(
                    "Please wait until the prenatal visit report has been generated.",
                    "Report Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var app = (App)Application.Current;

            var document = CreatePrintDocument(
                viewModel.Report,
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
                Cursor = System.Windows.Input.Cursors.Hand
            };

            printButton.Click += (_, _) =>
            {
                var printDialog = new PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintDocument(
                        document.DocumentPaginator,
                        "Golddie - Prenatal Visit Report");
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
                Cursor = System.Windows.Input.Cursors.Hand
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
                        PageOrientation.Portrait;

                    printDialog.PrintTicket = printTicket;

                    printDialog.PrintDocument(
                        document.DocumentPaginator,
                        "Golddie - Prenatal Visit Report");
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
                Title = "Prenatal Visit Report - Print Preview",
                Content = layout,
                Width = 1000,
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
        PrenatalVisitReport report,
        RegionalSettingsService regionalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        var document = new FixedDocument();

        document.DocumentPaginator.PageSize =
            new Size(PageWidth, PageHeight);

        var page = new FixedPage
        {
            Width = PageWidth,
            Height = PageHeight,
            Background = Brushes.White
        };

        double top = MarginSize;

        AddText(
            page,
            "GOLDDIE · CLINIC MANAGEMENT",
            MarginSize,
            top,
            ContentWidth,
            20,
            11,
            PrimaryBrush,
            FontWeights.Bold);

        top += 28;

        AddText(
            page,
            "PRENATAL VISIT REPORT",
            MarginSize,
            top,
            ContentWidth,
            30,
            19,
            Brushes.Black,
            FontWeights.Bold);

        top += 42;

        AddText(
            page,
            $"Generated: {FormatDateTime(report.GeneratedAtUtc, regionalSettingsService)}",
            MarginSize,
            top,
            ContentWidth,
            20,
            9,
            Brushes.Gray,
            FontWeights.Normal);

        top += 35;

        AddSectionTitle(page, "1. PATIENT INFORMATION", ref top);

        AddField(
            page,
            "Patient Number",
            report.Patient.PatientNumber,
            MarginSize,
            top,
            ContentWidth / 2);

        AddField(
            page,
            "Patient Name",
            GetPatientName(report),
            MarginSize + ContentWidth / 2,
            top,
            ContentWidth / 2);

        top += 48;

        AddField(
            page,
            "Date of Birth",
            FormatDate(report.Patient.DateOfBirth, regionalSettingsService),
            MarginSize,
            top,
            ContentWidth / 3);

        AddField(
            page,
            "Sex",
            report.Patient.Sex,
            MarginSize + ContentWidth / 3,
            top,
            ContentWidth / 3);

        AddField(
            page,
            "Contact Number",
            report.Patient.ContactNumber,
            MarginSize + (ContentWidth * 2 / 3),
            top,
            ContentWidth / 3);

        top += 55;

        AddSectionTitle(page, "2. PREGNANCY INFORMATION", ref top);

        AddField(
            page,
            "Pregnancy Status",
            report.Pregnancy.Status,
            MarginSize,
            top,
            ContentWidth / 3);

        AddField(
            page,
            "Last Menstrual Period",
            FormatDate(report.Pregnancy.LastMenstrualPeriod, regionalSettingsService),
            MarginSize + ContentWidth / 3,
            top,
            ContentWidth / 3);

        AddField(
            page,
            "Estimated Due Date",
            FormatDate(report.Pregnancy.EstimatedDueDate, regionalSettingsService),
            MarginSize + (ContentWidth * 2 / 3),
            top,
            ContentWidth / 3);

        top += 55;

        AddSectionTitle(page, "3. PRENATAL VISIT DETAILS", ref top);

        AddField(
            page,
            "Visit Date",
            FormatDate(report.PrenatalVisit.VisitDate, regionalSettingsService),
            MarginSize,
            top,
            ContentWidth);

        top += 48;

        AddField(
            page,
            "Clinical Notes",
            report.PrenatalVisit.Notes,
            MarginSize,
            top,
            ContentWidth);

        top += 65;

        AddSectionTitle(page, "4. VITAL SIGNS", ref top);

        AddText(
            page,
            $"Total vital sign records: {report.TotalVitalSigns}",
            MarginSize,
            top,
            ContentWidth,
            20,
            9,
            Brushes.Gray,
            FontWeights.Normal);

        top += 25;

        var vitalHeaders = new[]
        {
            "Recorded",
            "Blood Pressure",
            "Heart Rate",
            "Resp. Rate",
            "Temp. °C",
            "SpO₂",
            "Weight",
            "Fetal HR"
        };

        var vitalRows = report.VitalSigns.Select(v => new[]
        {
            FormatDateTime(v.RecordedAtUtc, regionalSettingsService),
            $"{FormatNumber(v.SystolicBloodPressure)}/{FormatNumber(v.DiastolicBloodPressure)}",
            FormatNumber(v.HeartRate),
            FormatNumber(v.RespiratoryRate),
            FormatNumber(v.TemperatureCelsius),
            FormatNumber(v.OxygenSaturation),
            FormatNumber(v.WeightKg),
            FormatNumber(v.FetalHeartRate)
        }).ToList();

        AddTable(
            page,
            vitalHeaders,
            vitalRows,
            new double[] { 100, 100, 65, 65, 65, 55, 70, 70 },
            ref top);

        top += 25;

        AddSectionTitle(page, "5. LABORATORY RESULTS", ref top);

        AddText(
            page,
            $"Total laboratory results: {report.TotalLaboratoryResults}",
            MarginSize,
            top,
            ContentWidth,
            20,
            9,
            Brushes.Gray,
            FontWeights.Normal);

        top += 25;

        var labHeaders = new[]
        {
            "Test Date",
            "Test Name",
            "Result",
            "Unit",
            "Reference Range"
        };

        var labRows = report.LaboratoryResults.Select(r => new[]
        {
            FormatDate(r.TestDate, regionalSettingsService),
            Display(r.TestName),
            Display(r.Result),
            Display(r.Unit),
            Display(r.ReferenceRange)
        }).ToList();

        AddTable(
            page,
            labHeaders,
            labRows,
            new double[] { 105, 140, 100, 70, 175 },
            ref top);

        top += 35;

        AddText(
            page,
            "END OF PRENATAL VISIT REPORT",
            MarginSize,
            Math.Min(top, PageHeight - 90),
            ContentWidth,
            20,
            10,
            PrimaryBrush,
            FontWeights.Bold,
            TextAlignment.Center);

        AddText(
            page,
            "GOLDDIE · CLINIC MANAGEMENT",
            MarginSize,
            PageHeight - 55,
            ContentWidth / 2,
            16,
            8,
            Brushes.Gray,
            FontWeights.Normal);

        AddText(
            page,
            "Confidential Clinical Record",
            MarginSize + ContentWidth / 2,
            PageHeight - 55,
            ContentWidth / 2,
            16,
            8,
            Brushes.Gray,
            FontWeights.Normal,
            TextAlignment.Right);

        var pageContent = new PageContent
        {
            Child = page
        };

        document.Pages.Add(pageContent);

        return document;
    }

    private static void AddSectionTitle(
        FixedPage page,
        string title,
        ref double top)
    {
        var border = new Border
        {
            Width = ContentWidth,
            Height = 28,
            Background = LightPurpleBrush,
            BorderBrush = new SolidColorBrush(
                Color.FromRgb(225, 221, 233)),
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new TextBlock
            {
                Text = title,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = PrimaryBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        FixedPage.SetLeft(border, MarginSize);
        FixedPage.SetTop(border, top);

        page.Children.Add(border);

        top += 36;
    }

    private static void AddField(
        FixedPage page,
        string label,
        string? value,
        double left,
        double top,
        double width)
    {
        AddText(
            page,
            label,
            left,
            top,
            width,
            14,
            8,
            Brushes.Gray,
            FontWeights.Normal);

        AddText(
            page,
            Display(value),
            left,
            top + 15,
            width - 8,
            28,
            9,
            Brushes.Black,
            FontWeights.SemiBold);
    }

    private static void AddTable(
        FixedPage page,
        string[] headers,
        System.Collections.Generic.List<string[]> rows,
        double[] columnWidths,
        ref double top)
    {
        if (rows.Count == 0)
        {
            AddText(
                page,
                "No records available.",
                MarginSize,
                top,
                ContentWidth,
                24,
                9,
                Brushes.Gray,
                FontWeights.Normal);

            top += 28;
            return;
        }

        double left = MarginSize;

        for (int i = 0; i < headers.Length; i++)
        {
            AddTableCell(
                page,
                headers[i],
                left,
                top,
                columnWidths[i],
                32,
                PrimaryBrush,
                Brushes.White,
                true);

            left += columnWidths[i];
        }

        top += 32;

        foreach (var row in rows)
        {
            left = MarginSize;

            for (int i = 0; i < row.Length; i++)
            {
                AddTableCell(
                    page,
                    row[i],
                    left,
                    top,
                    columnWidths[i],
                    32,
                    Brushes.WhiteSmoke,
                    Brushes.Black,
                    false);

                left += columnWidths[i];
            }

            top += 32;
        }
    }

    private static void AddTableCell(
        FixedPage page,
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
            FontSize = 8,
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

        page.Children.Add(border);
    }

    private static void AddText(
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
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        FixedPage.SetLeft(textBlock, left);
        FixedPage.SetTop(textBlock, top);

        page.Children.Add(textBlock);
    }

    private static string GetPatientName(PrenatalVisitReport report)
    {
        return string.Join(
            " ",
            new[]
            {
                report.Patient.FirstName,
                report.Patient.MiddleName,
                report.Patient.LastName
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string FormatDate(
        DateOnly? date,
        RegionalSettingsService regionalSettingsService)
    {
        return date.HasValue
            ? regionalSettingsService.FormatDate(date.Value)
            : "—";
    }

    private static string FormatDate(
        DateOnly date,
        RegionalSettingsService regionalSettingsService)
    {
        return regionalSettingsService.FormatDate(date);
    }

    private static string FormatDateTime(
        DateTime utc,
        RegionalSettingsService regionalSettingsService)
    {
        return regionalSettingsService.FormatDateTime(utc);
    }

    private static string FormatNumber<T>(T? value)
        where T : struct, IFormattable
    {
        return value.HasValue
            ? value.Value.ToString(null, null)
            : "—";
    }
}
