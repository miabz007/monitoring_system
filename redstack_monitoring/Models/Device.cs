namespace redstack_monitoring.Models;

public class Device
{
    public int Id { get; set; }

    public string DeviceName { get; set; } = "";

    public string EmployeeName { get; set; } = "";

    public string ApiKeyHash { get; set; } = "";

    public string CurrentStatus { get; set; } = "OFFLINE";

    public DateTime? LastHeartbeat { get; set; }

    public DateTime? LastActivity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}