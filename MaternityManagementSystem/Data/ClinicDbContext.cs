using System.IO;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;


namespace MaternityManagementSystem.Data;

public class ClinicDbContext : DbContext
{
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Pregnancy> Pregnancies => Set<Pregnancy>();
    public DbSet<PrenatalVisit> PrenatalVisits => Set<PrenatalVisit>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<RecordVersion> RecordVersions => Set<RecordVersion>();

    public ClinicDbContext()
    {
    }

    public ClinicDbContext(
        DbContextOptions<ClinicDbContext> options)
        : base(options)
    {
    }



    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Golddie");

            Directory.CreateDirectory(appDataFolder);

            string databasePath = Path.Combine(
                appDataFolder,
                "clinic.db");

            optionsBuilder.UseSqlite(
                $"Data Source={databasePath}");
        }
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(staff => staff.Id);

            entity.HasIndex(staff => staff.Username)
                  .IsUnique();

            entity.Property(staff => staff.Username)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(staff => staff.PasswordHash)
                  .IsRequired();

            entity.Property(staff => staff.Role)
                  .IsRequired()
                  .HasMaxLength(50);
        });


        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(patient => patient.Id);

            entity.HasIndex(patient => patient.PatientNumber)
                  .IsUnique();

            entity.Property(patient => patient.PatientNumber)
                  .IsRequired()
                  .HasMaxLength(30);

            entity.Property(patient => patient.FirstName)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(patient => patient.MiddleName)
                  .IsRequired(false)
                  .HasMaxLength(100);

            entity.Property(patient => patient.LastName)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(patient => patient.Sex)
                  .HasMaxLength(30);

            entity.Property(patient => patient.CivilStatus)
                  .HasMaxLength(30);

            entity.Property(patient => patient.ContactNumber)
                  .HasMaxLength(30);

            entity.Property(patient => patient.Email)
                  .HasMaxLength(254);

            entity.Property(patient => patient.Address)
                  .HasMaxLength(500);

            entity.Property(patient => patient.EmergencyContactName)
                  .HasMaxLength(200);

            entity.Property(patient => patient.EmergencyContactNumber)
                  .HasMaxLength(30);

            entity.Property(patient => patient.CreatedAtUtc)
                  .IsRequired();

            entity.Property(patient => patient.UpdatedAtUtc)
                  .IsRequired(false);
        });

        modelBuilder.Entity<Pregnancy>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(p => p.Notes)
                .HasMaxLength(2000);

            entity.HasOne(p => p.Patient)
                .WithMany()
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.PatientId);
        });

        modelBuilder.Entity<PrenatalVisit>(entity =>
        {
            entity.HasKey(pv => pv.Id);

            entity.Property(pv => pv.Notes)
                .HasMaxLength(2000);

            entity.HasOne(pv => pv.Pregnancy)
                .WithMany()
                .HasForeignKey(pv => pv.PregnancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(pv => pv.PregnancyId);
        });

        modelBuilder.Entity<VitalSign>(entity =>
        {
            entity.HasKey(vs => vs.Id);

            entity.Property(vs => vs.TemperatureCelsius)
                .HasPrecision(5, 2);

            entity.Property(vs => vs.OxygenSaturation)
                .HasPrecision(5, 2);

            entity.Property(vs => vs.WeightKg)
                .HasPrecision(6, 2);

            entity.Property(vs => vs.Notes)
                .HasMaxLength(2000);

            entity.HasOne(vs => vs.PrenatalVisit)
                .WithMany(pv => pv.VitalSigns)
                .HasForeignKey(vs => vs.PrenatalVisitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(vs => vs.PrenatalVisitId);

            entity.Property(vs => vs.RecordedAtUtc)
                .IsRequired();

            entity.Property(vs => vs.CreatedAtUtc)
                .IsRequired();

            entity.Property(vs => vs.UpdatedAtUtc)
                .IsRequired(false);

            entity.Property(vs => vs.IsArchived)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(vs => vs.ArchivedAtUtc)
                .IsRequired(false);

            entity.Property(vs => vs.ArchiveReason)
                .HasMaxLength(1000)
                .IsRequired(false);
        });


        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Username)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(a => a.Action)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(a => a.EntityType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(a => a.Details)
                .HasMaxLength(2000)
                .IsRequired(false);

            entity.Property(a => a.Reason)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(a => a.OccurredAtUtc)
                .IsRequired();

            entity.HasIndex(a => a.OccurredAtUtc);

            entity.HasIndex(a => new
            {
                a.EntityType,
                a.EntityId
            });

            entity.HasIndex(a => a.StaffId);
        });



        modelBuilder.Entity<RecordVersion>(entity =>
        {
            entity.HasKey(version => version.Id);

            entity.Property(version => version.EntityType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(version => version.EntityId)
                .IsRequired();

            entity.Property(version => version.PreviousValuesJson)
                .IsRequired();

            entity.Property(version => version.NewValuesJson)
                .IsRequired();

            entity.Property(version => version.Username)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(version => version.ChangedAtUtc)
                .IsRequired();

            entity.HasIndex(version => new
            {
                version.EntityType,
                version.EntityId,
                version.ChangedAtUtc
            });

            entity.HasIndex(version => version.StaffId);
        });




    }
}
