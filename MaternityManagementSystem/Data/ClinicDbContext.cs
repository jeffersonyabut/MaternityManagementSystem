using System.IO;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;


namespace MaternityManagementSystem.Data;

public class ClinicDbContext : DbContext
{
    public DbSet<Staff> StaffMembers => Set<Staff>();

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
    }
}
