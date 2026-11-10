
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MaternityManagementSystem.Services;
using SkiaSharp;

namespace MaternityManagementSystem.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DashboardAnalyticsService _analyticsService;

    private static readonly SKColor PrimaryPurple = new(121, 82, 179);
    private static readonly SKColor LightPurple = new(167, 139, 202);
    private static readonly SKColor SoftPurple = new(216, 201, 234);
    private static readonly SKColor PalePurple = new(238, 231, 246);

    [ObservableProperty]
    private int totalPatients;

    [ObservableProperty]
    private int activePregnancies;

    [ObservableProperty]
    private int todaysAppointments;

    [ObservableProperty]
    private int totalPrenatalVisits;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private Axis[] registrationXAxis = { new Axis() };

    [ObservableProperty]
    private Axis[] registrationYAxis = { new Axis() };

    [ObservableProperty]
    private Axis[] prenatalXAxis = { new Axis() };

    [ObservableProperty]
    private Axis[] prenatalYAxis = { new Axis() };

    public ObservableCollection<MonthlyStatistic> MonthlyRegistrations { get; } = new();

    public ObservableCollection<MonthlyStatistic> MonthlyPrenatalVisits { get; } = new();

    public ObservableCollection<AppointmentStatusStatistic> AppointmentDistribution { get; } = new();

    public ObservableCollection<UpcomingAppointment> UpcomingAppointments { get; } = new();

    public ObservableCollection<ISeries> RegistrationSeries { get; } = new();

    public ObservableCollection<ISeries> PrenatalVisitSeries { get; } = new();

    public ObservableCollection<ISeries> AppointmentStatusSeries { get; } = new();

    public DashboardViewModel()
        : this(new DashboardAnalyticsService(new AuthorizationService(new SessionService())))
    {
    }

    public DashboardViewModel(DashboardAnalyticsService analyticsService)
    {
        ArgumentNullException.ThrowIfNull(analyticsService);
        _analyticsService = analyticsService;
    }

    public async Task LoadDashboardAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var data = await _analyticsService.GetDashboardDataAsync();

            TotalPatients = data.TotalPatients;
            ActivePregnancies = data.ActivePregnancies;
            TodaysAppointments = data.TodaysAppointments;
            TotalPrenatalVisits = data.TotalPrenatalVisits;

            ReplaceItems(MonthlyRegistrations, data.MonthlyRegistrations);
            ReplaceItems(MonthlyPrenatalVisits, data.MonthlyPrenatalVisits);
            ReplaceItems(AppointmentDistribution, data.AppointmentDistribution);
            ReplaceItems(UpcomingAppointments, data.UpcomingAppointments);

            UpdateRegistrationChart();
            UpdatePrenatalVisitChart();
            UpdateAppointmentStatusChart();
        }
        catch (Exception)
        {
            ErrorMessage =
                "Dashboard information could not be loaded. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateRegistrationChart()
    {
        RegistrationSeries.Clear();

        RegistrationSeries.Add(new LineSeries<int>
        {
            Name = "New Patients",
            Values = MonthlyRegistrations
                .Select(item => item.Count)
                .ToArray(),
            LineSmoothness = 0.35,
            Stroke = new SolidColorPaint(PrimaryPurple)
            {
                StrokeThickness = 3
            },
            Fill = new SolidColorPaint(
                new SKColor(121, 82, 179, 30)),
            GeometrySize = 10,
            GeometryFill = new SolidColorPaint(PrimaryPurple),
            GeometryStroke = new SolidColorPaint(SKColors.White)
            {
                StrokeThickness = 2
            }
        });

        RegistrationXAxis = new[]
        {
            new Axis
            {
                Labels = MonthlyRegistrations
                    .Select(item => item.Label)
                    .ToArray(),
                LabelsPaint = new SolidColorPaint(
                    new SKColor(119, 113, 132)),
                TextSize = 11,
                SeparatorsPaint = null
            }
        };

        RegistrationYAxis = new[]
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

    private void UpdatePrenatalVisitChart()
    {
        PrenatalVisitSeries.Clear();

        PrenatalVisitSeries.Add(new ColumnSeries<int>
        {
            Name = "Prenatal Visits",
            Values = MonthlyPrenatalVisits
                .Select(item => item.Count)
                .ToArray(),
            Fill = new SolidColorPaint(PrimaryPurple),
            Stroke = null,
            Padding = 10,
            Rx = 6,
            Ry = 6
        });

        PrenatalXAxis = new[]
        {
            new Axis
            {
                Labels = MonthlyPrenatalVisits
                    .Select(item => item.Label)
                    .ToArray(),
                LabelsPaint = new SolidColorPaint(
                    new SKColor(119, 113, 132)),
                TextSize = 11,
                SeparatorsPaint = null
            }
        };

        PrenatalYAxis = new[]
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

    private void UpdateAppointmentStatusChart()
    {
        AppointmentStatusSeries.Clear();

        var statuses = new[]
        {
            new { Name = "Scheduled", Color = PrimaryPurple },
            new { Name = "Confirmed", Color = LightPurple },
            new { Name = "Completed", Color = SoftPurple },
            new { Name = "Cancelled", Color = new SKColor(180, 160, 195) }
        };

        foreach (var status in statuses)
        {
            int count = AppointmentDistribution
                .FirstOrDefault(item => item.Status == status.Name)?.Count ?? 0;

            AppointmentStatusSeries.Add(new PieSeries<int>
            {
                Name = status.Name,
                Values = new[] { count },
                IsHoverable = count > 0,
                IsVisibleAtLegend = true,
                InnerRadius = 65,
                Pushout = 2,
                Fill = new SolidColorPaint(status.Color),
                Stroke = new SolidColorPaint(SKColors.White)
                {
                    StrokeThickness = 3
                },
                AnimationsSpeed = TimeSpan.FromMilliseconds(500)
            });
        }
    }

    private static void ReplaceItems<T>(
        ObservableCollection<T> collection,
        IEnumerable<T> items)
    {
        collection.Clear();

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
