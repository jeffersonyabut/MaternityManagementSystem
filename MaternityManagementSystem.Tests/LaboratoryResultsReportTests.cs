using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;
using MaternityManagementSystem.ViewModels.Reports;

namespace MaternityManagementSystem.Tests;

public class LaboratoryResultsReportTests
{
    private static readonly DateOnly ReportDate = new(2026, 10, 12);

    [Fact]
    public async Task GetForReportAsync_UsesTheLifecycleDate_AndKeepsUnknownOnTestDate()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new LaboratoryResultRepository(database.Factory);

        await SeedAsync(
            database,
            new LaboratoryResult
            {
                TestName = "Requested in range",
                Status = LaboratoryResultStatus.Requested,
                RequestedDate = ReportDate,
                TestDate = null
            },
            new LaboratoryResult
            {
                TestName = "Requested out of range",
                Status = LaboratoryResultStatus.Requested,
                RequestedDate = ReportDate.AddDays(1),
                TestDate = null
            },
            new LaboratoryResult
            {
                TestName = "Awaiting in range",
                Status = LaboratoryResultStatus.AwaitingResult,
                RequestedDate = ReportDate.AddDays(-1),
                TestDate = ReportDate
            },
            new LaboratoryResult
            {
                TestName = "Awaiting out of range",
                Status = LaboratoryResultStatus.AwaitingResult,
                RequestedDate = ReportDate,
                TestDate = ReportDate.AddDays(1)
            },
            new LaboratoryResult
            {
                TestName = "Resulted in range",
                Status = LaboratoryResultStatus.Resulted,
                RequestedDate = ReportDate.AddDays(-2),
                TestDate = ReportDate,
                Result = "Recorded"
            },
            new LaboratoryResult
            {
                TestName = "Unknown in range",
                Status = null,
                RequestedDate = ReportDate,
                TestDate = ReportDate,
                Result = "Legacy value"
            },
            new LaboratoryResult
            {
                TestName = "Unknown out of range",
                Status = null,
                RequestedDate = ReportDate,
                TestDate = ReportDate.AddDays(-1)
            });

        var results = await repository.GetForReportAsync(
            ReportDate,
            ReportDate,
            null,
            null);

