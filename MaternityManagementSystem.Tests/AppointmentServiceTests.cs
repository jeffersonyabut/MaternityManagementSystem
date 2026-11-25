
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class AppointmentServiceTests
{
    private static async Task<AppointmentService> CreateServiceAsync(
        TestDatabase testDatabase,
        string? configuredDuration = "30")
    {
        int staffId;
        const string username = "appointment-test-user";
        string securityStamp;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = "APPOINTMENT-TEST-USER",
                PasswordHash = "test-password-hash",
                Role = StaffRoles.Admin,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.StaffMembers.Add(staff);

            if (configuredDuration is not null)
            {
                dbContext.SystemSettings.Add(new SystemSetting
                {
                    Key = SystemSettingKeys.Appointments.DefaultDuration,
                    Value = configuredDuration
                });
            }

            await dbContext.SaveChangesAsync();

            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var sessionService = new SessionService();

        sessionService.StartSession(
            new UserSession(
                staffId,
                username,
                StaffRoles.Admin,
                securityStamp));

        var staffRepository =
            new StaffRepository(testDatabase.Factory);

        var authorizationService = new AuthorizationService(
            sessionService,
            staffRepository);

        var settingRepository =
            new SystemSettingRepository(testDatabase.Factory);

        var systemSettingService = new SystemSettingService(
            settingRepository,
            authorizationService,
            sessionService);

        var regionalSettingsService =
            new RegionalSettingsService(settingRepository);

        return new AppointmentService(
            new AppointmentRepository(testDatabase.Factory),
            new PatientRepository(testDatabase.Factory),
            new PregnancyRepository(testDatabase.Factory),
            authorizationService,
            sessionService,
            regionalSettingsService,
            systemSettingService);
    }

    private static async Task<int> CreatePatientAsync(
        TestDatabase testDatabase,
        string patientNumber = "PAT-001")
    {
        await using var dbContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var patient = new Patient
        {
            PatientNumber = patientNumber,
            FirstName = "Test",
            LastName = "Patient"
        };

        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync();

        return patient.Id;
    }

    private static DateOnly GetFutureDate()
    {
        return DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(2));
    }

    private static Appointment CreateAppointment(
        int patientId,
        TimeOnly startTime,
        TimeOnly? endTime = null,
        int? pregnancyId = null)
    {
        return new Appointment
        {
            PatientId = patientId,
            PregnancyId = pregnancyId,
            AppointmentDate = GetFutureDate(),
            StartTime = startTime,
            EndTime = endTime,
            Purpose = "Prenatal consultation",
            Status = "Scheduled"
        };
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenEndTimeIsMissing_UsesConfiguredDuration()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(
            testDatabase,
            "45");

        int patientId =
            await CreatePatientAsync(testDatabase);

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(10, 0));

        var result = await service.CreateAppointmentAsync(
            appointment);

        Assert.Equal(
            new TimeOnly(10, 45),
            result.EndTime);

        Assert.Equal("Scheduled", result.Status);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAppointment =
            await verificationContext.Appointments.SingleAsync();

        Assert.Equal(
            new TimeOnly(10, 45),
            savedAppointment.EndTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("241")]
    [InlineData("0")]
    public async Task GetDefaultAppointmentDurationMinutesAsync_WhenSettingIsMissingOrInvalid_ReturnsThirtyMinutes(
        string? configuredDuration)
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(
            testDatabase,
            configuredDuration);

        int result =
            await service.GetDefaultAppointmentDurationMinutesAsync();

        Assert.Equal(30, result);
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenDefaultDurationExtendsPastMidnight_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(
            testDatabase,
            "30");

        int patientId =
            await CreatePatientAsync(testDatabase);

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(23, 45));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAppointmentAsync(appointment));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Empty(
            await verificationContext.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenManualDurationExceeds240Minutes_AllowsSameDayAppointment()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(
            testDatabase,
            "30");

        int patientId =
            await CreatePatientAsync(testDatabase);

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(10, 0),
            new TimeOnly(16, 0));

        var result = await service.CreateAppointmentAsync(
            appointment);

        Assert.Equal(
            new TimeOnly(16, 0),
            result.EndTime);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Single(
            await verificationContext.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenAppointmentOverlapsExistingAppointment_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int patientId =
            await CreatePatientAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            dbContext.Appointments.Add(CreateAppointment(
                patientId,
                new TimeOnly(10, 0),
                new TimeOnly(11, 0)));

            await dbContext.SaveChangesAsync();
        }

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(10, 30),
            new TimeOnly(11, 30));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAppointmentAsync(appointment));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Single(
            await verificationContext.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenAppointmentStartsAtExistingEndTime_AllowsAppointment()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int patientId =
            await CreatePatientAsync(testDatabase);

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            dbContext.Appointments.Add(CreateAppointment(
                patientId,
                new TimeOnly(10, 0),
                new TimeOnly(10, 30)));

            await dbContext.SaveChangesAsync();
        }

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(10, 30),
            new TimeOnly(11, 0));

        var result = await service.CreateAppointmentAsync(
            appointment);

        Assert.NotNull(result);
        Assert.Equal(new TimeOnly(10, 30), result.StartTime);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Equal(
            2,
            await verificationContext.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenPregnancyBelongsToAnotherPatient_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int firstPatientId =
            await CreatePatientAsync(testDatabase, "PAT-001");

        int secondPatientId =
            await CreatePatientAsync(testDatabase, "PAT-002");

        int pregnancyId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var pregnancy = new Pregnancy
            {
                PatientId = secondPatientId,
                Status = "Current"
            };

            dbContext.Pregnancies.Add(pregnancy);
            await dbContext.SaveChangesAsync();

            pregnancyId = pregnancy.Id;
        }

        var appointment = CreateAppointment(
            firstPatientId,
            new TimeOnly(10, 0),
            pregnancyId: pregnancyId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAppointmentAsync(appointment));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Empty(
            await verificationContext.Appointments.ToListAsync());
    }



    [Fact]
    public async Task CreateAppointmentAsync_WhenPatientDoesNotExist_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        var appointment = CreateAppointment(
            patientId: 999,
            new TimeOnly(10, 0));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAppointmentAsync(appointment));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Empty(
            await verificationContext.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenEndTimeIsNotAfterStartTime_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int patientId =
            await CreatePatientAsync(testDatabase);

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(10, 0),
            new TimeOnly(10, 0));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAppointmentAsync(appointment));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Empty(
            await verificationContext.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenAppointmentIsInThePast_ThrowsArgumentException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int patientId =
            await CreatePatientAsync(testDatabase);

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(10, 0));

        appointment.AppointmentDate =
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAppointmentAsync(appointment));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Empty(
            await verificationContext.Appointments.ToListAsync());
    }

    [Fact]
    public async Task UpdateAppointmentAsync_WhenRescheduledToAvailableTime_UpdatesAppointment()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int patientId =
            await CreatePatientAsync(testDatabase);

        int appointmentId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var appointment = CreateAppointment(
                patientId,
                new TimeOnly(10, 0),
                new TimeOnly(10, 30));

            dbContext.Appointments.Add(appointment);
            await dbContext.SaveChangesAsync();

            appointmentId = appointment.Id;
        }

        var updatedAppointment = new Appointment
        {
            Id = appointmentId,
            PatientId = patientId,
            AppointmentDate = GetFutureDate(),
            StartTime = new TimeOnly(11, 0),
            EndTime = new TimeOnly(11, 45),
            Purpose = "Updated prenatal consultation",
            Notes = "Rescheduled successfully",
            Status = "Scheduled"
        };

        var result = await service.UpdateAppointmentAsync(
            updatedAppointment);

        Assert.Equal(new TimeOnly(11, 0), result.StartTime);
        Assert.Equal(new TimeOnly(11, 45), result.EndTime);
        Assert.Equal(
            "Updated prenatal consultation",
            result.Purpose);

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAppointment =
            await verificationContext.Appointments.SingleAsync();

        Assert.Equal(new TimeOnly(11, 0), savedAppointment.StartTime);
        Assert.Equal(new TimeOnly(11, 45), savedAppointment.EndTime);
        Assert.NotNull(savedAppointment.UpdatedAtUtc);

        Assert.True(
            await verificationContext.AuditLogs.AnyAsync(
                audit => audit.EntityType == "Appointment" &&
                         audit.EntityId == appointmentId &&
                         audit.Action == "Updated"));
    }

    [Fact]
    public async Task UpdateAppointmentAsync_WhenRescheduledIntoConflict_ThrowsInvalidOperationException()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        var service = await CreateServiceAsync(testDatabase);

        int patientId =
            await CreatePatientAsync(testDatabase);

        int firstAppointmentId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            var firstAppointment = CreateAppointment(
                patientId,
                new TimeOnly(10, 0),
                new TimeOnly(10, 30));

            var secondAppointment = CreateAppointment(
                patientId,
                new TimeOnly(11, 0),
                new TimeOnly(11, 30));

            dbContext.Appointments.AddRange(
                firstAppointment,
                secondAppointment);

            await dbContext.SaveChangesAsync();

            firstAppointmentId = firstAppointment.Id;
        }

        var updatedAppointment = new Appointment
        {
            Id = firstAppointmentId,
            PatientId = patientId,
            AppointmentDate = GetFutureDate(),
            StartTime = new TimeOnly(11, 15),
            EndTime = new TimeOnly(11, 45),
            Purpose = "Rescheduled consultation",
            Status = "Scheduled"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAppointmentAsync(updatedAppointment));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        var savedAppointment =
            await verificationContext.Appointments
                .SingleAsync(a => a.Id == firstAppointmentId);

        Assert.Equal(new TimeOnly(10, 0), savedAppointment.StartTime);
        Assert.Equal(new TimeOnly(10, 30), savedAppointment.EndTime);
        Assert.Null(savedAppointment.UpdatedAtUtc);
    }




    [Fact]
    public async Task AddAsync_WhenLegacyAppointmentHasMissingEndTime_UsesConfiguredDurationForConflictDetection()
    {
        await using var testDatabase =
            await TestDatabase.CreateAsync();

        await CreateServiceAsync(testDatabase, "45");

        int patientId =
            await CreatePatientAsync(testDatabase);

        int staffId;

        await using (var dbContext =
            await testDatabase.Factory.CreateDbContextAsync())
        {
            staffId = await dbContext.StaffMembers
                .Select(staff => staff.Id)
                .SingleAsync();

            dbContext.Appointments.Add(CreateAppointment(
                patientId,
                new TimeOnly(10, 0),
                endTime: null));

            await dbContext.SaveChangesAsync();
        }

        var repository =
            new AppointmentRepository(testDatabase.Factory);

        var appointment = CreateAppointment(
            patientId,
            new TimeOnly(10, 30),
            new TimeOnly(11, 0));

        appointment.CreatedAtUtc = DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            StaffId = staffId,
            Username = "appointment-test-user",
            Action = "Created",
            EntityType = "Appointment",
            Details = "Appointment scheduled."
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddAsync(
                appointment,
                auditLog,
                defaultAppointmentDurationMinutes: 45));

        await using var verificationContext =
            await testDatabase.Factory.CreateDbContextAsync();

        Assert.Single(
            await verificationContext.Appointments.ToListAsync());

        Assert.Empty(
            await verificationContext.AuditLogs
                .Where(audit => audit.EntityType == "Appointment")
                .ToListAsync());
    }






}
