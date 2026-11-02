
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.ViewModels.Reports;

namespace MaternityManagementSystem.Views
{
    public partial class PatientMasterListReportView : UserControl
    {
        private readonly PatientMasterListViewModel _viewModel;

        private const double PageWidth = 1056;
        private const double PageHeight = 816;
        private const double MarginSize = 40;
        private const double ContentWidth = PageWidth - (MarginSize * 2);

        private static readonly double[] ColumnWidths =
        [
            100, 180, 105, 65, 100, 120, 306
        ];

        private static readonly string[] Headers =
        [
            "Patient No.",
            "Full Name",
            "Birth Date",
            "Sex",
            "Civil Status",
            "Contact",
            "Address"
        ];

        private static readonly TimeZoneInfo ClinicTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

        public PatientMasterListReportView(
            PatientMasterListViewModel viewModel)
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
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                dashboard.ShowReports();
            }
        }


        private void PrintPreviewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                var report = _viewModel.Report;

                if (report is null)
                {
                    MessageBox.Show(
                        "Generate a report before opening Print Preview.",
                        "No Report Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                var document = CreatePrintDocument(report);

                var viewer = new DocumentViewer
                {
                    Document = document,
                    Background = Brushes.White
                };

                var printButton = new Button
                {
                    Content = "Print / Save as PDF",
                    Padding = new Thickness(16, 8, 16, 8),
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Right,
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
                            "Golddie - Patient Master List");
                    }
                };

                var layout = new DockPanel();

                DockPanel.SetDock(printButton, Dock.Top);

                layout.Children.Add(printButton);
                layout.Children.Add(viewer);

                var owner = Window.GetWindow(this);

                var previewWindow = new Window
                {
                    Title = "Patient Master List - Print Preview",
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
            PatientMasterListReport report)
        {
            var document = new FixedDocument();

            document.DocumentPaginator.PageSize =
                new Size(PageWidth, PageHeight);

            var patients = report.Patients;

            // The first page has additional report information.
            const int firstPageCapacity = 12;

            // Subsequent pages have more room for patient records.
            const int otherPageCapacity = 15;

            var remaining = patients.Count;

            var totalPages = remaining <= firstPageCapacity
                ? 1
                : 1 + (int)Math.Ceiling(
                    (remaining - firstPageCapacity) /
                    (double)otherPageCapacity);

            var patientIndex = 0;

            for (var pageNumber = 1;
                 pageNumber <= totalPages;
                 pageNumber++)
            {
                var page = CreatePage();

                AddPageHeading(
                    page,
                    pageNumber,
                    report);

                var tableTop = pageNumber == 1 ? 220 : 105;

                AddTableHeader(page, tableTop);

                var capacity = pageNumber == 1
                    ? firstPageCapacity
                    : otherPageCapacity;

                // Use double because row positions support fractional values.
                double currentRowTop = tableTop + 34;

                const double rowHeight = 42;

                var rowsOnPage = Math.Min(
                    capacity,
                    remaining);

                for (var row = 0; row < rowsOnPage; row++)
                {
                    var patient = patients[patientIndex++];

                    AddPatientRow(
                        page,
                        patient,
                        currentRowTop,
                        rowHeight,
                        row % 2 == 1);

                    currentRowTop += rowHeight;
                    remaining--;
                }

                if (patients.Count == 0)
                {
                    AddText(
                        page,
                        "No matching patient records found.",
                        MarginSize,
                        tableTop + 65,
                        ContentWidth,
                        35,
                        13,
                        Brushes.Gray,
                        FontWeights.SemiBold,
                        TextAlignment.Center);
                }

                AddPageFooter(page, pageNumber, totalPages);

                var pageContent = new PageContent
                {
                    Child = page
                };

                document.Pages.Add(pageContent);
            }

            return document;
        }


        private static FixedPage CreatePage()
        {
            return new FixedPage
            {
                Width = PageWidth,
                Height = PageHeight,
                Background = Brushes.White
            };
        }

        private static void AddPageHeading(
            FixedPage page,
            int pageNumber,
            PatientMasterListReport report)
        {
            if (pageNumber == 1)
            {
                AddText(
                    page,
                    "GOLDDIE · CLINIC MANAGEMENT",
                    MarginSize,
                    32,
                    ContentWidth,
                    20,
                    11,
                    Brushes.DarkSlateBlue,
                    FontWeights.Bold,
                    TextAlignment.Center);

                AddText(
                    page,
                    "PATIENT MASTER LIST",
                    MarginSize,
                    55,
                    ContentWidth,
                    30,
                    20,
                    Brushes.Black,
                    FontWeights.Bold,
                    TextAlignment.Center);

                AddText(
                    page,
                    "Registered Patient Records",
                    MarginSize,
                    86,
                    ContentWidth,
                    20,
                    10,
                    Brushes.Gray,
                    FontWeights.Normal,
                    TextAlignment.Center);

                var generatedAt = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(
                        report.GeneratedAtUtc,
                        DateTimeKind.Utc),
                    ClinicTimeZone);

                AddText(
                    page,
                    $"Generated: {generatedAt:MMM dd, yyyy hh:mm tt} (UTC+8)",
                    MarginSize,
                    120,
                    ContentWidth,
                    20,
                    10,
                    Brushes.Black,
                    FontWeights.Normal);

                AddText(
                    page,
                    $"Registration From: {report.StartDate?.ToString("MMM dd, yyyy") ?? "Any"}",
                    MarginSize,
                    145,
                    450,
                    20,
                    10,
                    Brushes.Black,
                    FontWeights.Normal);

                AddText(
                    page,
                    $"Registration To: {report.EndDate?.ToString("MMM dd, yyyy") ?? "Any"}",
                    MarginSize + 480,
                    145,
                    450,
                    20,
                    10,
                    Brushes.Black,
                    FontWeights.Normal);

                AddText(
                    page,
                    $"Sex: {report.Sex ?? "All"}",
                    MarginSize,
                    170,
                    450,
                    20,
                    10,
                    Brushes.Black,
                    FontWeights.Normal);

                AddText(
                    page,
                    $"Search Term: {report.SearchTerm ?? "None"}",
                    MarginSize + 480,
                    170,
                    450,
                    20,
                    10,
                    Brushes.Black,
                    FontWeights.Normal);

                AddText(
                    page,
                    $"TOTAL MATCHING PATIENTS: {report.TotalPatients}",
                    MarginSize,
                    195,
                    ContentWidth,
                    20,
                    11,
                    Brushes.DarkSlateBlue,
                    FontWeights.Bold);
            }
            else
            {
                AddText(
                    page,
                    "GOLDDIE · CLINIC MANAGEMENT",
                    MarginSize,
                    35,
                    ContentWidth,
                    20,
                    10,
                    Brushes.DarkSlateBlue,
                    FontWeights.Bold);

                AddText(
                    page,
                    "PATIENT MASTER LIST",
                    MarginSize,
                    57,
                    ContentWidth,
                    22,
                    14,
                    Brushes.Black,
                    FontWeights.Bold);
            }
        }

        private static void AddTableHeader(
            FixedPage page,
            double top)
        {
            var left = MarginSize;

            for (var i = 0; i < Headers.Length; i++)
            {
                AddCell(
                    page,
                    Headers[i],
                    left,
                    top,
                    ColumnWidths[i],
                    34,
                    Brushes.DarkSlateBlue,
                    Brushes.White,
                    true);

                left += ColumnWidths[i];
            }
        }

        private static void AddPatientRow(
            FixedPage page,
            Patient patient,
            double top,
            double height,
            bool alternate)
        {
            var fullName = string.Join(
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
                patient.PatientNumber,
                fullName,
                patient.DateOfBirth?.ToString("MMM dd, yyyy") ?? "—",
                patient.Sex ?? "",
                patient.CivilStatus ?? "",
                patient.ContactNumber ?? "",
                patient.Address ?? ""
            };

            var left = MarginSize;

            var background = alternate
                ? Brushes.WhiteSmoke
                : Brushes.White;

            for (var i = 0; i < values.Length; i++)
            {
                AddCell(
                    page,
                    values[i],
                    left,
                    top,
                    ColumnWidths[i],
                    height,
                    background,
                    Brushes.Black,
                    false);

                left += ColumnWidths[i];
            }
        }

        private static void AddCell(
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
                FontSize = isHeader ? 9 : 8,
                FontWeight = isHeader
                    ? FontWeights.Bold
                    : FontWeights.Normal,
                Foreground = foreground,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var border = new Border
            {
                Width = width,
                Height = height,
                Background = background,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(5, 3, 5, 3),
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
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            FixedPage.SetLeft(textBlock, left);
            FixedPage.SetTop(textBlock, top);

            page.Children.Add(textBlock);
        }

        private static void AddPageFooter(
            FixedPage page,
            int pageNumber,
            int totalPages)
        {
            var footerTop = PageHeight - 35;

            var separator = new Border
            {
                Width = ContentWidth,
                Height = 0.7,
                Background = Brushes.LightGray
            };

            FixedPage.SetLeft(separator, MarginSize);
            FixedPage.SetTop(separator, footerTop);

            page.Children.Add(separator);

            AddText(
                page,
                "GOLDDIE · CLINIC MANAGEMENT",
                MarginSize,
                footerTop + 5,
                ContentWidth / 2,
                18,
                8,
                Brushes.Gray,
                FontWeights.Normal);

            AddText(
                page,
                $"Page {pageNumber} of {totalPages}",
                MarginSize + ContentWidth / 2,
                footerTop + 5,
                ContentWidth / 2,
                18,
                8,
                Brushes.Gray,
                FontWeights.Normal,
                TextAlignment.Right);
        }
    }
}
