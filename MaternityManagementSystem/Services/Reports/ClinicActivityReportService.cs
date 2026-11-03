
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.Services.Reports;

public class ClinicActivityReportService
{
    private static readonly TimeZoneInfo ClinicTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

    private readonly PatientService _patientService;
    private readonly PregnancyService _pregnancyService;
    private readonly PrenatalVisitService _prenatalVisitService;
    private readonly LaboratoryResultService _laboratoryResultService;
    private readonly AppointmentService _appointmentService;

    public ClinicActivityReportService(
        PatientService patientService,
        PregnancyService pregnancyService,
        PrenatalVisitService prenatalVisitService,
        LaboratoryResultService laboratoryResultService,
        AppointmentService appointmentService)
    {
        ArgumentNullException.ThrowIfNull(patientService);
        ArgumentNullException.ThrowIfNull(pregnancyService);
        ArgumentNullException.ThrowIfNull(prenatalVisitService);
        ArgumentNullException.ThrowIfNull(laboratoryResultService);
        ArgumentNullException.ThrowIfNull(appointmentService);

        _patientService = patientService;
        _pregnancyService = pregnancyService;
        _prenatalVisitService = prenatalVisitService;
        _laboratoryResultService = laboratoryResultService;
        _appointmentService = appointmentService;
    }

