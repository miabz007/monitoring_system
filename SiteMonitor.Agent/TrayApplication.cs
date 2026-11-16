
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SiteMonitor.Agent;

public class TrayApplication : ApplicationContext
{
    private readonly IHost _host;
    private readonly NotifyIcon _trayIcon;

    public TrayApplication(IHost host)
    {
        _host = host;

        var menu = new ContextMenuStrip();

        menu.Items.Add("Clock In", null, async (_, _) =>
    await SendBreakEvent("CLOCK_IN"));

        menu.Items.Add("Clock Out", null, async (_, _) =>
            await SendBreakEvent("CLOCK_OUT"));

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Start Break", null, async (_, _) =>
            await SendBreakEvent("BREAK_START"));

        menu.Items.Add("End Break", null, async (_, _) =>
            await SendBreakEvent("BREAK_END"));

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            ExitThread();
        });

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Employee Monitoring",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => menu.Show(
            Cursor.Position);

        _ = _host.StartAsync();
    }

    private async Task SendBreakEvent(string eventType)
    {
        try
        {
            var worker = _host.Services
                .GetRequiredService<Worker>();

            await worker.SendEventAsync(eventType);

            string message = eventType switch
            {
                "CLOCK_IN" => "Clocked in successfully.",
                "CLOCK_OUT" => "Clocked out successfully.",
                "BREAK_START" => "Break started.",
                "BREAK_END" => "Break ended.",
                _ => "Action completed."
            };

            _trayIcon.ShowBalloonTip(
                3000,
                "Employee Monitoring",
                message,
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(
                3000,
                "Monitoring Error",
                ex.Message,
                ToolTipIcon.Error);
        }
    }
    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();

        base.ExitThreadCore();
    }
}

