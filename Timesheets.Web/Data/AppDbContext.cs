using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Models;

namespace Timesheets.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();
    public DbSet<AppNotification> AppNotifications => Set<AppNotification>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

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

            entity.HasOne(e => e.Responsible)
                .WithMany(e => e.ManagedEmployees)
                .HasForeignKey(e => e.ResponsibleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.ClientName).HasMaxLength(120);
            entity.Property(e => e.ColorHex).HasMaxLength(7);
            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasMany(e => e.ResponsibleEmployees)
                .WithMany(e => e.ManagedProjects)
                .UsingEntity(join => join.ToTable("ProjectResponsibles"));
        });

        modelBuilder.Entity<TimesheetEntry>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(280);
            entity.Property(e => e.Hours).HasPrecision(5, 2);
            entity.Property(e => e.WorkDate).HasColumnType("date");
            entity.Property(e => e.EntryType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.TimesheetEntries)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApprovedByEmployee)
                .WithMany()
                .HasForeignKey(e => e.ApprovedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Project)
                .WithMany(e => e.TimesheetEntries)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.WorkDate, e.EmployeeId });
            entity.HasIndex(e => new { e.ProjectId, e.WorkDate });
            entity.HasIndex(e => new { e.EmployeeId, e.ApprovalStatus });
        });

        modelBuilder.Entity<AppNotification>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(e => e.Title).HasMaxLength(140);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(300);
            entity.Property(e => e.EmailError).HasMaxLength(500);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Notifications)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.EmployeeId, e.ReadAt, e.CreatedAt });
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.Property(e => e.TokenHash).HasMaxLength(128);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.PasswordResetTokens)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedByEmployee)
                .WithMany()
                .HasForeignKey(e => e.CreatedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.EmployeeId, e.ExpiresAt, e.UsedAt });
        });
    }
}
