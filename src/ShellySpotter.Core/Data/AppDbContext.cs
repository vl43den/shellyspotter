using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Models;

namespace ShellySpotter.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<PingTarget> PingTargets => Set<PingTarget>();
    public DbSet<PingResult> PingResults => Set<PingResult>();
    public DbSet<MaintenanceWindow> MaintenanceWindows => Set<MaintenanceWindow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).IsRequired().HasMaxLength(100);
            e.Property(r => r.OwnerId).IsRequired().HasMaxLength(100);
            e.Property(r => r.HighTemperatureThreshold).HasDefaultValue(28.0);
        });

        modelBuilder.Entity<SensorReading>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Room).WithMany(room => room.SensorReadings).HasForeignKey(r => r.RoomId);
            e.Property(r => r.Timestamp).IsRequired();
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasOne(a => a.Room).WithMany(r => r.Alerts).HasForeignKey(a => a.RoomId);
            e.Property(a => a.Type).IsRequired().HasMaxLength(50);
            e.Property(a => a.Message).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<PingTarget>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Room).WithMany(r => r.PingTargets).HasForeignKey(p => p.RoomId);
            e.Property(p => p.IpAddress).IsRequired().HasMaxLength(45);
        });

        modelBuilder.Entity<PingResult>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.PingTarget).WithMany(t => t.PingResults).HasForeignKey(p => p.PingTargetId);
        });

        modelBuilder.Entity<MaintenanceWindow>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasOne(m => m.Room).WithMany(r => r.MaintenanceWindows).HasForeignKey(m => m.RoomId);
        });

        // Seed initial room
        modelBuilder.Entity<Room>().HasData(new Room
        {
            Id = 1,
            Name = "Server Room A",
            Description = "Primary server room",
            OwnerId = "customer1",
            HighTemperatureThreshold = 28.0
        });
    }
}
