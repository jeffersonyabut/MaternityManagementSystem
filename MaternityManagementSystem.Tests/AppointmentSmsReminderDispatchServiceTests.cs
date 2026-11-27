using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class AppointmentSmsReminderDispatchServiceTests
{
    private static readonly DateTime NowUtc =
        new(2026, 12, 7, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly AppointmentDate = new(2026, 12, 10);
    private static readonly TimeOnly AppointmentTime = new(9, 0);

    [Fact]
    public async Task ClaimPendingReminder_SeparateContextsOnlyAllowOneClaim()
    {
        await using var fixture = await DispatchFixture.CreateAsync();
        int reminderId = await fixture.CreateDueReminderAsync();
        var first = new AppointmentSmsReminderRepository(fixture.Database.Factory);
        var second = new AppointmentSmsReminderRepository(fixture.Database.Factory);

        bool[] claims = await Task.WhenAll(
            first.TryClaimPendingReminderAsync(
                reminderId, NowUtc, new DateOnly(2026, 12, 7), new TimeOnly(9, 0)),
            second.TryClaimPendingReminderAsync(
                reminderId, NowUtc, new DateOnly(2026, 12, 7), new TimeOnly(9, 0)));

        Assert.Single(claims, claimed => claimed);
        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await context.AppointmentSmsReminders.SingleAsync();
        Assert.Equal(AppointmentSmsReminderStatus.Sending, reminder.Status);
        Assert.Equal(NowUtc, AppointmentSmsReminderService.TreatAsUtc(
            reminder.AttemptStartedAtUtc!.Value));
    }

    [Theory]
    [InlineData("consent")]
    [InlineData("schedule")]
    [InlineData("status")]
    [InlineData("archive")]
    [InlineData("contact")]
    public async Task DispatchDue_WhenEligibilityChangedBeforeDispatch_DoesNotSend(
        string change)
    {
        var client = new FakeUniSmsClient();
        await using var fixture = await DispatchFixture.CreateAsync();
        int staleReminderId = await fixture.CreateDueReminderAsync();

        await fixture.MakeIneligibleAsync(change);

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(NowUtc);

        Assert.Equal(0, client.SendCount);
        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var stale = await context.AppointmentSmsReminders.SingleAsync(
            reminder => reminder.Id == staleReminderId);
        Assert.Contains(stale.Status, new[]
        {
            AppointmentSmsReminderStatus.Skipped,
            AppointmentSmsReminderStatus.Superseded
        });
    }

    [Theory]
    [InlineData(SystemSettingKeys.Notifications.SmsEnabled)]
    [InlineData(SystemSettingKeys.Notifications.AppointmentReminders)]
    public async Task DispatchDue_WhenEitherGlobalSmsSettingDisabled_DoesNotSendOrClearPending(
        string settingKey)
    {
        var client = new FakeUniSmsClient();
        await using var fixture = await DispatchFixture.CreateAsync();
        int reminderId = await fixture.CreateDueReminderAsync();
        await fixture.SetSettingAsync(settingKey, "false");

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(NowUtc);

        Assert.Equal(0, client.SendCount);
        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        Assert.Equal(AppointmentSmsReminderStatus.Pending,
            (await context.AppointmentSmsReminders.SingleAsync(
                reminder => reminder.Id == reminderId)).Status);
    }

    [Fact]
    public async Task ClaimPendingReminder_ChangedConsentGenerationCannotClaimOldSnapshot()
    {
        var client = new FakeUniSmsClient();
        await using var fixture = await DispatchFixture.CreateAsync();
        int reminderId = await fixture.CreateDueReminderAsync();
        await fixture.MakeIneligibleAsync("generation");

        bool claimed = await new AppointmentSmsReminderRepository(fixture.Database.Factory)
            .TryClaimPendingReminderAsync(
                reminderId,
                NowUtc,
                new DateOnly(2026, 12, 7),
                new TimeOnly(9, 0));

        Assert.False(claimed);
        Assert.Equal(0, client.SendCount);
    }

    [Fact]
    public async Task DispatchDue_RevalidatesEligibilityAfterAtomicClaim()
    {
        var client = new FakeUniSmsClient();
        await using var fixture = await DispatchFixture.CreateAsync();
        await fixture.CreateDueReminderAsync();
        await using (var context = await fixture.Database.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER WithdrawConsentAfterReminderClaim
                AFTER UPDATE OF Status ON AppointmentSmsReminders
                WHEN NEW.Status = 'Sending'
                BEGIN
                    UPDATE Patients
                    SET HasSmsReminderConsent = 0
                    WHERE Id = (
                        SELECT PatientId FROM Appointments
                        WHERE Id = NEW.AppointmentId);
                END;
                """);
        }

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(NowUtc);

        Assert.Equal(0, client.SendCount);
        await using var verify = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await verify.AppointmentSmsReminders.SingleAsync(
            item => item.ReminderType == AppointmentSmsReminderType.Advance3Day);
        Assert.Equal(AppointmentSmsReminderStatus.Skipped, reminder.Status);
        Assert.Equal("consent_withdrawn", reminder.SanitizedOutcomeCode);
    }

    [Theory]
    [InlineData(SystemSettingKeys.Notifications.SmsEnabled)]
    [InlineData(SystemSettingKeys.Notifications.AppointmentReminders)]
    public async Task DispatchDue_RevalidatesGlobalSettingsAfterAtomicClaim(
        string settingKey)
    {
        var client = new FakeUniSmsClient();
        await using var fixture = await DispatchFixture.CreateAsync();
        await fixture.CreateDueReminderAsync();
        await using (var context = await fixture.Database.Factory.CreateDbContextAsync())
        {
            string triggerSql = settingKey switch
            {
                SystemSettingKeys.Notifications.SmsEnabled =>
                    """
                    CREATE TRIGGER DisableSmsSettingAfterClaim
                    AFTER UPDATE OF Status ON AppointmentSmsReminders
                    WHEN NEW.Status = 'Sending'
                    BEGIN
                        UPDATE SystemSettings SET Value = 'false'
                        WHERE Key = 'Notifications.SmsEnabled';
                    END;
                    """,
                SystemSettingKeys.Notifications.AppointmentReminders =>
                    """
                    CREATE TRIGGER DisableAppointmentReminderSettingAfterClaim
                    AFTER UPDATE OF Status ON AppointmentSmsReminders
                    WHEN NEW.Status = 'Sending'
                    BEGIN
                        UPDATE SystemSettings SET Value = 'false'
                        WHERE Key = 'Notifications.AppointmentReminders';
                    END;
                    """,
                _ => throw new ArgumentOutOfRangeException(nameof(settingKey))
            };
            await context.Database.ExecuteSqlRawAsync(triggerSql);
        }

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(NowUtc);

        Assert.Equal(0, client.SendCount);
        await using var verify = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await verify.AppointmentSmsReminders.SingleAsync(
            item => item.ReminderType == AppointmentSmsReminderType.Advance3Day);
        Assert.Equal(AppointmentSmsReminderStatus.Skipped, reminder.Status);
        Assert.Equal("sms_settings_disabled", reminder.SanitizedOutcomeCode);
    }

    [Fact]
    public async Task DispatchDue_AcceptedProviderResponsePersistsReferenceWithoutClaimingDelivery()
    {
        var client = new FakeUniSmsClient(new UniSmsApiResult(
            UniSmsOutcome.Accepted,
            "msg_dispatch_123",
            "pending"));
        await using var fixture = await DispatchFixture.CreateAsync();
        await fixture.CreateDueReminderAsync();

        int claimed = await fixture.CreateDispatchService(client)
            .DispatchDueRemindersAsync(NowUtc);

        Assert.Equal(1, claimed);
        Assert.Equal(1, client.SendCount);
        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await context.AppointmentSmsReminders.SingleAsync(
            item => item.ReminderType == AppointmentSmsReminderType.Advance3Day);
        Assert.Equal(AppointmentSmsReminderStatus.ProviderAccepted, reminder.Status);
        Assert.Equal("msg_dispatch_123", reminder.ProviderReference);
        Assert.Equal("pending", reminder.ProviderStatus);
        Assert.Equal("provider_accepted", reminder.SanitizedOutcomeCode);
        Assert.NotNull(reminder.FinalizedAtUtc);
    }

    [Fact]
    public async Task DispatchDue_ExplicitProviderRejectionPersistsFailed()
    {
        var client = new FakeUniSmsClient(new UniSmsApiResult(
            UniSmsOutcome.Rejected,
            FailureCode: "request_rejected"));
        await using var fixture = await DispatchFixture.CreateAsync();
        await fixture.CreateDueReminderAsync();

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(NowUtc);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await context.AppointmentSmsReminders.SingleAsync(
            item => item.ReminderType == AppointmentSmsReminderType.Advance3Day);
        Assert.Equal(AppointmentSmsReminderStatus.Failed, reminder.Status);
        Assert.Equal("request_rejected", reminder.SanitizedOutcomeCode);
    }

    [Fact]
    public async Task DispatchDue_AmbiguousProviderResultPersistsUnknownAndIsNeverRetried()
    {
        var client = new FakeUniSmsClient(new UniSmsApiResult(
            UniSmsOutcome.Unknown,
            FailureCode: "network_error"));
        await using var fixture = await DispatchFixture.CreateAsync();
        await fixture.CreateDueReminderAsync();
        var service = fixture.CreateDispatchService(client);

        await service.DispatchDueRemindersAsync(NowUtc);
        await service.DispatchDueRemindersAsync(NowUtc.AddMinutes(5));

        Assert.Equal(1, client.SendCount);
        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await context.AppointmentSmsReminders.SingleAsync(
            item => item.ReminderType == AppointmentSmsReminderType.Advance3Day);
        Assert.Equal(AppointmentSmsReminderStatus.Unknown, reminder.Status);
        Assert.Equal("network_error", reminder.SanitizedOutcomeCode);
    }

    [Fact]
    public async Task DispatchDue_StaleSendingReminderIsNeverRetried()
    {
        var client = new FakeUniSmsClient();
        await using var fixture = await DispatchFixture.CreateAsync();
        int reminderId = await fixture.CreateDueReminderAsync();
        await using (var context = await fixture.Database.Factory.CreateDbContextAsync())
        {
            var reminder = await context.AppointmentSmsReminders.SingleAsync();
            reminder.Status = AppointmentSmsReminderStatus.Sending;
            reminder.AttemptStartedAtUtc = NowUtc;
            reminder.StatusChangedAtUtc = NowUtc;
            await context.SaveChangesAsync();
        }

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(
            NowUtc.AddHours(1));

        Assert.Equal(0, client.SendCount);
        await using var verification = await fixture.Database.Factory.CreateDbContextAsync();
        Assert.Equal(AppointmentSmsReminderStatus.Sending,
            (await verification.AppointmentSmsReminders.SingleAsync(
                reminder => reminder.Id == reminderId)).Status);
    }

    [Fact]
    public async Task DispatchDue_ConcurrentPassesSendOnlyOneRequest()
    {
        var client = new FakeUniSmsClient();
        await using var fixture = await DispatchFixture.CreateAsync();
        await fixture.CreateDueReminderAsync();
        var first = fixture.CreateDispatchService(client);
        var second = fixture.CreateDispatchService(client);

        await Task.WhenAll(
            first.DispatchDueRemindersAsync(NowUtc),
            second.DispatchDueRemindersAsync(NowUtc));

        Assert.Equal(1, client.SendCount);
    }

    [Fact]
    public async Task DispatchDue_UsesFakeHttpHandlerAndSafeGenericMessage()
    {
        using var handler = new FakeHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://unismsapi.com/api/sms", request.RequestUri?.ToString());
            return Task.FromResult(JsonResponse(
                HttpStatusCode.Created,
                "pending",
                "msg_http_test_123"));
        });
        await using var fixture = await DispatchFixture.CreateAsync();
        int reminderId = await fixture.CreateDueReminderAsync();
        using var httpClient = new HttpClient(handler);
        UniSmsClient client = await fixture.CreateUniSmsClientAsync(httpClient);

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(NowUtc);

        string content = JsonDocument.Parse(handler.LastRequestBody!)
            .RootElement.GetProperty("content").GetString()!;
        Assert.Contains("Goldenstate Maternity Clinic", content);
        Assert.Contains("December 10, 2026", content);
        Assert.Contains("9:00 AM", content);
        Assert.DoesNotContain("Alice", content);
        Assert.DoesNotContain("Patient", content);
        Assert.DoesNotContain("Prenatal consultation", content);
        Assert.DoesNotContain("pregnan", content, StringComparison.OrdinalIgnoreCase);
        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await context.AppointmentSmsReminders.SingleAsync(
            item => item.Id == reminderId);
        Assert.Equal(AppointmentSmsReminderStatus.ProviderAccepted, reminder.Status);
        Assert.Equal("msg_http_test_123", reminder.ProviderReference);
    }

    [Fact]
    public async Task DispatchDue_FakeHttpRejectionAndNetworkFailurePersistTerminalOutcomes()
    {
        await VerifyHttpOutcomeAsync(
            (_, _) => Task.FromResult(new HttpResponseMessage(
                HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("secret clinical body")
            }),
            AppointmentSmsReminderStatus.Failed,
            "request_rejected");

        await VerifyHttpOutcomeAsync(
            (_, _) => throw new HttpRequestException("private response text"),
            AppointmentSmsReminderStatus.Unknown,
            "network_error");
    }

    [Fact]
    public void MessageBuilder_ContainsOnlyGenericAppointmentDetails()
    {
        string message = new AppointmentSmsReminderMessageBuilder().Build(
            "Goldenstate Maternity Clinic",
            AppointmentDate,
            AppointmentTime);

        Assert.Equal(
            "Reminder: You have an upcoming appointment at Goldenstate Maternity Clinic " +
            "on December 10, 2026 at 9:00 AM. Please contact the clinic if you need to reschedule.",
            message);
    }

    private static async Task VerifyHttpOutcomeAsync(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response,
        AppointmentSmsReminderStatus expectedStatus,
        string expectedCode)
    {
        using var handler = new FakeHttpMessageHandler(response);
        await using var fixture = await DispatchFixture.CreateAsync();
        await fixture.CreateDueReminderAsync();
        using var httpClient = new HttpClient(handler);
        UniSmsClient client = await fixture.CreateUniSmsClientAsync(httpClient);

        await fixture.CreateDispatchService(client).DispatchDueRemindersAsync(NowUtc);

        await using var context = await fixture.Database.Factory.CreateDbContextAsync();
        var reminder = await context.AppointmentSmsReminders.SingleAsync(
            item => item.ReminderType == AppointmentSmsReminderType.Advance3Day);
        Assert.Equal(expectedStatus, reminder.Status);
        Assert.Equal(expectedCode, reminder.SanitizedOutcomeCode);
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string status,
        string referenceId) => new(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    message = new { status, reference_id = referenceId }
                }),
                Encoding.UTF8,
                "application/json")
        };

    private sealed class FakeUniSmsClient(UniSmsApiResult? result = null) : IUniSmsClient
    {
        private int _sendCount;
        public int SendCount => Volatile.Read(ref _sendCount);

        public Task<UniSmsSendResult> SendTestSmsAsync(
            string recipient,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UniSmsApiResult> SendAppointmentReminderAsync(
            string recipient,
            string content,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _sendCount);
            return Task.FromResult(result ?? new UniSmsApiResult(
                UniSmsOutcome.Accepted,
                "msg_fake_123",
                "pending"));
        }

        public Task<UniSmsApiResult> GetDeliveryStatusAsync(
            string referenceId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await response(request, cancellationToken);
        }
    }

    private sealed class DispatchFixture : IAsyncDisposable
    {
        private const string FakeApiKey = "dispatch-test-api-key";
        private readonly string _tempDirectory;
        private readonly int _staffId;
        private readonly string _username;
        private readonly string _securityStamp;

        public TestDatabase Database { get; }
        public int AppointmentId { get; }

        private DispatchFixture(
            TestDatabase database,
            string tempDirectory,
            int staffId,
            string username,
            string securityStamp,
            int appointmentId)
        {
            Database = database;
            _tempDirectory = tempDirectory;
            _staffId = staffId;
            _username = username;
            _securityStamp = securityStamp;
            AppointmentId = appointmentId;
        }

        public static async Task<DispatchFixture> CreateAsync()
        {
            TestDatabase database = await TestDatabase.CreateAsync();
            string tempDirectory = Path.Combine(
                Path.GetTempPath(), $"GolddieReminderDispatch_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                const string username = "reminder-dispatch-test";
                string uniqueUsername = $"{username}-{Guid.NewGuid():N}";
                await using var context = await database.Factory.CreateDbContextAsync();
                var staff = new Staff
                {
                    Username = uniqueUsername,
                    NormalizedUsername = UsernameNormalizer.Normalize(uniqueUsername),
                    PasswordHash = "test-hash",
                    Role = StaffRoles.Admin,
                    IsActive = true
                };
                context.StaffMembers.Add(staff);

                var patient = new Patient
                {
                    PatientNumber = $"DIS-{Guid.NewGuid():N}"[..12],
                    FirstName = "Alice",
                    LastName = "Patient",
                    ContactNumber = "09171234567",
                    HasSmsReminderConsent = true,
                    SmsReminderConsentGeneration = 1
                };
                var appointment = new Appointment
                {
                    Patient = patient,
                    AppointmentDate = AppointmentDate,
                    StartTime = AppointmentTime,
                    Purpose = "Prenatal consultation",
                    Status = "Scheduled",
                    ScheduleRevision = 1
                };
                context.Appointments.Add(appointment);
                context.SystemSettings.AddRange(
                    Setting(SystemSettingKeys.Clinic.Name, "Goldenstate Maternity Clinic"),
                    Setting(SystemSettingKeys.Notifications.SmsEnabled, "true"),
                    Setting(SystemSettingKeys.Notifications.AppointmentReminders, "true"));
                await context.SaveChangesAsync();

                return new DispatchFixture(
                    database,
                    tempDirectory,
                    staff.Id,
                    staff.Username,
                    staff.SecurityStamp,
                    appointment.Id);
            }
            catch
            {
                await database.DisposeAsync();
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }

                throw;
            }
        }

        public AppointmentSmsReminderDispatchService CreateDispatchService(
            IUniSmsClient client)
        {
            var reminderRepository = new AppointmentSmsReminderRepository(Database.Factory);
            var settingsRepository = new SystemSettingRepository(Database.Factory);
            return new AppointmentSmsReminderDispatchService(
                new AppointmentSmsReminderService(reminderRepository, settingsRepository),
                reminderRepository,
                settingsRepository,
                client);
        }

        public async Task<int> CreateDueReminderAsync()
        {
            await using var context = await Database.Factory.CreateDbContextAsync();
            var reminder = new AppointmentSmsReminder
            {
                AppointmentId = AppointmentId,
                ScheduleRevision = 1,
                ConsentGeneration = 1,
                ReminderType = AppointmentSmsReminderType.Advance3Day,
                ScheduledAtUtc = NowUtc.AddMinutes(-1),
                AppointmentDateSnapshot = AppointmentDate,
                AppointmentStartTimeSnapshot = AppointmentTime,
                Status = AppointmentSmsReminderStatus.Pending,
                CreatedAtUtc = NowUtc.AddHours(-1),
                StatusChangedAtUtc = NowUtc.AddHours(-1)
            };
            context.AppointmentSmsReminders.Add(reminder);
            await context.SaveChangesAsync();
            return reminder.Id;
        }

        public async Task MakeIneligibleAsync(string change)
        {
            await using var context = await Database.Factory.CreateDbContextAsync();
            var appointment = await context.Appointments
                .Include(item => item.Patient)
                .SingleAsync(item => item.Id == AppointmentId);

            switch (change)
            {
                case "consent":
                    appointment.Patient.HasSmsReminderConsent = false;
                    break;
                case "generation":
                    appointment.Patient.SmsReminderConsentGeneration = 2;
                    break;
                case "schedule":
                    appointment.ScheduleRevision = 2;
                    appointment.AppointmentDate = AppointmentDate.AddDays(1);
                    break;
                case "status":
                    appointment.Status = "Cancelled";
                    break;
                case "archive":
                    appointment.IsArchived = true;
                    break;
                case "contact":
                    appointment.Patient.ContactNumber = "invalid";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change));
            }

            await context.SaveChangesAsync();
        }

        public async Task SetSettingAsync(string key, string value)
        {
            await using var context = await Database.Factory.CreateDbContextAsync();
            var setting = await context.SystemSettings.SingleAsync(item => item.Key == key);
            setting.Value = value;
            await context.SaveChangesAsync();
        }

        public async Task<UniSmsClient> CreateUniSmsClientAsync(HttpClient httpClient)
        {
            var session = new SessionService();
            session.StartSession(new UserSession(
                _staffId,
                _username,
                StaffRoles.Admin,
                _securityStamp));
            var authorization = new AuthorizationService(
                session,
                new StaffRepository(Database.Factory));
            var credentials = new UniSmsCredentialService(
                authorization,
                Path.Combine(_tempDirectory, "unisms-api-key.dat"));
            await credentials.SaveApiKeyAsync(FakeApiKey);
            return new UniSmsClient(credentials, httpClient);
        }

        private static SystemSetting Setting(string key, string value) => new()
        {
            Key = key,
            Value = value,
            UpdatedAtUtc = NowUtc
        };

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
    }
}
