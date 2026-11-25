
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class DashboardAnalyticsServiceTests
{
    private static readonly DateTime FixedUtcNow =
        new(2026, 7, 1, 0, 30, 0, DateTimeKind.Utc);

    private static async Task<DashboardAnalyticsService>
        CreateServiceAsync(
            TestDatabase testDatabase,
            bool authenticated = true)
    {
        var sessionService = new SessionService();

        if (authenticated)
        {
            var staff = new Staff
            {
                Username = "dashboardstaff",
                NormalizedUsername = "DASHBOARDSTAFF",
                PasswordHash = "test-password-hash",
                Role = StaffRoles.Staff,
                IsActive = true
            };

            await using var db =
                await testDatabase.Factory.CreateDbContextAsync();

            db.StaffMembers.Add(staff);
            await db.SaveChangesAsync();

            sessionService.StartSession(
                new UserSession(
                    staff.Id,
                    staff.Username,
                    staff.Role,
                    staff.SecurityStamp));
        }

        var authorizationService = new AuthorizationService(
            sessionService,
            new StaffRepository(testDatabase.Factory));

        var regionalSettingsService = new RegionalSettingsService(
            new SystemSettingRepository(testDatabase.Factory));

        return new DashboardAnalyticsService(
            authorizationService,
            regionalSettingsService,
            testDatabase.Factory,
            new FixedClock(FixedUtcNow));
    }

    private static Patient CreatePatient(
        string patientNumber,
        string firstName,
        DateTime createdAtUtc)
    {
        return new Patient
        {
            PatientNumber = patientNumber,
            FirstName = firstName,
            LastName = "Test",
            CreatedAtUtc = createdAtUtc
        };
    }

    private static Appointment CreateAppointment(
        int patientId,
        DateOnly date,
        TimeOnly time,
        string status,
        bool isArchived = false)
    {
        return new Appointment
        {
            PatientId = patientId,
            AppointmentDate = date,
            StartTime = time,
            Purpose = "Prenatal checkup",
            Status = status,
            IsArchived = isArchived
        };
    }

    [Fact]
    public async Task GetDashboardDataAsync_WhenDatabaseIsEmpty_ReturnsZeroCounts()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        DashboardData result =
            await service.GetDashboardDataAsync();

        Assert.Equal(0, result.TotalPatients);
        Assert.Equal(0, result.ActivePregnancies);
        Assert.Equal(0, result.TodaysAppointments);
        Assert.Equal(0, result.TotalPrenatalVisits);

        Assert.Equal(6, result.MonthlyRegistrations.Count);

        Assert.All(
            result.MonthlyRegistrations,
            item => Assert.Equal(0, item.Count));

        Assert.Equal(6, result.MonthlyPrenatalVisits.Count);

        Assert.All(
            result.MonthlyPrenatalVisits,
            item => Assert.Equal(0, item.Count));

        Assert.Equal(
            new[]
            {
                "Scheduled",
                "Confirmed",
                "Completed",
                "Cancelled",
                "No-show"
            },
            result.AppointmentDistribution
                .Select(item => item.Status));

        Assert.All(
            result.AppointmentDistribution,
            item => Assert.Equal(0, item.Count));

        Assert.Empty(result.UpcomingAppointments);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsCorrectSummaryAndAppointmentDistribution()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        await using (var db =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var firstPatient = CreatePatient(
                "DASH-001",
                "Alice",
                new DateTime(
                    2026, 6, 1, 0, 0, 0,
                    DateTimeKind.Utc));

            var secondPatient = CreatePatient(
                "DASH-002",
                "Bea",
                new DateTime(
                    2026, 6, 2, 0, 0, 0,
                    DateTimeKind.Utc));

            db.Patients.AddRange(firstPatient, secondPatient);
            await db.SaveChangesAsync();

            var currentPregnancy = new Pregnancy
            {
                PatientId = firstPatient.Id,
                Status = "Current"
            };

            var completedPregnancy = new Pregnancy
            {
                PatientId = secondPatient.Id,
                Status = "Completed"
            };

            db.Pregnancies.AddRange(
                currentPregnancy,
                completedPregnancy);

            await db.SaveChangesAsync();

            db.PrenatalVisits.AddRange(
                new PrenatalVisit
                {
                    PregnancyId = currentPregnancy.Id,
                    VisitDate = new DateOnly(2026, 6, 10)
                },
                new PrenatalVisit
                {
                    PregnancyId = completedPregnancy.Id,
                    VisitDate = new DateOnly(2026, 6, 15)
                });

            await db.SaveChangesAsync();

            var today = new DateOnly(2026, 7, 1);

            db.Appointments.AddRange(
                CreateAppointment(
                    firstPatient.Id,
                    today,
                    new TimeOnly(9, 0),
                    "Scheduled"),

                CreateAppointment(
                    firstPatient.Id,
                    today,
                    new TimeOnly(10, 0),
                    "Confirmed"),

                CreateAppointment(
                    firstPatient.Id,
                    today,
                    new TimeOnly(11, 0),
                    "Completed"),

                CreateAppointment(
                    firstPatient.Id,
                    today,
                    new TimeOnly(12, 0),
                    "Cancelled"),

                CreateAppointment(
                    firstPatient.Id,
                    today,
                    new TimeOnly(13, 0),
                    "No-show"),

                CreateAppointment(
                    secondPatient.Id,
                    today,
                    new TimeOnly(14, 0),
                    "Scheduled",
                    isArchived: true));

            await db.SaveChangesAsync();
        }

        DashboardData result =
            await service.GetDashboardDataAsync();

        Assert.Equal(2, result.TotalPatients);
        Assert.Equal(1, result.ActivePregnancies);
        Assert.Equal(2, result.TodaysAppointments);
        Assert.Equal(2, result.TotalPrenatalVisits);

        Assert.Equal(5, result.AppointmentDistribution.Count);

        Assert.Equal(
            1,
            result.AppointmentDistribution
                .Single(item => item.Status == "Scheduled").Count);

        Assert.Equal(
            1,
            result.AppointmentDistribution
                .Single(item => item.Status == "Confirmed").Count);

        Assert.Equal(
            1,
            result.AppointmentDistribution
                .Single(item => item.Status == "Completed").Count);

        Assert.Equal(
            1,
            result.AppointmentDistribution
                .Single(item => item.Status == "Cancelled").Count);

        Assert.Equal(
            1,
            result.AppointmentDistribution
                .Single(item => item.Status == "No-show").Count);
    }

    [Fact]
    public async Task GetDashboardDataAsync_GroupsPatientRegistrationsByClinicLocalMonth()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        await using (var db =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            db.Patients.AddRange(
                CreatePatient(
                    "BOUNDARY-001",
                    "BeforeWindow",
                    new DateTime(
                        2026, 1, 31, 15, 59, 0,
                        DateTimeKind.Utc)),

                CreatePatient(
                    "BOUNDARY-002",
                    "February",
                    new DateTime(
                        2026, 1, 31, 16, 0, 0,
                        DateTimeKind.Utc)),

                CreatePatient(
                    "BOUNDARY-003",
                    "JulyOne",
                    new DateTime(
                        2026, 6, 30, 16, 30, 0,
                        DateTimeKind.Utc)),

                CreatePatient(
                    "BOUNDARY-004",
                    "JulyTwo",
                    new DateTime(
                        2026, 7, 31, 15, 59, 0,
                        DateTimeKind.Utc)),

                CreatePatient(
                    "BOUNDARY-005",
                    "AfterWindow",
                    new DateTime(
                        2026, 7, 31, 16, 0, 0,
                        DateTimeKind.Utc)));

            await db.SaveChangesAsync();
        }

        DashboardData result =
            await service.GetDashboardDataAsync();

        Assert.Equal(6, result.MonthlyRegistrations.Count);

        Assert.Equal(
            new[] { 1, 0, 0, 0, 0, 2 },
            result.MonthlyRegistrations
                .Select(item => item.Count)
                .ToArray());

        Assert.DoesNotContain(
            result.MonthlyRegistrations,
            item => item.Count < 0);
    }

    [Fact]
    public async Task GetDashboardDataAsync_GroupsPrenatalVisitsIntoSixMonths()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        await using (var db =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = CreatePatient(
                "PRENATAL-001",
                "Prenatal",
                FixedUtcNow);

            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var pregnancy = new Pregnancy
            {
                PatientId = patient.Id,
                Status = "Current"
            };

            db.Pregnancies.Add(pregnancy);
            await db.SaveChangesAsync();

            db.PrenatalVisits.AddRange(
                new PrenatalVisit
                {
                    PregnancyId = pregnancy.Id,
                    VisitDate = new DateOnly(2026, 2, 1)
                },
                new PrenatalVisit
                {
                    PregnancyId = pregnancy.Id,
                    VisitDate = new DateOnly(2026, 2, 15)
                },
                new PrenatalVisit
                {
                    PregnancyId = pregnancy.Id,
                    VisitDate = new DateOnly(2026, 7, 31)
                },
                new PrenatalVisit
                {
                    PregnancyId = pregnancy.Id,
                    VisitDate = new DateOnly(2026, 1, 31)
                },
                new PrenatalVisit
                {
                    PregnancyId = pregnancy.Id,
                    VisitDate = new DateOnly(2026, 8, 1)
                });

            await db.SaveChangesAsync();
        }

        DashboardData result =
            await service.GetDashboardDataAsync();

        Assert.Equal(
            new[] { 2, 0, 0, 0, 0, 1 },
            result.MonthlyPrenatalVisits
                .Select(item => item.Count)
                .ToArray());
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsOnlyFiveFutureScheduledOrConfirmedAppointments()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int[] expectedAppointmentIds;

        await using (var db =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var patient = CreatePatient(
                "UPCOMING-001",
                "Upcoming",
                FixedUtcNow);

            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var today = new DateOnly(2026, 7, 1);
            var tomorrow = today.AddDays(1);

            var appointments = new List<Appointment>
            {
                CreateAppointment(
                    patient.Id,
                    today,
                    new TimeOnly(8, 29),
                    "Scheduled"),

                CreateAppointment(
                    patient.Id,
                    today,
                    new TimeOnly(8, 31),
                    "Scheduled"),

                CreateAppointment(
                    patient.Id,
                    today,
                    new TimeOnly(9, 0),
                    "Confirmed"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(8, 0),
                    "Scheduled"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(9, 0),
                    "Confirmed"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(10, 0),
                    "Scheduled"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(11, 0),
                    "Confirmed"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(12, 0),
                    "Scheduled"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(13, 0),
                    "Cancelled"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(14, 0),
                    "Completed"),

                CreateAppointment(
                    patient.Id,
                    tomorrow,
                    new TimeOnly(15, 0),
                    "Scheduled",
                    isArchived: true)
            };

            db.Appointments.AddRange(appointments);
            await db.SaveChangesAsync();

            expectedAppointmentIds = appointments
                .Skip(1)
                .Take(5)
                .Select(item => item.Id)
                .ToArray();
        }

        DashboardData result =
            await service.GetDashboardDataAsync();

        Assert.Equal(5, result.UpcomingAppointments.Count);

        Assert.Equal(
            expectedAppointmentIds,
            result.UpcomingAppointments
                .Select(item => item.AppointmentId)
                .ToArray());

        Assert.All(
            result.UpcomingAppointments,
            appointment => Assert.Contains(
                appointment.Status,
                new[] { "Scheduled", "Confirmed" }));

        Assert.All(
            result.UpcomingAppointments,
            appointment => Assert.Equal(
                "Upcoming Test",
                appointment.PatientName));
    }

    [Fact]
    public async Task GetDashboardDataAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(
            testDatabase,
            authenticated: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetDashboardDataAsync());
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; }

        public FixedClock(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(
                utcNow,
                DateTimeKind.Utc);
        }
    }
}
