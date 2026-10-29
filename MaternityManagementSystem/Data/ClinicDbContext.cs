using System.IO;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;


namespace MaternityManagementSystem.Data;

public class ClinicDbContext : DbContext
{
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Pregnancy> Pregnancies => Set<Pregnancy>();

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

    }
}
