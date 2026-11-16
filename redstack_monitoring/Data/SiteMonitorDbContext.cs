using Microsoft.EntityFrameworkCore;
using redstack_monitoring.Models;

namespace redstack_monitoring.Data;

public class SiteMonitorDbContext : DbContext
{
    public SiteMonitorDbContext(
        DbContextOptions<SiteMonitorDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<MonitoringEvent> MonitoringEvents
        => Set<MonitoringEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.DeviceName)
            .IsUnique();

        modelBuilder.Entity<MonitoringEvent>()
            .HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}