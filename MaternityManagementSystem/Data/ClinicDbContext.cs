using System.IO;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;


namespace MaternityManagementSystem.Data;

public class ClinicDbContext : DbContext
{
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<Patient> Patients => Set<Patient>();



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

    }
}