    public async Task<ClinicActivityReport> GenerateClinicActivityReportAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            startDate.Value > endDate.Value)
        {
            throw new ArgumentException(
                "The start date cannot be later than the end date.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var report = new ClinicActivityReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate
        };

        // Overall activity totals

        report.TotalPatientRegistrations =
            await _patientService
                .GetPatientRegistrationCountForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        report.TotalPregnancyRecords =
            await _pregnancyService
                .GetPregnancyRegistrationCountForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        report.TotalPrenatalVisits =
            await _prenatalVisitService
                .GetPrenatalVisitCountForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        report.TotalLaboratoryResults =
            await _laboratoryResultService
                .GetLaboratoryResultCountForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        report.TotalAppointments =
            await _appointmentService
                .GetAppointmentCountForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        // Appointment status breakdown.
        // These are the current statuses of appointments in the selected period.

        var appointmentStatuses =
            await _appointmentService
                .GetStatusesForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        report.ScheduledAppointments = CountStatus(
            appointmentStatuses, "Scheduled");

        report.ConfirmedAppointments = CountStatus(
            appointmentStatuses, "Confirmed");

        report.CompletedAppointments = CountStatus(
            appointmentStatuses, "Completed");

        report.CancelledAppointments = CountStatus(
            appointmentStatuses, "Cancelled");

        report.NoShowAppointments = CountStatus(
            appointmentStatuses, "No-show");

        // Unique patients with recorded activity.
        // Registration and pregnancy creation alone do not count.

        var servedPatientIds = new HashSet<int>();

        var prenatalPatientIds =
            await _prenatalVisitService
                .GetPatientIdsForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        var laboratoryPatientIds =
            await _laboratoryResultService
                .GetPatientIdsForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        var appointmentPatientIds =
            await _appointmentService
                .GetPatientIdsForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        servedPatientIds.UnionWith(prenatalPatientIds);
        servedPatientIds.UnionWith(laboratoryPatientIds);
        servedPatientIds.UnionWith(appointmentPatientIds);

        report.UniquePatientsServed = servedPatientIds.Count;

        // Retrieve dates for monthly activity.

        var patientRegistrationDates =
            await _patientService
                .GetPatientRegistrationDatesForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        var pregnancyRegistrationDates =
            await _pregnancyService
                .GetPregnancyRegistrationDatesForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        var prenatalVisitDates =
            await _prenatalVisitService
                .GetPrenatalVisitDatesForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        var laboratoryResultDates =
            await _laboratoryResultService
                .GetLaboratoryResultDatesForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        var appointmentDates =
            await _appointmentService
                .GetAppointmentDatesForClinicActivityReportAsync(
                    startDate,
                    endDate,
                    cancellationToken);

        // Convert UTC registration timestamps to Philippine local dates.

        var patientRegistrationLocalDates =
            patientRegistrationDates
                .Select(ConvertUtcToClinicDate)
                .ToList();

        var pregnancyRegistrationLocalDates =
            pregnancyRegistrationDates
                .Select(ConvertUtcToClinicDate)
                .ToList();

        // Determine the monthly reporting period.

        var activityMonths = new List<DateOnly>();

        activityMonths.AddRange(
            patientRegistrationLocalDates.Select(GetMonthStart));

        activityMonths.AddRange(
            pregnancyRegistrationLocalDates.Select(GetMonthStart));

        activityMonths.AddRange(
            prenatalVisitDates.Select(GetMonthStart));

        activityMonths.AddRange(
            laboratoryResultDates.Select(GetMonthStart));

        activityMonths.AddRange(
            appointmentDates.Select(GetMonthStart));

        DateOnly firstMonth;
        DateOnly lastMonth;

        if (startDate.HasValue && endDate.HasValue)
        {
            firstMonth = GetMonthStart(startDate.Value);
            lastMonth = GetMonthStart(endDate.Value);
        }
        else if (startDate.HasValue)
        {
            firstMonth = GetMonthStart(startDate.Value);
            lastMonth = activityMonths.Count > 0
                ? activityMonths.Max()
                : firstMonth;
        }
        else if (endDate.HasValue)
        {
            lastMonth = GetMonthStart(endDate.Value);
            firstMonth = activityMonths.Count > 0
                ? activityMonths.Min()
                : lastMonth;
        }
        else
        {
            firstMonth = activityMonths.Count > 0
                ? activityMonths.Min()
                : GetMonthStart(DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.UtcNow,
                        ClinicTimeZone)));

            lastMonth = activityMonths.Count > 0
                ? activityMonths.Max()
                : firstMonth;
        }

        // Create chronological monthly buckets, including months with no activity.

        var monthlySummaries = new Dictionary<DateOnly, ClinicActivityMonthlySummary>();

        for (DateOnly month = firstMonth;
             month <= lastMonth;
             month = month.AddMonths(1))
        {
            monthlySummaries[month] = new ClinicActivityMonthlySummary
            {
                Month = month.ToString("MMM yyyy")
            };
        }

        // Populate monthly registration counts.

        foreach (DateOnly date in patientRegistrationLocalDates)
        {
            DateOnly month = GetMonthStart(date);

            if (monthlySummaries.TryGetValue(month, out var summary))
            {
                summary.PatientRegistrations++;
            }
        }

        foreach (DateOnly date in pregnancyRegistrationLocalDates)
        {
            DateOnly month = GetMonthStart(date);

            if (monthlySummaries.TryGetValue(month, out var summary))
            {
                summary.PregnancyRecords++;
            }
        }

        // Populate monthly clinical activity counts.

        foreach (DateOnly date in prenatalVisitDates)
        {
            DateOnly month = GetMonthStart(date);

            if (monthlySummaries.TryGetValue(month, out var summary))
            {
                summary.PrenatalVisits++;
            }
        }

        foreach (DateOnly date in laboratoryResultDates)
        {
            DateOnly month = GetMonthStart(date);

            if (monthlySummaries.TryGetValue(month, out var summary))
            {
                summary.LaboratoryResults++;
            }
        }

        foreach (DateOnly date in appointmentDates)
        {
            DateOnly month = GetMonthStart(date);

            if (monthlySummaries.TryGetValue(month, out var summary))
            {
                summary.Appointments++;
            }
        }

        report.MonthlyActivity = monthlySummaries
            .OrderBy(item => item.Key)
            .Select(item => item.Value)
            .ToList();

        return report;
    }

    private static int CountStatus(
        IEnumerable<string> statuses,
        string expectedStatus)
    {
        return statuses.Count(status =>
            string.Equals(
                status,
                expectedStatus,
                StringComparison.OrdinalIgnoreCase));
    }

    private static DateOnly ConvertUtcToClinicDate(DateTime utcDate)
    {
        DateTime normalizedUtc = DateTime.SpecifyKind(
            utcDate,
            DateTimeKind.Utc);

        DateTime clinicDateTime =
            TimeZoneInfo.ConvertTimeFromUtc(
                normalizedUtc,
                ClinicTimeZone);

        return DateOnly.FromDateTime(clinicDateTime);
    }

    private static DateOnly GetMonthStart(DateOnly date)
    {
        return new DateOnly(date.Year, date.Month, 1);
    }
}
