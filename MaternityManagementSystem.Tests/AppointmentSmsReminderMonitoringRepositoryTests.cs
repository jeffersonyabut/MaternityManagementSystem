using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Tests;

public sealed class AppointmentSmsReminderMonitoringRepositoryTests
{
    private static readonly DateTime Timestamp =
        new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);

    private sealed record SeededReminder(int ReminderId, int PatientId);

    private static async Task<SeededReminder> AddReminderAsync(
        TestDatabase database,
        AppointmentSmsReminderStatus status = AppointmentSmsReminderStatus.Pending,
        AppointmentSmsReminderType type = AppointmentSmsReminderType.Advance3Day,
        DateOnly? appointmentDate = null,
        DateTime? scheduledAtUtc = null,
        DateTime? statusChangedAtUtc = null,
        string firstName = "Ada",
        string lastName = "Lovelace",
        string? patientNumber = null)
    {
        await using var context = await database.Factory.CreateDbContextAsync();
        var patient = new Patient
        {
            PatientNumber = patientNumber ?? $"PAT-{Guid.NewGuid():N}",
            FirstName = firstName,
            LastName = lastName,
            ContactNumber = "+639171234567",
            Address = "Sensitive address",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };
        var appointment = new Appointment
        {
            Patient = patient,
            AppointmentDate = appointmentDate ?? new DateOnly(2026, 12, 10),
            StartTime = new TimeOnly(9, 30),
            Purpose = "Sensitive clinical purpose",
            Notes = "Sensitive clinical notes",
            Status = "Scheduled"
        };
        var reminder = new AppointmentSmsReminder
        {
            Appointment = appointment,
            ScheduleRevision = 1,
            ConsentGeneration = 1,
            ReminderType = type,
            ScheduledAtUtc = scheduledAtUtc ?? Timestamp,
            AppointmentDateSnapshot = appointment.AppointmentDate,
            AppointmentStartTimeSnapshot = appointment.StartTime,
            Status = status,
            CreatedAtUtc = Timestamp.AddDays(-2),
            StatusChangedAtUtc = statusChangedAtUtc ?? Timestamp,
            AttemptStartedAtUtc = status is AppointmentSmsReminderStatus.Sending or
                AppointmentSmsReminderStatus.ProviderAccepted
                ? Timestamp.AddMinutes(-1)
                : null,
            FinalizedAtUtc = status is AppointmentSmsReminderStatus.ProviderAccepted or
                AppointmentSmsReminderStatus.Failed or AppointmentSmsReminderStatus.Unknown or
                AppointmentSmsReminderStatus.Superseded or AppointmentSmsReminderStatus.Skipped
                ? statusChangedAtUtc ?? Timestamp
                : null,
            SanitizedOutcomeCode = status == AppointmentSmsReminderStatus.Failed
                ? "provider_rejected"
                : null,
            ProviderStatus = status == AppointmentSmsReminderStatus.ProviderAccepted
                ? "accepted"
                : null
        };
        context.AppointmentSmsReminders.Add(reminder);
        await context.SaveChangesAsync();
        return new SeededReminder(reminder.Id, patient.Id);
    }

    [Fact]
    public async Task MonitoringQuery_ReturnsEveryReminderStatus()
    {
        await using var database = await TestDatabase.CreateAsync();
        foreach (AppointmentSmsReminderStatus status in
                 Enum.GetValues<AppointmentSmsReminderStatus>())
        {
            await AddReminderAsync(database, status: status);
        }

        var page = await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery());

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(Enum.GetValues<AppointmentSmsReminderStatus>().Order(),
            page.Records.Select(record => record.Status).Order());
    }

    [Fact]
    public async Task MonitoringQuery_FiltersByStatusAndReminderType()
    {
        await using var database = await TestDatabase.CreateAsync();
        await AddReminderAsync(database, AppointmentSmsReminderStatus.Pending,
            AppointmentSmsReminderType.Advance3Day);
        await AddReminderAsync(database, AppointmentSmsReminderStatus.Failed,
            AppointmentSmsReminderType.Final24Hour);
        var repository = new AppointmentSmsReminderRepository(database.Factory);

        var byStatus = await repository.GetMonitoringPageAsync(
            new AppointmentSmsReminderMonitoringQuery
            {
                Status = AppointmentSmsReminderStatus.Failed
            });
        var byType = await repository.GetMonitoringPageAsync(
            new AppointmentSmsReminderMonitoringQuery
            {
                ReminderType = AppointmentSmsReminderType.Final24Hour
            });

        Assert.Single(byStatus.Records);
        Assert.Equal(AppointmentSmsReminderStatus.Failed, byStatus.Records[0].Status);
        Assert.Single(byType.Records);
        Assert.Equal(AppointmentSmsReminderType.Final24Hour, byType.Records[0].ReminderType);
    }

    [Fact]
    public async Task MonitoringQuery_FiltersByInclusiveAppointmentDateRange()
    {
        await using var database = await TestDatabase.CreateAsync();
        await AddReminderAsync(database, appointmentDate: new DateOnly(2026, 12, 1));
        await AddReminderAsync(database, appointmentDate: new DateOnly(2026, 12, 2));
        await AddReminderAsync(database, appointmentDate: new DateOnly(2026, 12, 3));

        var page = await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery
            {
                AppointmentDateFrom = new DateOnly(2026, 12, 2),
                AppointmentDateTo = new DateOnly(2026, 12, 2)
            });

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(new DateOnly(2026, 12, 2), page.Records[0].AppointmentDate);
    }

    [Theory]
    [InlineData("lovelace", "Ada")]
    [InlineData("ada lovelace", "Ada")]
    [InlineData("pat-002", "Grace")]
    public async Task MonitoringQuery_SearchesPatientNamesAndNumbers(
        string searchText,
        string expectedFirstName)
    {
        await using var database = await TestDatabase.CreateAsync();
        await AddReminderAsync(database, firstName: "Ada", lastName: "Lovelace");
        await AddReminderAsync(database, firstName: "Grace", lastName: "Hopper",
            patientNumber: "PAT-002");

        var page = await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery
            {
                SearchText = searchText
            });

        Assert.Single(page.Records);
        Assert.Equal(expectedFirstName, page.Records[0].PatientFirstName);
    }

    [Fact]
    public async Task MonitoringQuery_ReturnsTotalCountAndPaginatesDeterministically()
    {
        await using var database = await TestDatabase.CreateAsync();
        SeededReminder first = await AddReminderAsync(database, scheduledAtUtc: Timestamp);
        SeededReminder second = await AddReminderAsync(database, scheduledAtUtc: Timestamp);
        await AddReminderAsync(database, scheduledAtUtc: Timestamp.AddMinutes(1));

        var page = await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery
            {
                PageNumber = 1,
                PageSize = 2
            });

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Records.Count);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(new[] { second.ReminderId, first.ReminderId },
            page.Records.Select(record => record.ReminderId));
    }

    [Fact]
    public async Task StatusCounts_IncludeAllStatusesAndZeroCounts()
    {
        await using var database = await TestDatabase.CreateAsync();
        await AddReminderAsync(database, AppointmentSmsReminderStatus.Pending);
        await AddReminderAsync(database, AppointmentSmsReminderStatus.Pending);
        await AddReminderAsync(database, AppointmentSmsReminderStatus.Unknown);

        var counts = await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringStatusCountsAsync();
        var countMap = counts.ToDictionary(item => item.Status, item => item.Count);

        Assert.Equal(7, counts.Count);
        Assert.Equal(2, countMap[AppointmentSmsReminderStatus.Pending]);
        Assert.Equal(1, countMap[AppointmentSmsReminderStatus.Unknown]);
        Assert.Equal(0, countMap[AppointmentSmsReminderStatus.ProviderAccepted]);
    }

    [Fact]
    public async Task MonitoringProjection_ContainsOnlyApprovedPatientAndReminderFields()
    {
        await using var database = await TestDatabase.CreateAsync();
        await AddReminderAsync(database, patientNumber: "PAT-001");

        var record = Assert.Single((await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery())).Records);

        Assert.Equal("Ada", record.PatientFirstName);
        Assert.Equal("Lovelace", record.PatientLastName);
        Assert.Equal("PAT-001", record.PatientNumber);
        string[] propertyNames = typeof(AppointmentSmsReminderMonitoringRecord)
            .GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("ContactNumber", propertyNames);
        Assert.DoesNotContain("Address", propertyNames);
        Assert.DoesNotContain("Purpose", propertyNames);
        Assert.DoesNotContain("Notes", propertyNames);
        Assert.DoesNotContain("MessageContent", propertyNames);
        Assert.DoesNotContain("ProviderReference", propertyNames);
        Assert.DoesNotContain("Appointment", propertyNames);
        Assert.DoesNotContain("Patient", propertyNames);
    }

    [Fact]
    public async Task MonitoringProjection_RestoresUtcKindWithoutChangingValuesOrSnapshots()
    {
        await using var database = await TestDatabase.CreateAsync();
        DateTime scheduledAtUtc = Timestamp.AddHours(6);
        DateTime statusChangedAtUtc = Timestamp.AddMinutes(5);
        DateTime attemptStartedAtUtc = Timestamp.AddMinutes(-1);
        await AddReminderAsync(database,
            status: AppointmentSmsReminderStatus.ProviderAccepted,
            appointmentDate: new DateOnly(2026, 12, 15),
            scheduledAtUtc: scheduledAtUtc,
            statusChangedAtUtc: statusChangedAtUtc);

        var record = Assert.Single((await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery())).Records);

        AssertUtcWithSameTicks(scheduledAtUtc, record.ScheduledAtUtc);
        AssertUtcWithSameTicks(Timestamp.AddDays(-2), record.CreatedAtUtc);
        AssertUtcWithSameTicks(statusChangedAtUtc, record.StatusChangedAtUtc);
        AssertUtcWithSameTicks(attemptStartedAtUtc, record.AttemptStartedAtUtc!.Value);
        AssertUtcWithSameTicks(statusChangedAtUtc, record.FinalizedAtUtc!.Value);
        Assert.Equal(new DateOnly(2026, 12, 15), record.AppointmentDate);
        Assert.Equal(new TimeOnly(9, 30), record.AppointmentStartTime);
    }

    [Fact]
    public async Task MonitoringProjection_PreservesNullLifecycleTimestamps()
    {
        await using var database = await TestDatabase.CreateAsync();
        await AddReminderAsync(database, status: AppointmentSmsReminderStatus.Pending);

        var record = Assert.Single((await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery())).Records);

        Assert.Equal(DateTimeKind.Utc, record.ScheduledAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, record.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, record.StatusChangedAtUtc.Kind);
        Assert.Null(record.AttemptStartedAtUtc);
        Assert.Null(record.FinalizedAtUtc);
    }

    private static void AssertUtcWithSameTicks(DateTime expectedUtc, DateTime actualUtc)
    {
        Assert.Equal(DateTimeKind.Utc, actualUtc.Kind);
        Assert.Equal(expectedUtc.Ticks, actualUtc.Ticks);
    }

    [Fact]
    public async Task MonitoringQuery_HistoricalRowsSortNewestStatusChangeFirstThenId()
    {
        await using var database = await TestDatabase.CreateAsync();
        SeededReminder older = await AddReminderAsync(database,
            status: AppointmentSmsReminderStatus.Failed,
            statusChangedAtUtc: Timestamp);
        SeededReminder newerTie = await AddReminderAsync(database,
            status: AppointmentSmsReminderStatus.Unknown,
            statusChangedAtUtc: Timestamp);
        SeededReminder newest = await AddReminderAsync(database,
            status: AppointmentSmsReminderStatus.Skipped,
            statusChangedAtUtc: Timestamp.AddMinutes(1));

        var page = await new AppointmentSmsReminderRepository(database.Factory)
            .GetMonitoringPageAsync(new AppointmentSmsReminderMonitoringQuery());

        Assert.Equal(new[] { newest.ReminderId, newerTie.ReminderId, older.ReminderId },
            page.Records.Select(record => record.ReminderId));
    }
}
