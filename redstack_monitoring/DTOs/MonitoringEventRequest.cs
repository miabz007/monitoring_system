
namespace redstack_monitoring.DTOs;

public class MonitoringEventRequest
{
    public int DeviceId { get; set; }

    public string EventType { get; set; } = "";

    public DateTime EventTime { get; set; } = DateTime.UtcNow;
}
