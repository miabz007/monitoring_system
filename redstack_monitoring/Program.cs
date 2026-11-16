using Microsoft.EntityFrameworkCore;
using redstack_monitoring.Data;
using redstack_monitoring.DTOs;
using redstack_monitoring.Models;
using Microsoft.EntityFrameworkCore;
using redstack_monitoring.Services;
using Microsoft.Extensions.Logging;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string is missing.");

builder.Services.AddDbContext<SiteMonitorDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"))
    .LogTo(
        Console.WriteLine,
        LogLevel.Warning));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<OfflineDetectionService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/db", async (
    SiteMonitorDbContext db) =>
{
    var connected = await db.Database.CanConnectAsync();

    return connected
        ? Results.Ok(new { status = "Database connected" })
        : Results.Problem("Database connection failed.");
});


app.MapPost("/api/devices/register",
    async (
        RegisterDeviceRequest request,
        SiteMonitorDbContext db) =>
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName) ||
            string.IsNullOrWhiteSpace(request.EmployeeName))
        {
            return Results.BadRequest("Device name and employee name are required.");
        }

        var existingDevice = await db.Devices
            .FirstOrDefaultAsync(d => d.DeviceName == request.DeviceName);

        if (existingDevice != null)
        {
            return Results.Conflict("This device is already registered.");
        }

        var device = new Device
        {
            DeviceName = request.DeviceName.Trim(),
            EmployeeName = request.EmployeeName.Trim(),
            ApiKeyHash = "",
            CurrentStatus = "OFFLINE",
            CreatedAt = DateTime.UtcNow
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Device registered successfully",
            device.Id,
            device.DeviceName,
            device.EmployeeName
        });
    });



app.MapPost("/api/events",
    async (
        MonitoringEventRequest request,
        SiteMonitorDbContext db) =>
    {
        var allowedEvents = new[]
        {
            "LOGIN", "IDLE", "ACTIVE", "LOGOUT",
            "LOCK", "UNLOCK", "HEARTBEAT",
            "BREAK_START", "BREAK_END",
            "CLOCK_IN", "CLOCK_OUT"
        };

        var eventType = request.EventType.ToUpperInvariant();

        if (!allowedEvents.Contains(eventType))
        {
            return Results.BadRequest("Invalid event type.");
        }

        var device = await db.Devices.FindAsync(request.DeviceId);

        if (device == null)
        {
            return Results.NotFound("Device not found.");
        }

        var eventTime = request.EventTime == default
            ? DateTime.UtcNow
            : request.EventTime.ToUniversalTime();

        // HEARTBEAT updates connectivity only.
        // It does not create a MonitoringEvents record.
        if (eventType == "HEARTBEAT")
        {
            device.LastHeartbeat = eventTime;

            var lastEvent = await db.MonitoringEvents
                .Where(e => e.DeviceId == device.Id)
                .OrderByDescending(e => e.EventTime)
                .FirstOrDefaultAsync();

            if (lastEvent != null)
            {
                switch (lastEvent.EventType)
                {
                    case "CLOCK_IN":
                    case "LOGIN":
                    case "ACTIVE":
                    case "UNLOCK":
                        device.CurrentStatus = "ONLINE";
                        break;

                    case "IDLE":
                        device.CurrentStatus = "IDLE";
                        break;

                    case "LOCK":
                        device.CurrentStatus = "LOCKED";
                        break;

                    case "BREAK_START":
                        device.CurrentStatus = "ON BREAK";
                        break;

                    case "CLOCK_OUT":
                    case "LOGOUT":
                        device.CurrentStatus = "OFFLINE";
                        break;
                }
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Heartbeat received.",
                device.DeviceName,
                eventType,
                device.CurrentStatus
            });
        }

        // Record meaningful employee activity.
        var monitoringEvent = new MonitoringEvent
        {
            DeviceId = request.DeviceId,
            EventType = eventType,
            EventTime = eventTime
        };

        db.MonitoringEvents.Add(monitoringEvent);

        switch (eventType)
        {
            case "LOGIN":
            case "CLOCK_IN":
            case "ACTIVE":
            case "UNLOCK":
                if (device.CurrentStatus == "LOCKED")
                {
                    device.CurrentStatus = "ONLINE";
                }
                break;
            case "BREAK_END":
                device.CurrentStatus = "ONLINE";
                device.LastActivity = eventTime;
                break;

            case "CLOCK_OUT":
            case "LOGOUT":
                device.CurrentStatus = "OFFLINE";
                break;

            case "IDLE":
                device.CurrentStatus = "IDLE";
                break;

            case "LOCK":
                if (device.CurrentStatus == "ONLINE" ||
                    device.CurrentStatus == "IDLE")
                {
                    device.CurrentStatus = "LOCKED";
                }
                break;


            case "BREAK_START":
                device.CurrentStatus = "ON BREAK";
                break;
        }

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Event received successfully",
            device.DeviceName,
            eventType,
            device.CurrentStatus
        });
    });



app.MapGet("/api/events", async (
    int? deviceId,
    DateTimeOffset? startUtc,
    DateTimeOffset? endUtc,
    SiteMonitorDbContext db) =>
{
    var query = db.MonitoringEvents
        .AsNoTracking()
        .Where(e => e.EventType != "HEARTBEAT")
        .AsQueryable();

    if (deviceId.HasValue)
    {
        query = query.Where(
            e => e.DeviceId == deviceId.Value);
    }

    if (startUtc.HasValue)
    {
        var start = startUtc.Value.UtcDateTime;
        query = query.Where(e => e.EventTime >= start);
    }

    if (endUtc.HasValue)
    {
        var end = endUtc.Value.UtcDateTime;
        query = query.Where(e => e.EventTime < end);
    }


    var events = await query
        .OrderByDescending(e => e.EventTime)
        .Take(500)
        .Select(e => new
        {
            e.Id,
            e.DeviceId,
            DeviceName = e.Device!.DeviceName,
            EmployeeName = e.Device.EmployeeName,
            e.EventType,
            e.EventTime
        })
        .ToListAsync();

    return Results.Ok(events);
});



app.MapGet("/api/devices",
    async (SiteMonitorDbContext db) =>
    {
        var devices = await db.Devices
            .OrderBy(d => d.DeviceName)
            .Select(d => new
            {
                d.Id,
                d.DeviceName,
                d.EmployeeName,
                d.CurrentStatus,
                d.LastHeartbeat,
                d.LastActivity
            })
            .ToListAsync();

        return Results.Ok(devices);
    });


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//p.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
