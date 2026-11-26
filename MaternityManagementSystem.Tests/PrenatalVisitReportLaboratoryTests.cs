using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace MaternityManagementSystem.Tests;

public class PrenatalVisitReportLaboratoryTests
{
    [Fact]
    public void GetLaboratoryStatusDisplay_FormatsLifecycleStatesAndLegacyStatus()
    {
        Assert.Equal(
            "Requested",
            PrenatalVisitReportViewModel.GetLaboratoryStatusDisplay(
                LaboratoryResultStatus.Requested));
        Assert.Equal(
            "AwaitingResult",
            PrenatalVisitReportViewModel.GetLaboratoryStatusDisplay(
                LaboratoryResultStatus.AwaitingResult));
        Assert.Equal(
            "Resulted",
            PrenatalVisitReportViewModel.GetLaboratoryStatusDisplay(
                LaboratoryResultStatus.Resulted));
        Assert.Equal(
            "Unknown / Unclassified",
            PrenatalVisitReportViewModel.GetLaboratoryStatusDisplay(null));
    }

    [Fact]
    public void GetLaboratoryResultDisplay_ShowsMissingAndAwaitingResultsWithoutInference()
    {
        Assert.Equal(
            "—",
            PrenatalVisitReportViewModel.GetLaboratoryResultDisplay(
                LaboratoryResultStatus.Requested,
                null));
        Assert.Equal(
            "Result not yet available",
            PrenatalVisitReportViewModel.GetLaboratoryResultDisplay(
                LaboratoryResultStatus.AwaitingResult,
                string.Empty));
        Assert.Equal(
            "Actual value",
            PrenatalVisitReportViewModel.GetLaboratoryResultDisplay(
                LaboratoryResultStatus.Resulted,
                "Actual value"));
    }

    [Fact]
    public void GetLaboratoryResultDisplay_PreservesStoredLegacyResult()
    {
        Assert.Equal(
            "Legacy value",
            PrenatalVisitReportViewModel.GetLaboratoryResultDisplay(
                null,
                "Legacy value"));
    }

    [Fact]
    public void GetLaboratoryValueDisplay_UsesUnavailableMarkerForMissingValues()
    {
        Assert.Equal(
            "—",
            PrenatalVisitReportViewModel.GetLaboratoryValueDisplay(null));
        Assert.Equal(
            "—",
            PrenatalVisitReportViewModel.GetLaboratoryValueDisplay("  "));
        Assert.Equal(
            "Stored note",
            PrenatalVisitReportViewModel.GetLaboratoryValueDisplay("Stored note"));
    }

    [Fact]
    public void GetLaboratoryDateDisplay_UsesUnavailableMarkerWithoutInferringDates()
    {
        var regionalSettings = new RegionalSettingsService(
            new SystemSettingRepository());
        var date = new DateOnly(2026, 5, 4);

        Assert.Equal(
            "—",
            PrenatalVisitReportViewModel.GetLaboratoryDateDisplay(
                null,
                regionalSettings));
        Assert.Equal(
            regionalSettings.FormatDate(date),
            PrenatalVisitReportViewModel.GetLaboratoryDateDisplay(
                date,
                regionalSettings));
    }

    [Fact]
    public void PrenatalVisitReport_TotalLaboratoryRecordsCountsAllLinkedRecords()
    {
        var report = new PrenatalVisitReport
        {
            LaboratoryResults =
            [
                new LaboratoryResult
                {
                    Status = LaboratoryResultStatus.Requested,
                    RequestedDate = new DateOnly(2026, 5, 1),
                    TestDate = null,
                    Result = null
                },
                new LaboratoryResult
                {
                    Status = LaboratoryResultStatus.AwaitingResult,
                    RequestedDate = new DateOnly(2026, 5, 2),
                    TestDate = new DateOnly(2026, 5, 3),
                    Result = null
                },
                new LaboratoryResult
                {
                    Status = LaboratoryResultStatus.Resulted,
                    TestDate = new DateOnly(2026, 5, 4),
                    Result = "Result"
                },
                new LaboratoryResult
                {
                    Status = null,
                    RequestedDate = null,
                    TestDate = null,
                    Result = "Legacy result"
                }
            ]
        };

        Assert.Equal(4, report.TotalLaboratoryResults);
    }

    [Fact]
    public async Task GetByPrenatalVisitIdAsync_ReturnsOnlyLinkedNonArchivedRecords()
    {
        await using var database = await TestDatabase.CreateAsync();
        int visitId;

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            var patient = new Patient
            {
                PatientNumber = "PV-REPORT-001",
                FirstName = "Test",
                LastName = "Patient"
            };
            var pregnancy = new Pregnancy { Patient = patient };
            var visit = new PrenatalVisit { Pregnancy = pregnancy };
            var linkedRecord = new LaboratoryResult
            {
                Patient = patient,
                PrenatalVisit = visit,
                Status = LaboratoryResultStatus.Requested,
                RequestedDate = new DateOnly(2026, 5, 1),
                TestDate = null,
                Result = null
            };
            var archivedRecord = new LaboratoryResult
            {
                Patient = patient,
                PrenatalVisit = visit,
                Status = null,
                IsArchived = true,
                Result = "Archived"
            };
            var otherVisitRecord = new LaboratoryResult
            {
                Patient = patient,
                Status = LaboratoryResultStatus.Resulted,
                Result = "Other visit"
            };

            context.LaboratoryResults.AddRange(
                linkedRecord,
                archivedRecord,
                otherVisitRecord);
            await context.SaveChangesAsync();
            visitId = visit.Id;
        }

        var repository = new LaboratoryResultRepository(database.Factory);
        var records = await repository.GetByPrenatalVisitIdAsync(visitId);

        Assert.Single(records);
        Assert.Equal(LaboratoryResultStatus.Requested, records[0].Status);
        Assert.Null(records[0].TestDate);
        Assert.Null(records[0].Result);
    }
}
