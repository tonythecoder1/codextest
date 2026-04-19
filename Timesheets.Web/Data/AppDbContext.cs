using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Models;

namespace Timesheets.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(120);
            entity.Property(e => e.Email).HasMaxLength(160);
            entity.Property(e => e.JobTitle).HasMaxLength(120);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.ClientName).HasMaxLength(120);
            entity.Property(e => e.ColorHex).HasMaxLength(7);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<TimesheetEntry>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(280);
            entity.Property(e => e.Hours).HasPrecision(5, 2);
            entity.Property(e => e.WorkDate).HasColumnType("date");

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.TimesheetEntries)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Project)
                .WithMany(e => e.TimesheetEntries)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.WorkDate, e.EmployeeId });
            entity.HasIndex(e => new { e.ProjectId, e.WorkDate });
        });
    }
}
