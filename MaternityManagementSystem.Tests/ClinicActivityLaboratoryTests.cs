using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class ClinicActivityLaboratoryTests
{
    private static readonly DateOnly OctoberDate = new(2026, 10, 15);
    private static readonly DateOnly NovemberDate = new(2026, 11, 2);

    [Fact]
    public async Task ClinicActivityQueries_UseLifecycleDatesAcrossAllRangeShapes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var patientIds = await SeedRangeScenarioAsync(database);
        var repository = new LaboratoryResultRepository(database.Factory);

        await AssertRangeAsync(
            repository,
            patientIds,
            null,
            null,
            expectedCount: 11,
            expectedDates:
            [
                OctoberDate.AddDays(-1),
                OctoberDate.AddDays(-1),
                OctoberDate,
                OctoberDate,
                OctoberDate,
                NovemberDate,
                NovemberDate,
                NovemberDate
            ],
            expectedPatientNumbers:
            ["P-1", "P-2", "P-3", "P-4", "P-5", "P-6", "P-7", "P-8", "P-9"]);

        await AssertRangeAsync(
            repository,
            patientIds,
            OctoberDate,
            null,
            expectedCount: 6,
            expectedDates:
            [OctoberDate, OctoberDate, OctoberDate, NovemberDate, NovemberDate, NovemberDate],
            expectedPatientNumbers: ["P-1", "P-3", "P-5", "P-6"]);

        await AssertRangeAsync(
            repository,
            patientIds,
            null,
            OctoberDate,
            expectedCount: 5,
            expectedDates:
            [OctoberDate.AddDays(-1), OctoberDate.AddDays(-1), OctoberDate, OctoberDate, OctoberDate],
            expectedPatientNumbers: ["P-1", "P-2", "P-3", "P-4", "P-6"]);

        await AssertRangeAsync(
            repository,
            patientIds,
            OctoberDate,
            OctoberDate,
            expectedCount: 3,
            expectedDates: [OctoberDate, OctoberDate, OctoberDate],
            expectedPatientNumbers: ["P-1", "P-3", "P-6"]);
    }

    [Fact]
    public async Task ClinicActivityReport_IncludesUnassignedCountAndReconcilesDatedMonthlyRecords()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sessionService = await SeedReportScenarioAsync(database);
        var reportService = CreateReportService(database, sessionService);

        var allDatesReport =
            await reportService.GenerateClinicActivityReportAsync(null, null);

        Assert.Equal(4, allDatesReport.TotalLaboratoryResults);
        Assert.Equal(1, allDatesReport.UnassignedActivityDateCount);
        Assert.Equal(1, allDatesReport.TotalPrenatalVisits);
        Assert.Equal(1, allDatesReport.TotalAppointments);
        Assert.Equal(3, allDatesReport.UniquePatientsServed);
        Assert.Equal(2, MonthlyLaboratoryRecords(allDatesReport, "Oct 2026"));
        Assert.Equal(1, MonthlyLaboratoryRecords(allDatesReport, "Nov 2026"));
        Assert.Equal(
            allDatesReport.TotalLaboratoryResults,
            allDatesReport.MonthlyActivity.Sum(item => item.LaboratoryResults) +
            allDatesReport.UnassignedActivityDateCount);

        var filteredReport =
            await reportService.GenerateClinicActivityReportAsync(
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 31));

        Assert.Equal(2, filteredReport.TotalLaboratoryResults);
        Assert.Equal(0, filteredReport.UnassignedActivityDateCount);
        Assert.Equal(2, filteredReport.MonthlyActivity.Sum(
            item => item.LaboratoryResults));
        Assert.Equal(1, filteredReport.TotalPrenatalVisits);
        Assert.Equal(1, filteredReport.TotalAppointments);
        Assert.Equal(1, filteredReport.UniquePatientsServed);
    }

    private static async Task AssertRangeAsync(
        LaboratoryResultRepository repository,
        IReadOnlyDictionary<string, int> patientIds,
        DateOnly? startDate,
        DateOnly? endDate,
        int expectedCount,
        DateOnly[] expectedDates,
        string[] expectedPatientNumbers)
    {
        int count = await repository
            .GetLaboratoryResultCountForClinicActivityReportAsync(
                startDate,
                endDate);

        var dates = await repository.GetTestDatesForClinicActivityReportAsync(
            startDate,
            endDate);

        var ids = await repository.GetPatientIdsForClinicActivityReportAsync(
            startDate,
            endDate);

        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedDates.OrderBy(date => date), dates);
        Assert.Equal(
            expectedPatientNumbers.Select(number => patientIds[number]).OrderBy(id => id),
            ids.OrderBy(id => id));
    }

    private static async Task<IReadOnlyDictionary<string, int>>
        SeedRangeScenarioAsync(TestDatabase database)
    {
        await using var context = await database.Factory.CreateDbContextAsync();
        var patients = Enumerable.Range(1, 10)
            .Select(number => CreatePatient($"P-{number}"))
            .ToArray();
        context.Patients.AddRange(patients);
        await context.SaveChangesAsync();

        int PatientId(int number) => patients[number - 1].Id;

        context.LaboratoryResults.AddRange(
            CreateResult(PatientId(1), "Requested on", LaboratoryResultStatus.Requested,
                requestedDate: OctoberDate),
            CreateResult(PatientId(1), "Requested in November", LaboratoryResultStatus.Requested,
                requestedDate: NovemberDate),
            CreateResult(PatientId(2), "Requested before", LaboratoryResultStatus.Requested,
                requestedDate: OctoberDate.AddDays(-1)),
            CreateResult(PatientId(3), "Awaiting on", LaboratoryResultStatus.AwaitingResult,
                testDate: OctoberDate),
            CreateResult(PatientId(3), "Awaiting in November", LaboratoryResultStatus.AwaitingResult,
                testDate: NovemberDate),
            CreateResult(PatientId(4), "Resulted before", LaboratoryResultStatus.Resulted,
                testDate: OctoberDate.AddDays(-1)),
            CreateResult(PatientId(5), "Resulted in November", LaboratoryResultStatus.Resulted,
                testDate: NovemberDate),
            CreateResult(PatientId(6), "Unknown on", status: null,
                requestedDate: NovemberDate,
                testDate: OctoberDate),
            CreateResult(PatientId(7), "Requested without date",
                LaboratoryResultStatus.Requested),
            CreateResult(PatientId(8), "Unknown without test date", status: null,
                requestedDate: OctoberDate),
            CreateResult(PatientId(9), "Awaiting without test date",
                LaboratoryResultStatus.AwaitingResult,
                requestedDate: OctoberDate),
            new LaboratoryResult
            {
                PatientId = PatientId(10),
                TestName = "Archived",
                Status = LaboratoryResultStatus.Resulted,
                RequestedDate = OctoberDate,
                TestDate = OctoberDate,
                IsArchived = true
            });

        await context.SaveChangesAsync();

        return patients.ToDictionary(
            patient => patient.PatientNumber,
            patient => patient.Id);
    }

    private static async Task<SessionService> SeedReportScenarioAsync(
        TestDatabase database)
    {
        await using var context = await database.Factory.CreateDbContextAsync();
        var staff = new Staff
        {
            Username = "clinic-activity-test-user",
            PasswordHash = "test-password-hash",
            Role = StaffRoles.Staff,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var patients = new[]
        {
            CreatePatient("P-1"),
            CreatePatient("P-2"),
            CreatePatient("P-3")
        };

        context.StaffMembers.Add(staff);
        context.Patients.AddRange(patients);
        await context.SaveChangesAsync();

        var pregnancy = new Pregnancy
        {
            PatientId = patients[0].Id,
            Status = "Current",
            CreatedAtUtc = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc)
        };
        context.Pregnancies.Add(pregnancy);
        await context.SaveChangesAsync();

        context.PrenatalVisits.Add(new PrenatalVisit
        {
            PregnancyId = pregnancy.Id,
            VisitDate = OctoberDate,
            CreatedAtUtc = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc)
        });

        context.Appointments.Add(new Appointment
        {
            PatientId = patients[0].Id,
            PregnancyId = pregnancy.Id,
            AppointmentDate = OctoberDate,
            StartTime = new TimeOnly(9, 0),
            Purpose = "Routine appointment",
            Status = "Scheduled",
            CreatedAtUtc = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc)
        });

        context.LaboratoryResults.AddRange(
            CreateResult(patients[0].Id, "Requested", LaboratoryResultStatus.Requested,
                requestedDate: OctoberDate),
            CreateResult(patients[0].Id, "Awaiting", LaboratoryResultStatus.AwaitingResult,
                requestedDate: OctoberDate.AddDays(-1),
                testDate: OctoberDate),
            CreateResult(patients[1].Id, "Resulted", LaboratoryResultStatus.Resulted,
                requestedDate: OctoberDate,
                testDate: NovemberDate),
            CreateResult(patients[2].Id, "Unknown without activity date", status: null,
                requestedDate: NovemberDate),
            new LaboratoryResult
            {
                PatientId = patients[2].Id,
                TestName = "Archived result",
                Status = LaboratoryResultStatus.Resulted,
                RequestedDate = OctoberDate,
                TestDate = NovemberDate,
                IsArchived = true
            },
            new LaboratoryResult
            {
                PatientId = patients[2].Id,
                TestName = "Archived without activity date",
                Status = null,
                RequestedDate = null,
                TestDate = null,
                IsArchived = true
            });

        await context.SaveChangesAsync();

        var sessionService = new SessionService();
        sessionService.StartSession(
            new UserSession(
                staff.Id,
                staff.Username,
                staff.Role,
                staff.SecurityStamp));

        return sessionService;
    }

    private static ClinicActivityReportService CreateReportService(
        TestDatabase database,
        SessionService sessionService)
    {
        var patientRepository = new PatientRepository(database.Factory);
        var pregnancyRepository = new PregnancyRepository(database.Factory);
        var prenatalVisitRepository = new PrenatalVisitRepository(database.Factory);
        var laboratoryResultRepository =
            new LaboratoryResultRepository(database.Factory);
        var appointmentRepository = new AppointmentRepository(database.Factory);
        var staffRepository = new StaffRepository(database.Factory);
        var authorizationService = new AuthorizationService(
            sessionService,
            staffRepository);
        var regionalSettingsService = new RegionalSettingsService(
            new SystemSettingRepository(database.Factory));
        var systemSettingService = new SystemSettingService(
            new SystemSettingRepository(database.Factory),
            authorizationService,
            sessionService);

        var patientService = new PatientService(
            patientRepository,
            authorizationService,
            sessionService,
            regionalSettingsService);
        var pregnancyService = new PregnancyService(
            pregnancyRepository,
            patientRepository,
            authorizationService,
            sessionService,
            regionalSettingsService);
        var prenatalVisitService = new PrenatalVisitService(
            prenatalVisitRepository,
            pregnancyRepository,
            authorizationService,
            sessionService);
        var laboratoryResultService = new LaboratoryResultService(
            laboratoryResultRepository,
            patientRepository,
            prenatalVisitRepository,
            pregnancyRepository,
            authorizationService,
            sessionService);
        var appointmentService = new AppointmentService(
            appointmentRepository,
            patientRepository,
            pregnancyRepository,
            authorizationService,
            sessionService,
            regionalSettingsService,
            systemSettingService);

        return new ClinicActivityReportService(
            patientService,
            pregnancyService,
            prenatalVisitService,
            laboratoryResultService,
            appointmentService,
            regionalSettingsService);
    }

    private static int MonthlyLaboratoryRecords(
        ClinicActivityReport report,
        string month)
    {
        return Assert.Single(
            report.MonthlyActivity,
            item => item.Month == month).LaboratoryResults;
    }

    private static Patient CreatePatient(string patientNumber)
    {
        return new Patient
        {
            PatientNumber = patientNumber,
            FirstName = patientNumber,
            LastName = "Test",
            CreatedAtUtc = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static LaboratoryResult CreateResult(
        int patientId,
        string testName,
        LaboratoryResultStatus? status,
        DateOnly? requestedDate = null,
        DateOnly? testDate = null)
    {
        return new LaboratoryResult
        {
            PatientId = patientId,
            TestName = testName,
            Status = status,
            RequestedDate = requestedDate,
            TestDate = testDate,
            Result = status == LaboratoryResultStatus.Resulted
                ? "Recorded result"
                : null
        };
    }
}