        Assert.Equal(
            new[]
            {
                "Awaiting in range",
                "Requested in range",
                "Resulted in range",
                "Unknown in range"
            }.OrderBy(name => name),
            results.Select(result => result.TestName).OrderBy(name => name));
    }

    [Fact]
    public async Task GetForReportAsync_DoesNotMatchRowsWithNullApplicableDate()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new LaboratoryResultRepository(database.Factory);

        await SeedAsync(
            database,
            new LaboratoryResult
            {
                TestName = "Requested without request date",
                Status = LaboratoryResultStatus.Requested,
                RequestedDate = null,
                TestDate = ReportDate
            },
            new LaboratoryResult
            {
                TestName = "Awaiting without test date",
                Status = LaboratoryResultStatus.AwaitingResult,
                RequestedDate = ReportDate,
                TestDate = null
            },
            new LaboratoryResult
            {
                TestName = "Resulted without test date",
                Status = LaboratoryResultStatus.Resulted,
                RequestedDate = ReportDate,
                TestDate = null
            },
            new LaboratoryResult
            {
                TestName = "Unknown without test date",
                Status = null,
                RequestedDate = ReportDate,
                TestDate = null
            });

        var results = await repository.GetForReportAsync(
            ReportDate,
            ReportDate,
            null,
            null);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetForReportAsync_AppliesPatientAndTestFilters_AndExcludesArchivedRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        int firstPatientId;
        int secondPatientId;

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            var firstPatient = CreatePatient("P-100", "Ada", "Lovelace");
            var secondPatient = CreatePatient("P-200", "Grace", "Hopper");
            context.Patients.AddRange(firstPatient, secondPatient);
            await context.SaveChangesAsync();
            firstPatientId = firstPatient.Id;
            secondPatientId = secondPatient.Id;

            context.LaboratoryResults.AddRange(
                CreateResult(firstPatientId, "CBC panel", LaboratoryResultStatus.Requested),
                CreateResult(firstPatientId, "Urine panel", LaboratoryResultStatus.Resulted),
                CreateResult(secondPatientId, "CBC panel", LaboratoryResultStatus.Resulted),
                new LaboratoryResult
                {
                    PatientId = firstPatientId,
                    TestName = "CBC archived",
                    Status = LaboratoryResultStatus.Resulted,
                    RequestedDate = ReportDate,
                    TestDate = ReportDate,
                    IsArchived = true
                });
            await context.SaveChangesAsync();
        }

        var repository = new LaboratoryResultRepository(database.Factory);
        var results = await repository.GetForReportAsync(
            ReportDate,
            ReportDate,
            "Ada",
            "CBC",
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(firstPatientId, result.PatientId);
        Assert.Equal("CBC panel", result.TestName);
        Assert.Equal("Ada", result.Patient.FirstName);
    }

    [Fact]
    public async Task ReportAndViewModel_CountReturnedStatuses_IncludingEmptyReports()
    {
        var report = new LaboratoryResultsReport
        {
            LaboratoryResults =
            [
                CreateResult(1, "Requested", LaboratoryResultStatus.Requested),
                CreateResult(1, "Awaiting", LaboratoryResultStatus.AwaitingResult),
                CreateResult(2, "Resulted", LaboratoryResultStatus.Resulted),
                CreateResult(3, "Legacy A", null),
                CreateResult(4, "Legacy B", null)
            ]
        };
        var viewModel = CreateViewModel();
        viewModel.Report = report;

        Assert.Equal(5, report.TotalLaboratoryRecords);
        Assert.Equal(1, report.RequestedCount);
        Assert.Equal(1, report.AwaitingResultCount);
        Assert.Equal(1, report.ResultedCount);
        Assert.Equal(2, report.UnknownUnclassifiedCount);
        Assert.Equal(5, viewModel.TotalLaboratoryRecords);
        Assert.Equal(1, viewModel.RequestedCount);
        Assert.Equal(1, viewModel.AwaitingResultCount);
        Assert.Equal(1, viewModel.ResultedCount);
        Assert.Equal(2, viewModel.UnknownUnclassifiedCount);

        viewModel.Report = new LaboratoryResultsReport();

        Assert.Equal(0, viewModel.TotalLaboratoryRecords);
        Assert.Equal(0, viewModel.RequestedCount);
        Assert.Equal(0, viewModel.AwaitingResultCount);
        Assert.Equal(0, viewModel.ResultedCount);
        Assert.Equal(0, viewModel.UnknownUnclassifiedCount);
        Assert.True(viewModel.HasNoLaboratoryResults);
    }

    [Fact]
    public async Task GetForReportAsync_PreservesLegacyStoredResultAndUnknownStatus()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new LaboratoryResultRepository(database.Factory);
        await SeedAsync(
            database,
            new LaboratoryResult
            {
                TestName = "Historical panel",
                Status = null,
                RequestedDate = null,
                TestDate = ReportDate,
                Result = "Positive"
            });

        var result = Assert.Single(
            await repository.GetForReportAsync(
                ReportDate,
                ReportDate,
                null,
                null));

        Assert.Null(result.Status);
        Assert.Equal("Positive", result.Result);
        Assert.Equal(
            "Unknown / Unclassified",
            LaboratoryResultsReportViewModel.GetStatusDisplay(result.Status));
        Assert.Equal(
            "Positive",
            LaboratoryResultsReportViewModel.FormatResultValue(result.Result));
    }

    [Fact]
    public void RecordDateFormatting_UsesAnExplicitMissingValue()
    {
        var regionalSettings = new RegionalSettingsService(
            new SystemSettingRepository());

        Assert.Equal(
            "—",
            LaboratoryResultsReportViewModel.FormatRecordDate(
                null,
                regionalSettings));
        Assert.Equal(
            "Unknown / Unclassified",
            LaboratoryResultsReportViewModel.GetStatusDisplay(null));
    }

    private static LaboratoryResultsReportViewModel CreateViewModel()
    {
        var sessionService = new SessionService();
        var laboratoryResultService = new LaboratoryResultService(
            new LaboratoryResultRepository(),
            new PatientRepository(),
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            new AuthorizationService(sessionService),
            sessionService);

        return new LaboratoryResultsReportViewModel(
            new LaboratoryResultsReportService(laboratoryResultService));
    }

    private static async Task SeedAsync(
        TestDatabase database,
        params LaboratoryResult[] results)
    {
        await using var context = await database.Factory.CreateDbContextAsync();
        var patient = CreatePatient("P-300", "Test", "Patient");
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        foreach (var result in results)
        {
            result.PatientId = patient.Id;
        }

        context.LaboratoryResults.AddRange(results);
        await context.SaveChangesAsync();
    }

    private static Patient CreatePatient(
        string patientNumber,
        string firstName,
        string lastName)
    {
        return new Patient
        {
            PatientNumber = patientNumber,
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static LaboratoryResult CreateResult(
        int patientId,
        string testName,
        LaboratoryResultStatus? status)
    {
        return new LaboratoryResult
        {
            PatientId = patientId,
            TestName = testName,
            Status = status,
            RequestedDate = ReportDate,
            TestDate = status == LaboratoryResultStatus.Requested
                ? null
                : ReportDate,
            Result = status == LaboratoryResultStatus.Resulted
                ? "Recorded"
                : null
        };
    }
}
