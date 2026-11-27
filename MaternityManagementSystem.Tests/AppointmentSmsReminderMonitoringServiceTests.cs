using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public sealed class AppointmentSmsReminderMonitoringServiceTests
{
    private const string PageOperation = "Page";
    private const string StatusCountsOperation = "StatusCounts";

    private static async Task<(
        AppointmentSmsReminderMonitoringService Service,
        SessionService Session,
        int StaffId)> CreateServiceAsync(
        TestDatabase database,
        string role,
        IDbContextFactory<ClinicDbContext>? monitoringContextFactory = null)
    {
        int staffId;
        string securityStamp;

        await using (var context =
                     await database.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = "reminder-monitoring-test-user",
                PasswordHash = "test-password-hash",
                Role = role,
                IsActive = true
            };

            context.StaffMembers.Add(staff);
            await context.SaveChangesAsync();

            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var sessionService = new SessionService();
        sessionService.StartSession(new UserSession(
            staffId,
            "reminder-monitoring-test-user",
            role,
            securityStamp));

        var authorizationService = new AuthorizationService(
            sessionService,
            new StaffRepository(database.Factory));
        var service = new AppointmentSmsReminderMonitoringService(
            new AppointmentSmsReminderRepository(
                monitoringContextFactory ?? database.Factory),
            authorizationService);

        return (service, sessionService, staffId);
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<ClinicDbContext>
    {
        public int CreateContextCallCount { get; private set; }

        public ClinicDbContext CreateDbContext()
        {
            CreateContextCallCount++;
            throw new InvalidOperationException(
                "The monitoring repository must not create a context for an unauthorized request.");
        }

        public Task<ClinicDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            CreateContextCallCount++;
            throw new InvalidOperationException(
                "The monitoring repository must not create a context for an unauthorized request.");
        }
    }

    [Fact]
    public async Task GetMonitoringDataAsync_WhenAdmin_ReturnsRepositoryDtos()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (service, _, _) = await CreateServiceAsync(
            database,
            StaffRoles.Admin);

        var query = new AppointmentSmsReminderMonitoringQuery
        {
            SearchText = "patient",
            PageNumber = 1,
            PageSize = 25
        };

        AppointmentSmsReminderMonitoringPage page =
            await service.GetMonitoringPageAsync(query);
        IReadOnlyList<AppointmentSmsReminderStatusCount> counts =
            await service.GetMonitoringStatusCountsAsync();

        Assert.Empty(page.Records);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(
            Enum.GetValues<AppointmentSmsReminderStatus>(),
            counts.Select(item => item.Status));
        Assert.All(counts, item => Assert.Equal(0, item.Count));
    }

    [Fact]
    public async Task GetMonitoringDataAsync_WhenStaff_ThrowsUnauthorizedAccessException()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (service, _, _) = await CreateServiceAsync(
            database,
            StaffRoles.Staff);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetMonitoringPageAsync(
                new AppointmentSmsReminderMonitoringQuery()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetMonitoringStatusCountsAsync());
    }

    [Theory]
    [InlineData(PageOperation, "NoSession")]
    [InlineData(StatusCountsOperation, "NoSession")]
    [InlineData(PageOperation, "InactiveAdmin")]
    [InlineData(StatusCountsOperation, "InactiveAdmin")]
    [InlineData(PageOperation, "ChangedRole")]
    [InlineData(StatusCountsOperation, "ChangedRole")]
    [InlineData(PageOperation, "ChangedSecurityStamp")]
    [InlineData(StatusCountsOperation, "ChangedSecurityStamp")]
    public async Task MonitoringOperation_WhenAdminSessionIsInvalid_ThrowsUnauthorizedAccessException(
        string operation,
        string invalidState)
    {
        await using var database = await TestDatabase.CreateAsync();
        var (service, session, staffId) = await CreateServiceAsync(
            database,
            StaffRoles.Admin);

        if (invalidState == "NoSession")
        {
            session.EndSession();
        }
        else
        {
            await using var context =
                await database.Factory.CreateDbContextAsync();
            Staff staff = await context.StaffMembers
                .SingleAsync(member => member.Id == staffId);

            switch (invalidState)
            {
                case "InactiveAdmin":
                    staff.IsActive = false;
                    break;
                case "ChangedRole":
                    staff.Role = StaffRoles.Staff;
                    break;
                case "ChangedSecurityStamp":
                    staff.SecurityStamp = Guid.NewGuid().ToString("N");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidState));
            }

            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => InvokeOperationAsync(service, operation));
    }

    [Fact]
    public async Task MonitoringOperations_WhenStaff_RejectBeforeMonitoringRepositoryAccess()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repositoryContextFactory = new ThrowingDbContextFactory();
        var (service, _, _) = await CreateServiceAsync(
            database,
            StaffRoles.Staff,
            repositoryContextFactory);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => InvokeOperationAsync(service, PageOperation));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => InvokeOperationAsync(service, StatusCountsOperation));

        Assert.Equal(0, repositoryContextFactory.CreateContextCallCount);
    }

    [Fact]
    public async Task GetMonitoringDataAsync_WhenCancellationRequested_ForwardsCancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (service, _, _) = await CreateServiceAsync(
            database,
            StaffRoles.Admin);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        OperationCanceledException pageException =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetMonitoringPageAsync(
                new AppointmentSmsReminderMonitoringQuery(),
                cancellation.Token));

        Assert.Equal(cancellation.Token, pageException.CancellationToken);

        OperationCanceledException countsException =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetMonitoringStatusCountsAsync(
                cancellation.Token));

        Assert.Equal(cancellation.Token, countsException.CancellationToken);
    }

    private static Task InvokeOperationAsync(
        AppointmentSmsReminderMonitoringService service,
        string operation)
    {
        return operation switch
        {
            PageOperation => service.GetMonitoringPageAsync(
                new AppointmentSmsReminderMonitoringQuery()),
            StatusCountsOperation =>
                service.GetMonitoringStatusCountsAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}
