
using Microsoft.Win32;

namespace SiteMonitor.Agent;

public class SessionMonitor : IDisposable
{
    public event Action<string>? SessionChanged;

    public SessionMonitor()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void OnSessionSwitch(
        object sender,
        SessionSwitchEventArgs e)
    {
        string? eventType = e.Reason switch
        {
            SessionSwitchReason.SessionLock => "LOCK",
            SessionSwitchReason.SessionUnlock => "UNLOCK",
            SessionSwitchReason.SessionLogon => "LOGIN",
            SessionSwitchReason.SessionLogoff => "LOGOUT",
            _ => null
        };

        if (eventType != null)
            SessionChanged?.Invoke(eventType);
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
