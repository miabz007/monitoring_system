namespace redstack_monitoring.Models;


public class MonitoringEvent
{
    public long Id { get; set; }

    public int DeviceId { get; set; }

    public string EventType { get; set; } = "";

    public DateTime EventTime { get; set; } = DateTime.UtcNow;

    public Device? Device { get; set; }
}