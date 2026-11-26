using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Tests;

public class LaboratoryResultWorkflowTests
{
    [Fact]
    public async Task RegistrationViewModel_WhenStatusChanges_UpdatesFieldAvailabilityWithoutClearingValues()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var service = await CreateServiceAsync(testDatabase);
        var viewModel = new LaboratoryResultRegistrationViewModel(
            service,
            new PregnancyRepository(testDatabase.Factory),
            new PrenatalVisitRepository(testDatabase.Factory),
            new Patient { Id = 1 });

        Assert.Equal(LaboratoryResultStatus.Requested, viewModel.Status);
        Assert.True(viewModel.IsRequestedDateRequired);
        Assert.False(viewModel.IsTestDateEnabled);
        Assert.False(viewModel.IsResultEnabled);

        var performedDate = DateTime.Today.AddDays(-1);
        viewModel.TestDate = performedDate;
        viewModel.Result = "Existing entry";

        viewModel.Status = LaboratoryResultStatus.AwaitingResult;

        Assert.True(viewModel.IsTestDateEnabled);
        Assert.False(viewModel.IsResultEnabled);
        Assert.True(viewModel.IsRequestedDateRequired);
        Assert.Equal(performedDate, viewModel.TestDate);
        Assert.Equal("Existing entry", viewModel.Result);

        viewModel.Status = LaboratoryResultStatus.Resulted;

