
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Printing;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels.Reports;

namespace MaternityManagementSystem.Views.Reports;

public partial class ClinicActivityReportView : UserControl
{
    private const double PageWidth = 1056;
    private const double PageHeight = 816;

    private const double LeftMargin = 45;
    private const double RightMargin = 45;
    private const double TopMargin = 40;
    private const double BottomMargin = 40;

    private const double RowHeight = 28;
    private const int FirstPageRows = 5;
    private const int SubsequentPageRows = 19;

    private readonly Brush PrimaryPurple =
        new SolidColorBrush(Color.FromRgb(121, 82, 179));

    private readonly Brush DarkText =
        new SolidColorBrush(Color.FromRgb(41, 36, 59));

    private readonly Brush SecondaryText =
        new SolidColorBrush(Color.FromRgb(119, 113, 132));

    private readonly Brush LightBackground =
        new SolidColorBrush(Color.FromRgb(248, 246, 251));

    public ClinicActivityReportView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void PrintPreviewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not ClinicActivityReportViewModel viewModel ||
            viewModel.Report is not ClinicActivityReport report)
        {
            MessageBox.Show(
                "Please generate a clinic activity report first.",
                "Report Unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

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

            var clinicName = string.IsNullOrWhiteSpace(clinicSetting?.Value)
                ? SystemSettingDefaults.Values[SystemSettingKeys.Clinic.Name]
                : clinicSetting.Value;

            FixedDocument document = CreateReportDocument(
                report,
                clinicName,
                app.RegionalSettings);

            ShowPrintPreview(document, clinicName);
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

    private FixedDocument CreateReportDocument(
        ClinicActivityReport report,
        string clinicName,
        RegionalSettingsService regionalSettingsService)
    {
        var document = new FixedDocument();

        document.DocumentPaginator.PageSize =
            new Size(PageWidth, PageHeight);

        var monthlyActivity = report.MonthlyActivity;

        int totalPages = monthlyActivity.Count <= FirstPageRows
            ? 1
            : 1 + (int)Math.Ceiling(
                (monthlyActivity.Count - FirstPageRows) /
                (double)SubsequentPageRows);

        for (int pageIndex = 0;
             pageIndex < totalPages;
             pageIndex++)
        {
            var page = new FixedPage
            {
                Width = PageWidth,
                Height = PageHeight,
                Background = Brushes.White
            };

            AddPageHeader(
                page,
                report,
                pageIndex + 1,
                totalPages,
                clinicName,
                regionalSettingsService);

            bool isFirstPage = pageIndex == 0;

            if (isFirstPage)
            {
                AddSummarySection(
                    page,
                    report,
                    regionalSettingsService);
            }

            int skip;
            int take;

            if (isFirstPage)
            {
                skip = 0;
                take = FirstPageRows;
            }
            else
            {
                skip = FirstPageRows +
                       (pageIndex - 1) * SubsequentPageRows;

                take = SubsequentPageRows;
            }

            AddMonthlyActivityTable(
                page,
                monthlyActivity
                    .Skip(skip)
                    .Take(take)
                    .ToList(),
                isFirstPage);

            AddPageFooter(
                page,
                pageIndex + 1,
                totalPages,
                clinicName);

            var pageContent = new PageContent();

            ((System.Windows.Markup.IAddChild)pageContent)
                .AddChild(page);

            document.Pages.Add(pageContent);
        }

        return document;
    }

    private void AddPageHeader(
        FixedPage page,
        ClinicActivityReport report,
        int pageNumber,
        int totalPages,
        string clinicName,
        RegionalSettingsService regionalSettingsService)
    {
        AddText(
            page,
            clinicName,
            LeftMargin,
            TopMargin,
            400,
            18,
            10,
            PrimaryPurple,
            FontWeights.Bold);

        AddText(
            page,
            "Clinic Activity Report",
            LeftMargin,
            TopMargin + 23,
            650,
            38,
            24,
            DarkText,
            FontWeights.Bold);

        AddText(
            page,
            "Consolidated overview of patient registrations and clinic operations",
            LeftMargin,
            TopMargin + 65,
            700,
            22,
            11,
            SecondaryText);

        AddText(
            page,
            $"Generated: {regionalSettingsService.FormatDateTime(report.GeneratedAtUtc)}",
            PageWidth - RightMargin - 280,
            TopMargin + 5,
            280,
            20,
            10,
            SecondaryText,
            textAlignment: TextAlignment.Right);

        AddText(
            page,
            $"Page {pageNumber} of {totalPages}",
            PageWidth - RightMargin - 280,
            TopMargin + 27,
            280,
            20,
            10,
            SecondaryText,
            textAlignment: TextAlignment.Right);

        AddLine(
            page,
            LeftMargin,
            TopMargin + 98,
            PageWidth - RightMargin,
            TopMargin + 98);
    }

    private void AddSummarySection(
        FixedPage page,
        ClinicActivityReport report,
        RegionalSettingsService regionalSettingsService)
    {
        double y = 155;

        AddSectionTitle(page, "REPORT PERIOD", y);

        string startDate = report.StartDate.HasValue
            ? regionalSettingsService.FormatDate(report.StartDate.Value)
            : "All dates";

        string endDate = report.EndDate.HasValue
            ? regionalSettingsService.FormatDate(report.EndDate.Value)
            : "All dates";

        string dateRange = $"{startDate} — {endDate}";

        AddText(
            page,
            dateRange,
            LeftMargin,
            y + 24,
            500,
            24,
            12,
            DarkText,
            FontWeights.SemiBold);

        y += 66;

        AddSectionTitle(page, "CLINIC ACTIVITY TOTALS", y);

        y += 25;

        double cardGap = 12;
        double cardWidth =
            (PageWidth - LeftMargin - RightMargin - (cardGap * 2)) / 3;

        var metrics = new[]
        {
            ("Patient Registrations", report.TotalPatientRegistrations),
            ("Pregnancy Records", report.TotalPregnancyRecords),
            ("Prenatal Visits", report.TotalPrenatalVisits),
            ("Laboratory Results", report.TotalLaboratoryResults),
            ("Appointments", report.TotalAppointments),
            ("Unique Patients Served", report.UniquePatientsServed)
        };

        for (int i = 0; i < metrics.Length; i++)
        {
            int row = i / 3;
            int column = i % 3;

            double x = LeftMargin + column * (cardWidth + cardGap);
            double cardY = y + row * 65;

            AddCard(
                page,
                x,
                cardY,
                cardWidth,
                54,
                metrics[i].Item1,
                metrics[i].Item2.ToString("N0"));
        }

        y += 145;

        AddSectionTitle(page, "APPOINTMENT STATUS BREAKDOWN", y);

        y += 25;

        var statuses = new[]
        {
            ("Scheduled", report.ScheduledAppointments),
            ("Confirmed", report.ConfirmedAppointments),
            ("Completed", report.CompletedAppointments),
            ("Cancelled", report.CancelledAppointments),
            ("No-show", report.NoShowAppointments)
        };

        double statusGap = 10;
        double statusWidth =
            (PageWidth - LeftMargin - RightMargin - (statusGap * 4)) / 5;

        for (int i = 0; i < statuses.Length; i++)
        {
            double x = LeftMargin + i * (statusWidth + statusGap);

            AddCard(
                page,
                x,
                y,
                statusWidth,
                48,
                statuses[i].Item1,
                statuses[i].Item2.ToString("N0"));
        }

        AddText(
            page,
            "Appointment statuses represent their current recorded status, not their historical status as of the report period.",
            LeftMargin,
            y + 55,
            PageWidth - LeftMargin - RightMargin,
            25,
            9,
            SecondaryText);
    }

    private void AddMonthlyActivityTable(
        FixedPage page,
        System.Collections.Generic.List<ClinicActivityMonthlySummary> records,
        bool firstPage)
    {
        double y = firstPage ? 550 : 155;

        AddSectionTitle(page, "MONTHLY CLINIC ACTIVITY", y);

        y += 28;

        double[] columnWidths =
        {
            130, 165, 145, 135, 145, 150
        };

        string[] headers =
        {
            "Month",
            "Patient Registrations",
            "Pregnancy Records",
            "Prenatal Visits",
            "Laboratory Results",
            "Appointments"
        };

        double x = LeftMargin;

        for (int i = 0; i < headers.Length; i++)
        {
            AddTableCell(
                page,
                headers[i],
                x,
                y,
                columnWidths[i],
                RowHeight,
                true);

            x += columnWidths[i];
        }

        y += RowHeight;

        if (records.Count == 0)
        {
            AddTableCell(
                page,
                "No monthly activity recorded.",
                LeftMargin,
                y,
                columnWidths.Sum(),
                RowHeight,
                false);

            return;
        }

        foreach (var record in records)
        {
            x = LeftMargin;

            string[] values =
            {
                record.Month,
                record.PatientRegistrations.ToString("N0"),
                record.PregnancyRecords.ToString("N0"),
                record.PrenatalVisits.ToString("N0"),
                record.LaboratoryResults.ToString("N0"),
                record.Appointments.ToString("N0")
            };

            for (int i = 0; i < values.Length; i++)
            {
                AddTableCell(
                    page,
                    values[i],
                    x,
                    y,
                    columnWidths[i],
                    RowHeight,
                    false,
                    i == 0 ? TextAlignment.Left : TextAlignment.Center);

                x += columnWidths[i];
            }

            y += RowHeight;
        }
    }

    private void AddPageFooter(
        FixedPage page,
        int pageNumber,
        int totalPages,
        string clinicName)
    {
        double footerY = PageHeight - BottomMargin;

        AddLine(
            page,
            LeftMargin,
            footerY - 10,
            PageWidth - RightMargin,
            footerY - 10);

        AddText(
            page,
            clinicName,
            LeftMargin,
            footerY,
            400,
            18,
            9,
            SecondaryText,
            FontWeights.SemiBold);

        AddText(
            page,
            $"Page {pageNumber} of {totalPages}",
            PageWidth - RightMargin - 150,
            footerY,
            150,
            18,
            9,
            SecondaryText,
            textAlignment: TextAlignment.Right);
    }

    private void AddSectionTitle(
        FixedPage page,
        string title,
        double y)
    {
        AddText(
            page,
            title,
            LeftMargin,
            y,
            PageWidth - LeftMargin - RightMargin,
            20,
            12,
            PrimaryPurple,
            FontWeights.Bold);
    }

    private void AddCard(
        FixedPage page,
        double x,
        double y,
        double width,
        double height,
        string label,
        string value)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            Background = LightBackground,
            BorderBrush = new SolidColorBrush(
                Color.FromRgb(234, 231, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };

        var content = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            Foreground = SecondaryText,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        content.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = DarkText,
            Margin = new Thickness(0, 3, 0, 0)
        });

        border.Child = content;

        FixedPage.SetLeft(border, x);
        FixedPage.SetTop(border, y);

        page.Children.Add(border);
    }

    private void AddTableCell(
        FixedPage page,
        string value,
        double x,
        double y,
        double width,
        double height,
        bool isHeader,
        TextAlignment alignment = TextAlignment.Left)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            Background = isHeader
                ? LightBackground
                : Brushes.White,
            BorderBrush = new SolidColorBrush(
                Color.FromRgb(234, 231, 240)),
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(6, 4, 6, 4),
            Child = new TextBlock
            {
                Text = value,
                FontSize = isHeader ? 8 : 9,
                FontWeight = isHeader
                    ? FontWeights.Bold
                    : FontWeights.Normal,
                Foreground = isHeader
                    ? PrimaryPurple
                    : DarkText,
                TextAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            }
        };

        FixedPage.SetLeft(border, x);
        FixedPage.SetTop(border, y);

        page.Children.Add(border);
    }

    private void AddText(
        FixedPage page,
        string value,
        double x,
        double y,
        double width,
        double height,
        double fontSize,
        Brush foreground,
        FontWeight? fontWeight = null,
        TextAlignment textAlignment = TextAlignment.Left)
    {
        var textBlock = new TextBlock
        {
            Text = value,
            Width = width,
            Height = height,
            FontSize = fontSize,
            Foreground = foreground,
            FontWeight = fontWeight ?? FontWeights.Normal,
            TextAlignment = textAlignment,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        FixedPage.SetLeft(textBlock, x);
        FixedPage.SetTop(textBlock, y);

        page.Children.Add(textBlock);
    }

    private void AddLine(
        FixedPage page,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        var line = new System.Windows.Shapes.Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = x2 - x1,
            Y2 = 0,
            Stroke = new SolidColorBrush(
                Color.FromRgb(234, 231, 240)),
            StrokeThickness = 1
        };

        FixedPage.SetLeft(line, x1);
        FixedPage.SetTop(line, y1);

        page.Children.Add(line);
    }

    private void ShowPrintPreview(
        FixedDocument document,
        string clinicName)
    {
        var previewWindow = new Window
        {
            Title = $"{clinicName} | Clinic Activity Report - Print Preview",
            Width = 1150,
            Height = 850,
            MinWidth = 850,
            MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Background = new SolidColorBrush(
                Color.FromRgb(245, 245, 250))
        };

        var root = new DockPanel();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12)
        };

        var printButton = new Button
        {
            Content = "Print",
            Padding = new Thickness(20, 9, 20, 9),
            Margin = new Thickness(0, 0, 10, 0)
        };

        var pdfButton = new Button
        {
            Content = "Save as PDF",
            Padding = new Thickness(20, 9, 20, 9),
            Background = PrimaryPurple,
            Foreground = Brushes.White
        };

        var viewer = new DocumentViewer
        {
            Document = document,
            Margin = new Thickness(12)
        };

        printButton.Click += (_, _) =>
        {
            var dialog = new PrintDialog();

            if (dialog.ShowDialog() == true)
            {
                dialog.PrintDocument(
                    document.DocumentPaginator,
                    $"{clinicName} - Clinic Activity Report");
            }
        };

        pdfButton.Click += (_, _) =>
        {
            try
            {
                var localServer = new LocalPrintServer();

                var pdfQueue = localServer
                    .GetPrintQueues()
                    .FirstOrDefault(queue =>
                        queue.Name.Contains(
                            "Microsoft Print to PDF",
                            StringComparison.OrdinalIgnoreCase));

                if (pdfQueue is null)
                {
                    MessageBox.Show(
                        "Microsoft Print to PDF is not available on this computer.",
                        "PDF Export Unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                var dialog = new PrintDialog
                {
                    PrintQueue = pdfQueue
                };

                if (dialog.ShowDialog() == true)
                {
                    dialog.PrintDocument(
                        document.DocumentPaginator,
                        $"{clinicName} - Clinic Activity Report");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"PDF export failed: {ex.Message}",
                    "PDF Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        toolbar.Children.Add(printButton);
        toolbar.Children.Add(pdfButton);

        DockPanel.SetDock(toolbar, Dock.Top);

        root.Children.Add(toolbar);
        root.Children.Add(viewer);

        previewWindow.Content = root;

        previewWindow.ShowDialog();
    }
}
