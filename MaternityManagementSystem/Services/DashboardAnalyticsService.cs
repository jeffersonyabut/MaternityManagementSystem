
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;

namespace MaternityManagementSystem.Services;

public class DashboardAnalyticsService : IDashboardAnalyticsService
{
    private static readonly string[] AppointmentStatuses =
    {
        "Scheduled",
        "Confirmed",
        "Completed",
        "Cancelled",
        "No-show"
    };

    private readonly AuthorizationService _authorizationService;
    private readonly RegionalSettingsService _regionalSettingsService;
    private readonly IDbContextFactory<ClinicDbContext> _contextFactory;
    private readonly IClock _clock;

    public DashboardAnalyticsService(
        AuthorizationService authorizationService,
        RegionalSettingsService regionalSettingsService,
        IDbContextFactory<ClinicDbContext> contextFactory,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(clock);

        _authorizationService = authorizationService;
        _regionalSettingsService = regionalSettingsService;
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task<DashboardData> GetDashboardDataAsync(
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAuthenticatedAsync();

        await using var dbContext =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        DateTime localNow =
            _regionalSettingsService.ConvertUtcToLocal(
                _clock.UtcNow);

        DateOnly today = DateOnly.FromDateTime(localNow);
        TimeOnly currentTime = TimeOnly.FromDateTime(localNow);

        DateOnly firstDayOfCurrentMonth =
            new(today.Year, today.Month, 1);

        DateOnly firstMonth =
            firstDayOfCurrentMonth.AddMonths(-5);

        DateTime firstMonthLocal = firstMonth.ToDateTime(
            TimeOnly.MinValue);

        DateTime nextMonthLocal =
            firstDayOfCurrentMonth.AddMonths(1).ToDateTime(
                TimeOnly.MinValue);

        DateTime firstMonthUtc =
            _regionalSettingsService.ConvertLocalToUtc(
                firstMonthLocal);

        DateTime nextMonthUtc =
            _regionalSettingsService.ConvertLocalToUtc(
                nextMonthLocal);

        // Summary statistics
        int totalPatients = await dbContext.Patients
            .AsNoTracking()
            .CountAsync(cancellationToken);

        int activePregnancies = await dbContext.Pregnancies
            .AsNoTracking()
            .CountAsync(
                pregnancy => pregnancy.Status == "Current",
                cancellationToken);

        int todaysAppointments = await dbContext.Appointments
            .AsNoTracking()
            .CountAsync(
                appointment =>
                    !appointment.IsArchived &&
                    appointment.AppointmentDate == today &&
                    (appointment.Status == "Scheduled" ||
                     appointment.Status == "Confirmed"),
                cancellationToken);

        int totalPrenatalVisits = await dbContext.PrenatalVisits
            .AsNoTracking()
            .CountAsync(cancellationToken);

        // Monthly patient registrations
        // Retrieve timestamps within the six-month UTC range.
        var patientRegistrationTimestamps =
            await dbContext.Patients
                .AsNoTracking()
                .Where(patient =>
                    patient.CreatedAtUtc >= firstMonthUtc &&
                    patient.CreatedAtUtc < nextMonthUtc)
                .Select(patient => patient.CreatedAtUtc)
                .ToListAsync(cancellationToken);

        // Convert timestamps to clinic-local time before grouping.
        var patientRegistrationCounts =
            patientRegistrationTimestamps
                .Select(createdAtUtc =>
                    _regionalSettingsService.ConvertUtcToLocal(
                        createdAtUtc))
                .GroupBy(localDateTime => (
                    localDateTime.Year,
                    localDateTime.Month))
                .ToDictionary(
                    group => group.Key,
                    group => group.Count());

        var monthlyRegistrations = Enumerable
            .Range(0, 6)
            .Select(offset =>
            {
                DateOnly month = firstMonth.AddMonths(offset);

                patientRegistrationCounts.TryGetValue(
                    (month.Year, month.Month),
                    out int count);

                return new MonthlyStatistic
                {
                    Label = month.ToString("MMM yyyy"),
                    Count = count
                };
            })
            .ToList();

        // Monthly prenatal visits
        var prenatalVisitCounts =
            await dbContext.PrenatalVisits
                .AsNoTracking()
                .Where(visit =>
                    visit.VisitDate >= firstMonth &&
                    visit.VisitDate <
                        firstDayOfCurrentMonth.AddMonths(1))
                .GroupBy(visit => new
                {
                    visit.VisitDate.Year,
                    visit.VisitDate.Month
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

        var monthlyPrenatalVisits = Enumerable
            .Range(0, 6)
            .Select(offset =>
            {
                DateOnly month = firstMonth.AddMonths(offset);

                int count = prenatalVisitCounts
                    .FirstOrDefault(item =>
                        item.Year == month.Year &&
                        item.Month == month.Month)?.Count ?? 0;

                return new MonthlyStatistic
                {
                    Label = month.ToString("MMM yyyy"),
                    Count = count
                };
            })
            .ToList();

        // Appointment status distribution
        // Archived appointments are excluded.
        var appointmentStatusCounts =
            await dbContext.Appointments
                .AsNoTracking()
                .Where(appointment => !appointment.IsArchived)
                .GroupBy(appointment => appointment.Status)
                .Select(group => new
                {
                    Status = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

        var appointmentDistribution = AppointmentStatuses
            .Select(status => new AppointmentStatusStatistic
            {
                Status = status,
                Count = appointmentStatusCounts
                    .FirstOrDefault(item =>
                        item.Status == status)?.Count ?? 0
            })
            .ToList();

        // Upcoming appointments
        // Only future, non-archived Scheduled and Confirmed appointments.
        var upcomingAppointments =
            await dbContext.Appointments
                .AsNoTracking()
                .Where(appointment =>
                    !appointment.IsArchived &&
                    (appointment.Status == "Scheduled" ||
                     appointment.Status == "Confirmed") &&
                    (
                        appointment.AppointmentDate > today ||
                        (
                            appointment.AppointmentDate == today &&
                            appointment.StartTime > currentTime
                        )
                    ))
                .OrderBy(appointment => appointment.AppointmentDate)
                .ThenBy(appointment => appointment.StartTime)
                .Take(5)
                .Select(appointment => new UpcomingAppointment
                {
                    AppointmentId = appointment.Id,
                    PatientName =
                        appointment.Patient.FirstName + " " +
                        appointment.Patient.LastName,
                    AppointmentDate = appointment.AppointmentDate,
                    StartTime = appointment.StartTime,
                    Purpose = appointment.Purpose,
                    Status = appointment.Status
                })
                .ToListAsync(cancellationToken);

        return new DashboardData
        {
            TotalPatients = totalPatients,
            ActivePregnancies = activePregnancies,
            TodaysAppointments = todaysAppointments,
            TotalPrenatalVisits = totalPrenatalVisits,
            MonthlyRegistrations = monthlyRegistrations,
            MonthlyPrenatalVisits = monthlyPrenatalVisits,
            AppointmentDistribution = appointmentDistribution,
            UpcomingAppointments = upcomingAppointments
        };
    }
}

public class DashboardData
{
    public int TotalPatients { get; set; }

    public int ActivePregnancies { get; set; }

    public int TodaysAppointments { get; set; }

    public int TotalPrenatalVisits { get; set; }

    public List<MonthlyStatistic> MonthlyRegistrations { get; set; } = new();

    public List<MonthlyStatistic> MonthlyPrenatalVisits { get; set; } = new();

    public List<AppointmentStatusStatistic> AppointmentDistribution { get; set; } = new();

    public List<UpcomingAppointment> UpcomingAppointments { get; set; } = new();
}

public class MonthlyStatistic
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class AppointmentStatusStatistic
{
    public string Status { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class UpcomingAppointment
{
    public int AppointmentId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public DateOnly AppointmentDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