        Assert.True(viewModel.IsTestDateEnabled);
        Assert.True(viewModel.IsResultEnabled);
        Assert.False(viewModel.IsRequestedDateRequired);
        Assert.Contains("Optional", viewModel.RequestedDateLabel);
        Assert.Equal(performedDate, viewModel.TestDate);
        Assert.Equal("Existing entry", viewModel.Result);
    }

    [Fact]
    public async Task RegistrationViewModel_SaveRequested_AllowsNullTestDateAndResult()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var (patientId, _) = await CreatePatientAndVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);
        var viewModel = new LaboratoryResultRegistrationViewModel(
            service,
            new PregnancyRepository(testDatabase.Factory),
            new PrenatalVisitRepository(testDatabase.Factory),
            new Patient { Id = patientId });
        viewModel.TestName = "CBC";
        viewModel.TestDate = null;
        viewModel.Result = string.Empty;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(
            "Laboratory result saved successfully.",
            viewModel.StatusMessage);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var saved = await verificationContext.LaboratoryResults
            .SingleAsync(result => result.PatientId == patientId);

        Assert.Equal(LaboratoryResultStatus.Requested, saved.Status);
        Assert.NotNull(saved.RequestedDate);
        Assert.Null(saved.TestDate);
        Assert.Null(saved.Result);
    }

    [Fact]
    public async Task EditViewModel_WhenLegacyDatesAreNull_PreservesNullsUntilExplicitStatusSelection()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var (patientId, visitId) = await CreatePatientAndVisitAsync(testDatabase);
        int laboratoryResultId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var legacyResult = new LaboratoryResult
            {
                PatientId = patientId,
                PrenatalVisitId = visitId,
                TestName = "Legacy test",
                Status = null,
                RequestedDate = null,
                TestDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
                Result = "Legacy result"
            };

            dbContext.LaboratoryResults.Add(legacyResult);
            await dbContext.SaveChangesAsync();
            laboratoryResultId = legacyResult.Id;
        }

        var service = await CreateServiceAsync(testDatabase);
        var viewModel = new LaboratoryResultEditViewModel(service);
        await viewModel.LoadLaboratoryResultAsync(laboratoryResultId);

        Assert.Null(viewModel.Status);
        Assert.Null(viewModel.RequestedDate);
        Assert.Contains("no status", viewModel.StatusHelpText);
        Assert.Contains("before selecting Requested", viewModel.RequestedDateHelpText);

        viewModel.Status = LaboratoryResultStatus.Requested;

        Assert.True(viewModel.IsRequestedDateRequired);
        Assert.False(viewModel.IsTestDateEnabled);
        Assert.False(viewModel.IsResultEnabled);
        Assert.Null(viewModel.RequestedDate);
        Assert.Contains("before saving", viewModel.RequestedDateHelpText);
        Assert.Equal(DateTime.Today.AddDays(-1), viewModel.TestDate);
        Assert.Equal("Legacy result", viewModel.Result);

        viewModel.Status = LaboratoryResultStatus.AwaitingResult;

        Assert.True(viewModel.IsTestDateEnabled);
        Assert.False(viewModel.IsResultEnabled);
        Assert.True(viewModel.IsRequestedDateRequired);
        Assert.Null(viewModel.RequestedDate);
        Assert.Equal("Legacy result", viewModel.Result);

        viewModel.Status = LaboratoryResultStatus.Resulted;

        Assert.True(viewModel.IsTestDateEnabled);
        Assert.True(viewModel.IsResultEnabled);
        Assert.False(viewModel.IsRequestedDateRequired);
        Assert.Contains("Optional", viewModel.RequestedDateLabel);
        Assert.Null(viewModel.RequestedDate);
    }

    [Theory]
    [InlineData(LaboratoryResultStatus.Requested)]
    [InlineData(LaboratoryResultStatus.AwaitingResult)]
    [InlineData(LaboratoryResultStatus.Resulted)]
    public void Validate_WhenLifecycleFieldsAreConsistent_AcceptsRecord(
        LaboratoryResultStatus status)
    {
        var laboratoryResult = CreateValidResult(status);

        LaboratoryResultValidator.Validate(laboratoryResult);
    }

    [Theory]
    [InlineData(LaboratoryResultStatus.Requested, true, false, false)]
    [InlineData(LaboratoryResultStatus.Requested, false, true, false)]
    [InlineData(LaboratoryResultStatus.AwaitingResult, false, false, false)]
    [InlineData(LaboratoryResultStatus.AwaitingResult, true, false, true)]
    [InlineData(LaboratoryResultStatus.Resulted, true, false, false)]
    [InlineData(LaboratoryResultStatus.Resulted, false, true, false)]
    public void Validate_WhenLifecycleFieldsConflict_ThrowsArgumentException(
        LaboratoryResultStatus status,
        bool hasTestDate,
        bool hasResult,
        bool requestedDateIsNull)
    {
        var laboratoryResult = CreateValidResult(status);
        laboratoryResult.TestDate = hasTestDate
            ? DateOnly.FromDateTime(DateTime.Today)
            : null;
        laboratoryResult.Result = hasResult
            ? "Hemoglobin 12.1"
            : null;

        if (requestedDateIsNull)
        {
            laboratoryResult.RequestedDate = null;
        }

        Assert.Throws<ArgumentException>(
            () => LaboratoryResultValidator.Validate(laboratoryResult));
    }

    [Fact]
    public async Task CreateLaboratoryResultAsync_WhenRequested_UsesRequestedDateAndNoPerformedData()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var (patientId, visitId) = await CreatePatientAndVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);

        var result = await service.CreateLaboratoryResultAsync(
            new LaboratoryResult
            {
                PatientId = patientId,
                PrenatalVisitId = visitId,
                TestName = "CBC",
                Status = LaboratoryResultStatus.Requested,
                RequestedDate = DateOnly.FromDateTime(DateTime.Today),
                TestDate = null,
                Result = null
            });

        Assert.Equal(LaboratoryResultStatus.Requested, result.Status);
        Assert.NotNull(result.RequestedDate);
        Assert.Null(result.TestDate);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task CreateLaboratoryResultAsync_WhenRequestedDateIsMissing_RejectsInsteadOfSubstitutingToday()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var (patientId, visitId) = await CreatePatientAndVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);
        var laboratoryResult = CreateValidResult(
            LaboratoryResultStatus.Requested);
        laboratoryResult.PatientId = patientId;
        laboratoryResult.PrenatalVisitId = visitId;
        laboratoryResult.RequestedDate = null;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateLaboratoryResultAsync(laboratoryResult));

        Assert.Contains("requested date", exception.Message);
    }

    [Fact]
    public async Task CreateLaboratoryResultAsync_WhenPatientDoesNotExist_ThrowsArgumentException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var service = await CreateServiceAsync(testDatabase);
        var laboratoryResult = CreateValidResult(
            LaboratoryResultStatus.Requested);
        laboratoryResult.PatientId = 999999;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateLaboratoryResultAsync(
                laboratoryResult));

        Assert.Contains("could not be found", exception.Message);
    }

    [Fact]
    public async Task CreateLaboratoryResultAsync_WhenVisitBelongsToAnotherPatient_ThrowsInvalidOperationException()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var (firstPatientId, _) = await CreatePatientAndVisitAsync(testDatabase);
        var (_, otherVisitId) = await CreatePatientAndVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);
        var laboratoryResult = CreateValidResult(
            LaboratoryResultStatus.Requested);
        laboratoryResult.PatientId = firstPatientId;
        laboratoryResult.PrenatalVisitId = otherVisitId;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateLaboratoryResultAsync(laboratoryResult));

        Assert.Contains("does not belong", exception.Message);
    }

    [Fact]
    public async Task UpdateLaboratoryResultAsync_WhenLifecycleChanges_SavesAuditAndVersionHistory()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var (patientId, visitId) = await CreatePatientAndVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);

        var requested = CreateValidResult(
            LaboratoryResultStatus.Requested);
        requested.PatientId = patientId;
        requested.PrenatalVisitId = visitId;
        var created = await service.CreateLaboratoryResultAsync(requested);

        var awaiting = CreateValidResult(
            LaboratoryResultStatus.AwaitingResult);
        awaiting.Id = created.Id;
        awaiting.PatientId = patientId;
        awaiting.PrenatalVisitId = visitId;

        await service.UpdateLaboratoryResultAsync(awaiting);

        var resulted = CreateValidResult(
            LaboratoryResultStatus.Resulted);
        resulted.Id = created.Id;
        resulted.PatientId = patientId;
        resulted.PrenatalVisitId = visitId;
        resulted.Result = "Hemoglobin 12.1";

        await service.UpdateLaboratoryResultAsync(resulted);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var saved = await verificationContext.LaboratoryResults
            .SingleAsync(item => item.Id == created.Id);

        Assert.Equal(LaboratoryResultStatus.Resulted, saved.Status);
        Assert.Equal(resulted.RequestedDate, saved.RequestedDate);
        Assert.Equal(resulted.TestDate, saved.TestDate);
        Assert.Equal("Hemoglobin 12.1", saved.Result);

        var auditLogs = await verificationContext.AuditLogs
            .Where(log => log.EntityType == "LaboratoryResult" &&
                          log.EntityId == created.Id &&
                          log.Action == "Updated")
            .OrderBy(log => log.Id)
            .ToListAsync();

        Assert.Equal(2, auditLogs.Count);
        Assert.All(auditLogs, log => Assert.Equal("lab-test-user", log.Username));

        var versions = await verificationContext.RecordVersions
            .Where(version => version.EntityType == "LaboratoryResult" &&
                              version.EntityId == created.Id)
            .OrderBy(version => version.Id)
            .ToListAsync();

        Assert.Equal(2, versions.Count);
        Assert.Equal("lab-test-user", versions[0].Username);

        var firstOldValues = JsonSerializer.Deserialize<JsonElement>(
            versions[0].PreviousValuesJson);
        var firstNewValues = JsonSerializer.Deserialize<JsonElement>(
            versions[0].NewValuesJson);
        Assert.Equal(
            LaboratoryResultStatus.Requested.ToString(),
            firstOldValues.GetProperty("Status").GetString());
        Assert.Equal(
            LaboratoryResultStatus.AwaitingResult.ToString(),
            firstNewValues.GetProperty("Status").GetString());

        var secondOldValues = JsonSerializer.Deserialize<JsonElement>(
            versions[1].PreviousValuesJson);
        var secondNewValues = JsonSerializer.Deserialize<JsonElement>(
            versions[1].NewValuesJson);
        Assert.Equal(
            LaboratoryResultStatus.AwaitingResult.ToString(),
            secondOldValues.GetProperty("Status").GetString());
        Assert.Equal(
            LaboratoryResultStatus.Resulted.ToString(),
            secondNewValues.GetProperty("Status").GetString());
    }

    [Fact]
    public async Task UpdateLaboratoryResultAsync_WhenStatusMovesBackward_RejectsWithoutChangingRecordedData()
    {
        await using var testDatabase = await TestDatabase.CreateAsync();
        var (patientId, visitId) = await CreatePatientAndVisitAsync(testDatabase);
        var service = await CreateServiceAsync(testDatabase);

        var resulted = CreateValidResult(LaboratoryResultStatus.Resulted);
        resulted.PatientId = patientId;
        resulted.PrenatalVisitId = visitId;
        var created = await service.CreateLaboratoryResultAsync(resulted);

        var update = CreateValidResult(
            LaboratoryResultStatus.AwaitingResult);
        update.Id = created.Id;
        update.PatientId = patientId;
        update.PrenatalVisitId = visitId;
        update.Result = created.Result;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateLaboratoryResultAsync(update));

        Assert.Contains("cannot move backward", exception.Message);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var unchanged = await verificationContext.LaboratoryResults
            .SingleAsync(item => item.Id == created.Id);

        Assert.Equal(LaboratoryResultStatus.Resulted, unchanged.Status);
        Assert.Equal(created.TestDate, unchanged.TestDate);
        Assert.Equal(created.Result, unchanged.Result);
        Assert.Equal(0, await verificationContext.AuditLogs.CountAsync(log =>
            log.EntityType == "LaboratoryResult" &&
            log.EntityId == created.Id &&
            log.Action == "Updated"));
        Assert.Equal(0, await verificationContext.RecordVersions.CountAsync(
            version => version.EntityType == "LaboratoryResult" &&
                       version.EntityId == created.Id));
    }

    private static LaboratoryResult CreateValidResult(
        LaboratoryResultStatus status)
    {
        bool hasPerformedDate =
            status != LaboratoryResultStatus.Requested;
        bool hasResult = status == LaboratoryResultStatus.Resulted;

        return new LaboratoryResult
        {
            PatientId = 1,
            TestName = "CBC",
            Status = status,
            RequestedDate = DateOnly.FromDateTime(DateTime.Today),
            TestDate = hasPerformedDate
                ? DateOnly.FromDateTime(DateTime.Today)
                : null,
            Result = hasResult ? "Hemoglobin 12.1" : null
        };
    }

    private static async Task<(int PatientId, int VisitId)>
        CreatePatientAndVisitAsync(TestDatabase testDatabase)
    {
        await using var dbContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var patient = new Patient
        {
            PatientNumber = $"TEST-LAB-{Guid.NewGuid():N}",
            FirstName = "Jane",
            LastName = "Doe",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync();

        var pregnancy = new Pregnancy
        {
            PatientId = patient.Id,
            Status = "Current",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Pregnancies.Add(pregnancy);
        await dbContext.SaveChangesAsync();

        var visit = new PrenatalVisit
        {
            PregnancyId = pregnancy.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.PrenatalVisits.Add(visit);
        await dbContext.SaveChangesAsync();

        return (patient.Id, visit.Id);
    }

    private static async Task<LaboratoryResultService> CreateServiceAsync(
        TestDatabase testDatabase)
    {
        int staffId;
        string securityStamp;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = "lab-test-user",
                PasswordHash = "test-password-hash",
                Role = StaffRoles.Staff,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.StaffMembers.Add(staff);
            await dbContext.SaveChangesAsync();
            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var sessionService = new SessionService();
        sessionService.StartSession(
            new UserSession(
                staffId,
                "lab-test-user",
                StaffRoles.Staff,
                securityStamp));

        var authorizationService = new AuthorizationService(
            sessionService,
            new StaffRepository(testDatabase.Factory));

        return new LaboratoryResultService(
            new LaboratoryResultRepository(testDatabase.Factory),
            new PatientRepository(testDatabase.Factory),
            new PrenatalVisitRepository(testDatabase.Factory),
            new PregnancyRepository(testDatabase.Factory),
            authorizationService,
            sessionService);
    }
}
