using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services.Reports;
using SkiaSharp;

namespace MaternityManagementSystem.ViewModels.Reports;

public partial class ClinicActivityReportViewModel : ObservableObject
{
    private readonly ClinicActivityReportService _reportService;

    private static readonly SKColor PrimaryPurple = new(121, 82, 179);
    private static readonly SKColor LightPurple = new(167, 139, 202);
    private static readonly SKColor SoftPurple = new(216, 201, 234);
    private static readonly SKColor PalePurple = new(238, 231, 246);
    private static readonly SKColor DarkPurple = new(91, 61, 137);

    [ObservableProperty]
    private DateTime? startDate;

    [ObservableProperty]
    private DateTime? endDate;

    [ObservableProperty]
    private ClinicActivityReport? report;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private Axis[] monthlyActivityXAxes = { new Axis() };

    [ObservableProperty]
    private Axis[] monthlyActivityYAxes = { new Axis() };

    public ObservableCollection<ISeries> MonthlyActivitySeries { get; } = new();

    public bool HasGeneratedReport => Report is not null;

    public bool IsNotLoading => !IsLoading;

    public bool HasMonthlyActivity =>
        Report is not null && Report.MonthlyActivity.Count > 0;

    public ClinicActivityReportViewModel(
        ClinicActivityReportService reportService)
    {
        ArgumentNullException.ThrowIfNull(reportService);
        _reportService = reportService;
    }

    partial void OnReportChanged(ClinicActivityReport? value)
    {
        OnPropertyChanged(nameof(HasGeneratedReport));
        OnPropertyChanged(nameof(HasMonthlyActivity));

        UpdateMonthlyActivityChart();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
    }

    private void UpdateMonthlyActivityChart()
    {
        MonthlyActivitySeries.Clear();

        if (Report is null || Report.MonthlyActivity.Count == 0)
        {
            MonthlyActivityXAxes = new[]
            {
                new Axis()
            };

            MonthlyActivityYAxes = new[]
            {
                new Axis()
            };
            return;
        }

        var monthlyData = Report.MonthlyActivity;

        MonthlyActivitySeries.Add(new ColumnSeries<int>
        {
            Name = "Patient Registrations",
            Values = monthlyData.Select(x => x.PatientRegistrations).ToArray(),
            Fill = new SolidColorPaint(PrimaryPurple),
            Stroke = null,
            Padding = 5,
            Rx = 4,
            Ry = 4
        });

        MonthlyActivitySeries.Add(new ColumnSeries<int>
        {
            Name = "Pregnancy Records",
            Values = monthlyData.Select(x => x.PregnancyRecords).ToArray(),
            Fill = new SolidColorPaint(LightPurple),
            Stroke = null,
            Padding = 5,
            Rx = 4,
            Ry = 4
        });

        MonthlyActivitySeries.Add(new ColumnSeries<int>
        {
            Name = "Prenatal Visits",
            Values = monthlyData.Select(x => x.PrenatalVisits).ToArray(),
            Fill = new SolidColorPaint(SoftPurple),
            Stroke = null,
            Padding = 5,
            Rx = 4,
            Ry = 4
        });

        MonthlyActivitySeries.Add(new ColumnSeries<int>
        {
            Name = "Laboratory Results",
            Values = monthlyData.Select(x => x.LaboratoryResults).ToArray(),
            Fill = new SolidColorPaint(PalePurple),
            Stroke = null,
            Padding = 5,
            Rx = 4,
            Ry = 4
        });

        MonthlyActivitySeries.Add(new ColumnSeries<int>
        {
            Name = "Appointments",
            Values = monthlyData.Select(x => x.Appointments).ToArray(),
            Fill = new SolidColorPaint(DarkPurple),
            Stroke = null,
            Padding = 5,
            Rx = 4,
            Ry = 4
        });

        MonthlyActivityXAxes = new[]
        {
            new Axis
            {
                Labels = monthlyData
                    .Select(x => x.Month)
                    .ToArray(),

                LabelsPaint = new SolidColorPaint(
                    new SKColor(119, 113, 132)),

                TextSize = 11,
                SeparatorsPaint = null
            }
        };

        MonthlyActivityYAxes = new[]
        {
            new Axis
            {
                MinLimit = 0,

                LabelsPaint = new SolidColorPaint(
                    new SKColor(119, 113, 132)),

                TextSize = 11,

                SeparatorsPaint = new SolidColorPaint(
                    new SKColor(234, 231, 240))
                {
                    StrokeThickness = 1
                }
            }
        };
    }

    [RelayCommand]
    private async Task GenerateReportAsync(
        CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        if (StartDate.HasValue &&
            EndDate.HasValue &&
            StartDate.Value.Date > EndDate.Value.Date)
        {
            ErrorMessage =
                "The start date cannot be later than the end date.";

            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            Report = null;

            DateOnly? reportStartDate = StartDate.HasValue
                ? DateOnly.FromDateTime(StartDate.Value)
                : null;

            DateOnly? reportEndDate = EndDate.HasValue
                ? DateOnly.FromDateTime(EndDate.Value)
                : null;

            Report = await _reportService.GenerateClinicActivityReportAsync(
                reportStartDate,
                reportEndDate,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Report generation was cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        StartDate = null;
        EndDate = null;
        Report = null;
        ErrorMessage = null;
    }
}
