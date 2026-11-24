
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.Views.Reports;

public partial class IndividualPatientRecordReportView : UserControl
{
    private readonly IndividualPatientRecordReport _report;

    public event Action? BackRequested;
    public event Action? AllReportsRequested;

    private const double PageWidth = 1056;
    private const double PageHeight = 816;
    private const double MarginSize = 40;
    private const double ContentWidth = PageWidth - (MarginSize * 2);
    private const double FooterTop = PageHeight - 42;
    private const double ContentBottom = FooterTop - 18;

    private static readonly TimeZoneInfo ClinicTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

    private static readonly Brush PrimaryBrush =
        new SolidColorBrush(Color.FromRgb(121, 82, 179));

    private static readonly Brush LightPurpleBrush =
        new SolidColorBrush(Color.FromRgb(245, 242, 250));

    private static readonly Brush BorderBrush =
        new SolidColorBrush(Color.FromRgb(225, 221, 233));

    public IndividualPatientRecordReportView(
        IndividualPatientRecordReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Patient);

        InitializeComponent();

        _report = report;
        DataContext = _report;
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private void AllReportsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        AllReportsRequested?.Invoke();
    }

    private async void PrintPreviewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var app = (App)Application.Current;
            var sessionService = app.SessionService;
            var authorizationService =
                new AuthorizationService(sessionService);

            var systemSettingService = new SystemSettingService(
                new SystemSettingRepository(),
                authorizationService,
                sessionService);

            string clinicName;

            try
            {
                var setting = await systemSettingService.GetByKeyAsync(
                    SystemSettingKeys.Clinic.Name);

                clinicName = string.IsNullOrWhiteSpace(setting?.Value)
                    ? SystemSettingDefaults.Values[SystemSettingKeys.Clinic.Name]
                    : setting.Value;
            }
            catch
            {
                clinicName =
                    SystemSettingDefaults.Values[SystemSettingKeys.Clinic.Name];
            }

            var document = CreatePrintDocument(_report, clinicName);

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
                        "Golddie - Individual Patient Record");
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
                        "Golddie - Individual Patient Record");
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
                Title = "Individual Patient Record - Print Preview",
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
        IndividualPatientRecordReport report,
        string clinicName)
    {
        var document = new FixedDocument();

        document.DocumentPaginator.PageSize =
            new Size(PageWidth, PageHeight);

        var patientFullName = string.Join(
            " ",
            new[]
            {
                report.Patient.FirstName,
                report.Patient.MiddleName,
                report.Patient.LastName
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var composer = new ReportPageComposer(
            clinicName,
            patientFullName);

        composer.AddPatientInformation(report.Patient);
        composer.AddObstetricHistory(report);
        composer.AddPregnancies(report.Pregnancies);
        composer.AddPrenatalVisits(report.PrenatalVisits);
        composer.AddVitalSigns(report.VitalSigns);
        composer.AddLaboratoryResults(report.LaboratoryResults);

        composer.AddText(
            "END OF INDIVIDUAL PATIENT RECORD",
            12,
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

    private static string FormatDate(DateOnly? date)
    {
        return date?.ToString("MMM dd, yyyy") ?? "—";
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("MMM dd, yyyy");
    }

    private static string FormatDateTime(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            ClinicTimeZone);

        return local.ToString("MMM dd, yyyy hh:mm tt");
    }

    private static string FormatNumber<T>(T? value)
        where T : struct, IFormattable
    {
        return value.HasValue
            ? value.Value.ToString(null, null)
            : "—";
    }

    private sealed class ReportPageComposer
    {
        private readonly string _clinicName;
        private readonly string _reportPatientName;

        public List<FixedPage> Pages { get; } = [];

        private FixedPage _currentPage = null!;
        private double _currentTop;

        public ReportPageComposer(
            string clinicName,
            string patientName)
        {
            _clinicName = clinicName;
            _reportPatientName = string.IsNullOrWhiteSpace(patientName)
                ? "Patient"
                : patientName;

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
                "INDIVIDUAL PATIENT RECORD",
                MarginSize,
                45,
                ContentWidth,
                25,
                16,
                Brushes.Black,
                FontWeights.Bold);

            AddTextToPage(
                _currentPage,
                $"Patient: {Display(_reportPatientName)}",
                MarginSize,
                73,
                ContentWidth,
                18,
                10,
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

        private void AddFieldPair(
            string label,
            string value,
            double left,
            double top,
            double width)
        {
            AddTextToPage(
                _currentPage,
                label,
                left,
                top,
                width,
                14,
                8,
                Brushes.Gray,
                FontWeights.Normal);

            AddTextToPage(
                _currentPage,
                Display(value),
                left,
                top + 14,
                width,
                20,
                9,
                Brushes.Black,
                FontWeights.SemiBold);
        }

        public void AddPatientInformation(Patient patient)
        {
            AddSectionTitle("1. PATIENT INFORMATION");

            double columnWidth = ContentWidth / 3;
            double rowHeight = 43;

            var fields = new (string Label, string Value)[]
            {
                ("Patient Number", Display(patient.PatientNumber)),
                ("First Name", Display(patient.FirstName)),
                ("Middle Name", Display(patient.MiddleName)),
                ("Last Name", Display(patient.LastName)),
                ("Date of Birth", patient.DateOfBirth?.ToString("MMM dd, yyyy") ?? "—"),
                ("Sex", Display(patient.Sex)),
                ("Civil Status", Display(patient.CivilStatus)),
                ("Contact Number", Display(patient.ContactNumber)),
                ("Email Address", Display(patient.Email))
            };

            for (var i = 0; i < fields.Length; i++)
            {
                var row = i / 3;
                var column = i % 3;

                AddFieldPair(
                    fields[i].Label,
                    fields[i].Value,
                    MarginSize + (column * columnWidth) + 8,
                    _currentTop + (row * rowHeight),
                    columnWidth - 16);
            }

            _currentTop += rowHeight * 3 + 5;

            AddFieldPair(
                "Residential Address",
                Display(patient.Address),
                MarginSize + 8,
                _currentTop,
                ContentWidth / 2 - 16);

            AddFieldPair(
                "Registration Date",
                FormatDateTime(patient.CreatedAtUtc),
                MarginSize + ContentWidth / 2,
                _currentTop,
                ContentWidth / 2 - 16);

            _currentTop += 48;

            AddFieldPair(
                "Emergency Contact Name",
                Display(patient.EmergencyContactName),
                MarginSize + 8,
                _currentTop,
                ContentWidth / 2 - 16);

            AddFieldPair(
                "Emergency Contact Number",
                Display(patient.EmergencyContactNumber),
                MarginSize + ContentWidth / 2,
                _currentTop,
                ContentWidth / 2 - 16);

            _currentTop += 48;
        }

        public void AddObstetricHistory(
            IndividualPatientRecordReport report)
        {
            AddSectionTitle("2. PREGNANCY AND OBSTETRIC HISTORY");

            var summary = report.ObstetricHistory;

            AddTable(
                ["G", "T", "P", "A", "L", "Unclassified"],
                [
                    [
                        summary.Gravida.ToString(),
                        summary.Term.ToString(),
                        summary.Preterm.ToString(),
                        summary.Abortion.ToString(),
                        summary.LivingChildrenDisplay,
                        summary.UnclassifiedPregnancies.ToString()
                    ]
                ],
                [150, 150, 150, 150, 180, 196]);

            _currentTop += 10;
        }

        public void AddPregnancies(List<Pregnancy> pregnancies)
        {
            AddSectionTitle("3. PREGNANCY RECORDS");

            var rows = pregnancies.Select(p => new string[]
            {
                Display(p.Status),
                FormatDate(p.LastMenstrualPeriod),
                FormatDate(p.EstimatedDueDate),
                Display(p.PregnancyOutcome),
                Display(p.DeliveryMethod),
                FormatNumber(p.GestationalAgeAtEndWeeks),
                FormatNumber(p.LivingChildrenCount)
            }).ToList();

            AddTable(
                ["Status", "LMP", "EDD", "Outcome", "Delivery Method", "Weeks", "Living"],
                rows,
                [110, 125, 125, 160, 190, 110, 146]);
        }

        public void AddPrenatalVisits(List<PrenatalVisit> visits)
        {
            AddSectionTitle("4. PRENATAL VISIT HISTORY");

            var rows = visits.Select(v => new string[]
            {
                FormatDate(v.VisitDate),
                Display(v.Notes)
            }).ToList();

            AddTable(
                ["Visit Date", "Clinical Notes"],
                rows,
                [180, ContentWidth - 180]);
        }

        public void AddVitalSigns(List<VitalSign> vitalSigns)
        {
            AddSectionTitle("5. VITAL SIGNS HISTORY");

            var rows = vitalSigns.Select(v => new string[]
            {
                FormatDateTime(v.RecordedAtUtc),
                $"{FormatNumber(v.SystolicBloodPressure)}/{FormatNumber(v.DiastolicBloodPressure)}",
                FormatNumber(v.HeartRate),
                FormatNumber(v.RespiratoryRate),
                FormatNumber(v.TemperatureCelsius),
                FormatNumber(v.OxygenSaturation),
                FormatNumber(v.WeightKg),
                FormatNumber(v.FetalHeartRate)
            }).ToList();

            AddTable(
                ["Recorded", "BP", "HR", "RR", "Temp °C", "SpO₂", "Weight kg", "Fetal HR"],
                rows,
                [160, 125, 95, 95, 115, 105, 125, 131]);
        }

        public void AddLaboratoryResults(List<LaboratoryResult> results)
        {
            AddSectionTitle("6. LABORATORY RESULTS");

            var rows = results.Select(r => new string[]
            {
                FormatDate(r.TestDate),
                Display(r.TestName),
                Display(r.Result),
                Display(r.Unit),
                Display(r.ReferenceRange),
                Display(r.Notes)
            }).ToList();

            AddTable(
                ["Test Date", "Test Name", "Result", "Unit", "Reference Range", "Notes"],
                rows,
                [120, 170, 140, 90, 180, 251]);
        }

        private void AddTable(
            string[] headers,
            List<string[]> rows,
            double[] columnWidths)
        {
            AddTableHeader(headers, columnWidths);

            if (rows.Count == 0)
            {
                AddTableRow(
                    ["No records available."],
                    [ContentWidth],
                    34,
                    false);

                return;
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (_currentTop + 38 > ContentBottom)
                {
                    CreateNewPage();
                    AddTableHeader(headers, columnWidths);
                }

                AddTableRow(
                    rows[rowIndex],
                    columnWidths,
                    38,
                    rowIndex % 2 == 1);
            }
        }

        private void AddTableHeader(
            string[] headers,
            double[] columnWidths)
        {
            EnsureSpace(40);

            var left = MarginSize;

            for (var i = 0; i < headers.Length; i++)
            {
                AddCell(
                    headers[i],
                    left,
                    _currentTop,
                    columnWidths[i],
                    32,
                    PrimaryBrush,
                    Brushes.White,
                    true);

                left += columnWidths[i];
            }

            _currentTop += 32;
        }

        private void AddTableRow(
            string[] values,
            double[] columnWidths,
            double height,
            bool alternate)
        {
            var left = MarginSize;

            if (values.Length == 1 && columnWidths.Length == 1)
            {
                AddCell(
                    values[0],
                    left,
                    _currentTop,
                    columnWidths[0],
                    height,
                    Brushes.WhiteSmoke,
                    Brushes.Gray,
                    false);

                _currentTop += height;
                return;
            }

            for (var i = 0; i < values.Length; i++)
            {
                AddCell(
                    values[i],
                    left,
                    _currentTop,
                    columnWidths[i],
                    height,
                    alternate ? Brushes.WhiteSmoke : Brushes.White,
                    Brushes.Black,
                    false);

                left += columnWidths[i];
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
                BorderBrush = System.Windows.Media.Brushes.LightGray,
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
