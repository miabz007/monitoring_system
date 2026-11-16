
using System.Net.Http.Json;

namespace SiteMonitor.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    private bool _isIdle = false;
    private volatile bool _isOnBreak = false;
    private volatile bool _isClockedIn = false;

    private SessionMonitor? _sessionMonitor;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var serverUrl = _configuration["Monitoring:ServerUrl"];
        var deviceId = _configuration.GetValue<int>(
            "Monitoring:DeviceId");

        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new InvalidOperationException("ServerUrl is missing.");

        _logger.LogInformation("Monitoring agent started.");

        _sessionMonitor = new SessionMonitor();

        _sessionMonitor.SessionChanged += async eventType =>
        {
            try

            {

                if (eventType == "LOGIN" || eventType == "LOGOUT")
                    return;
                await SendEvent(
                    serverUrl,
                    deviceId,
                    eventType,
                    stoppingToken);

                _logger.LogInformation(
                    "Windows session event: {EventType}",
                    eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session event error.");
            }
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Do not run idle detection during a break.
                if (_isClockedIn && !_isOnBreak)
                {
                    double idleSeconds =
                        IdleDetector.GetIdleSeconds();

                    if (idleSeconds >= 300 && !_isIdle)
                    {
                        await SendEvent(
                            serverUrl,
                            deviceId,
                            "IDLE",
                            stoppingToken);

                        _isIdle = true;

                        _logger.LogInformation(
                            "Computer is now IDLE.");
                    }
                    else if (idleSeconds < 300 && _isIdle)
                    {
                        await SendEvent(
                            serverUrl,
                            deviceId,
                            "ACTIVE",
                            stoppingToken);

                        _isIdle = false;

                        _logger.LogInformation(
                            "Computer is ACTIVE again.");
                    }
                }

                // Heartbeat continues even during breaks.
                await SendEvent(
                    serverUrl,
                    deviceId,
                    "HEARTBEAT",
                    stoppingToken);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Monitoring error.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(10),
                stoppingToken);
        }
    }

    // Called by the system tray buttons.
    public async Task SendEventAsync(string eventType)
    {
        var serverUrl = _configuration["Monitoring:ServerUrl"];
        var deviceId = _configuration.GetValue<int>(
            "Monitoring:DeviceId");

        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new InvalidOperationException("ServerUrl is missing.");

        var allowedEvents = new[]
        {
        "CLOCK_IN",
        "CLOCK_OUT",
        "BREAK_START",
        "BREAK_END"
    };

        eventType = eventType.ToUpperInvariant();

        if (!allowedEvents.Contains(eventType))
            throw new ArgumentException("Invalid event type.");

        if (eventType == "BREAK_START" && !_isClockedIn)
            throw new InvalidOperationException(
                "Please clock in before starting a break.");

        if (eventType == "BREAK_END" && !_isOnBreak)
            throw new InvalidOperationException(
                "You are not currently on a break.");

        if (eventType == "CLOCK_OUT" && _isOnBreak)
            throw new InvalidOperationException(
                "Please end your break before clocking out.");

        bool success = await SendEvent(
            serverUrl,
            deviceId,
            eventType,
            CancellationToken.None);

        if (!success)
            throw new HttpRequestException(
                $"{eventType} could not be sent.");

        switch (eventType)
        {
            case "CLOCK_IN":
                _isClockedIn = true;
                _isIdle = false;
                break;

            case "CLOCK_OUT":
                _isClockedIn = false;
                _isOnBreak = false;
                _isIdle = false;
                break;

            case "BREAK_START":
                _isOnBreak = true;
                break;

            case "BREAK_END":
                _isOnBreak = false;
                _isIdle = false;
                break;
        }

        _logger.LogInformation(
            "Employee action completed: {EventType}",
            eventType);
    }

    private async Task<bool> SendEvent(
        string serverUrl,
        int deviceId,
        string eventType,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{serverUrl}/api/events",
            new
            {
                deviceId,
                eventType,
                eventTime = DateTime.UtcNow
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "{EventType} failed with status {StatusCode}",
                eventType,
                response.StatusCode);

            return false;
        }

        if (eventType != "HEARTBEAT")
        {
            _logger.LogInformation("{EventType} sent.", eventType);
        }

        return true;
    }

    public override void Dispose()
    {
        _sessionMonitor?.Dispose();
        base.Dispose();
    }
}
