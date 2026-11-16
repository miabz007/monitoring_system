# Onsite Employee PC Monitoring System

## 1. Project Overview

The Onsite Employee PC Monitoring System is a lightweight employee activity monitoring solution designed for small construction teams operating across multiple project sites.

The system allows the office to monitor the working status of employees using their onsite computers through a central dashboard.

It is designed to provide basic attendance and computer activity visibility without invasive monitoring.

### Key Objectives

- Monitor employee clock-in and clock-out times.
- Detect when an employee's computer becomes idle.
- Detect when an employee resumes computer activity.
- Monitor computer lock and unlock events.
- Track employee breaks.
- Detect whether onsite computers are online or offline.
- Provide a central dashboard for office monitoring.
- Maintain an activity history in PostgreSQL.

**Privacy principle:** The system does not capture screenshots, record keystrokes, or monitor the content of employees' work.

---

## 2. System Architecture

The system uses a centralised architecture.

### Office PC (Central Server)

The office PC hosts:

- ASP.NET Core Web API
- PostgreSQL database
- Employee monitoring dashboard
- Offline detection service

### Onsite PCs (Monitoring Agents)

Each onsite PC runs a lightweight Windows monitoring agent.

The agent communicates with the office API through Tailscale.

### Architecture Diagram


             ONSITE CONSTRUCTION SITES

   +-------------------+   +-------------------+
   |    SITE-PC-01     |   |    SITE-PC-02     |
   |                   |   |                   |
   | Monitoring Agent  |   | Monitoring Agent  |
   +---------+---------+   +---------+---------+
             |                       |
             +-----------+-----------+
                         |
                    TAILSCALE
                Private Network
                         |
             +-----------v-----------+
             |       OFFICE PC       |
             |                       |
             |   ASP.NET Core API   |
             |                       |
             |   PostgreSQL 18      |
             |                       |
             |   Monitoring         |
             |   Dashboard          |
             +-----------------------+
                         |
                  Office Browser


The onsite agents communicate with the API. They do not connect directly to PostgreSQL.

---

## 3. Technology Stack

| Component | Technology |
|---|---|
| Backend API | ASP.NET Core |
| Programming Language | C# |
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| Database Access | Entity Framework Core |
| API Documentation | Swagger / OpenAPI |
| Frontend Dashboard | HTML, CSS, JavaScript |
| Monitoring Agent | .NET Worker Service |
| Windows Tray Interface | Windows Forms |
| Network Connectivity | Tailscale |
| Development Environment | Visual Studio |

---

## 4. Project Structure

The solution contains two main projects.

### A. company_monitoring

The central API and dashboard project.

Responsibilities:

- Receive monitoring events.
- Register onsite computers.
- Update device statuses.
- Store employee activity records.
- Provide device and activity information to the dashboard.
- Detect offline computers.
- Serve the web dashboard.

Important files:


company_monitoring/
│
├── Models/
│   ├── Device.cs
│   └── MonitoringEvent.cs
│
├── Data/
│   └── SiteMonitorDbContext.cs
│
├── Services/
│   └── OfflineDetectionService.cs
│
├── wwwroot/
│   └── index.html
│
├── Program.cs
├── appsettings.json
└── company_monitoring.csproj


### B. SiteMonitor.Agent

The lightweight monitoring application installed on each onsite PC.

Responsibilities:

- Detect Windows user activity.
- Detect idle periods.
- Detect Windows lock and unlock events.
- Send monitoring events to the office API.
- Send periodic heartbeats.
- Provide employee clock-in, clock-out and break controls through the Windows system tray.

Important files:


SiteMonitor.Agent/
│
├── Program.cs
├── Worker.cs
├── IdleDetector.cs
├── SessionMonitor.cs
├── TrayApplication.cs
├── appsettings.json
└── SiteMonitor.Agent.csproj


---

## 5. Monitoring Features

### 5.1 Employee Clock In

Employees use the system tray menu to select **Clock In**.

The API records a `CLOCK_IN` event and updates the computer status to `ONLINE`.

### 5.2 Employee Clock Out

Employees select **Clock Out** from the system tray menu.

The API records a `CLOCK_OUT` event and updates the computer status to `OFFLINE`.

Employees must end their break before clocking out.

### 5.3 Idle Detection

The monitoring agent uses Windows `GetLastInputInfo` to determine the time since the last keyboard or mouse input.

Current configuration:

- Idle threshold: 5 minutes (300 seconds).
- When the threshold is reached, an `IDLE` event is sent.
- When activity resumes, an `ACTIVE` event is sent.

Idle detection operates while the employee is clocked in and not on a break.

### 5.4 Windows Lock and Unlock

The monitoring agent detects Windows session lock and unlock events.

Events:

- `LOCK`
- `UNLOCK`

Lock and unlock events are recorded in the activity history.

The API preserves `OFFLINE` and `ON BREAK` statuses when these events occur.

### 5.5 Break Management

Employees can manage breaks using the system tray.

Available actions:

- Start Break
- End Break

Events:

- `BREAK_START`
- `BREAK_END`

The dashboard displays `ON BREAK` when an employee starts a break.

### 5.6 Heartbeat Monitoring

Each monitoring agent sends a heartbeat to the API every 10 seconds.

Heartbeats are used to determine whether an onsite computer is still connected.

Important:

- Heartbeats update the `LastHeartbeat` field in the `Devices` table.
- Heartbeats do not need to be stored as activity events in `MonitoringEvents`.
- The API restores the appropriate status using the latest recorded employee event.
- A device is considered offline when its last heartbeat is older than 2 minutes.

### 5.7 Offline Detection

The office API runs an offline detection background service.

Current configuration:

| Setting | Value |
|---|---|
| Heartbeat interval | 10 seconds |
| Offline threshold | 2 minutes |
| Offline detection interval | 30 seconds |

When a computer stops communicating, its status changes to `OFFLINE`.

---

## 6. Device Statuses

The dashboard currently uses the following statuses:

| Status | Description |
|---|---|
| ONLINE | Employee is clocked in and computer is active |
| IDLE | No keyboard or mouse activity for at least 5 minutes |
| LOCKED | Windows session is locked |
| ON BREAK | Employee has started a break |
| OFFLINE | Computer is disconnected, clocked out, or not communicating |

---

## 7. Database Structure

Database name:


site_monitoring


Database user:


site_monitor
company

### Devices Table

Stores registered onsite computers and their current monitoring information.

| Column | Description |
|---|---|
| Id | Unique device ID |
| DeviceName | Computer name |
| EmployeeName | Assigned employee |
| ApiKeyHash | Reserved for device API authentication |
| CurrentStatus | Current monitoring status |
| LastHeartbeat | Most recent heartbeat timestamp |
| LastActivity | Most recent recorded activity timestamp |
| CreatedAt | Device registration timestamp |

**Note:** `ApiKeyHash` is currently reserved for future device authentication. API-key authentication has not yet been implemented.

### MonitoringEvents Table

Stores employee activity records.

| Column | Description |
|---|---|
| Id | Unique event ID |
| DeviceId | Associated device |
| EventType | Type of monitoring event |
| EventTime | Event timestamp in UTC |

Supported event types include:

companytext
LOGIN
LOGOUT
CLOCK_IN
CLOCK_OUT
IDLE
ACTIVE
LOCK
UNLOCK
BREAK_START
BREAK_END
company

`HEARTBEAT` is processed separately and should not create a record in this table.

---

## 8. API Endpoints

The main API endpoints are:

| Method | Endpoint | Purpose |
|---|---|---|
| GET | /health/db | Check database connectivity |
| POST | /api/devices/register | Register a computer |
| GET | /api/devices | Retrieve registered computers |
| POST | /api/events | Receive monitoring events |
| GET | /api/events | Retrieve employee activity history |

### Swagger Documentation

Swagger is available at:

companytext
http://localhost:5080/swagger
company

### Dashboard

The monitoring dashboard is available at:

companytext
http://localhost:5080/
company

The dashboard automatically refreshes its information every 10 seconds.

---

## 9. Installation and Configuration

### 9.1 Office PC Requirements

Install:

1. Windows 10/11.
2. PostgreSQL 18.
3. .NET 10 runtime or deployment prerequisites.
4. Tailscale.
5. The `company_monitoring` API application.

The office PC must remain powered on and connected to the network.

### 9.2 Database Setup

Create the PostgreSQL database:

companysql
CREATE DATABASE site_monitoring;
company

Configure the database connection string in the API project's `appsettings.json`.

Apply Entity Framework Core migrations:

companypowershell
dotnet ef database update
company

### 9.3 Running the API

From the API project directory:

companypowershell
dotnet run --urls "http://0.0.0.0:5080"
company

The API will listen on port 5080.

### 9.4 Onsite Agent Configuration

Each onsite PC requires its own monitoring agent configuration.

Example `appsettings.json`:

companyjson
{
  "Monitoring": {
    "ServerUrl": "http://OFFICE-TAILSCALE-IP:5080",
    "DeviceId": 1,
    "HeartbeatIntervalSeconds": 30
  }
}
company

Replace:

- `OFFICE-TAILSCALE-IP` with the office PC's Tailscale IP.
- `DeviceId` with the correct registered device ID.

**Note:** The current Worker implementation sends heartbeats every 10 seconds in its code. The `HeartbeatIntervalSeconds` setting is not currently used.

Each onsite PC must have the correct device ID.

### 9.5 Publishing the Agent

The agent can be published as a self-contained Windows application:

companypowershell
dotnet publish -c Release -r win-x64 --self-contained true
company

Copy the published application folder to the onsite PC.

The agent currently runs as an interactive Windows system tray application.

---

## 10. Network and Security

The system uses Tailscale for private connectivity between onsite computers and the office PC.

Security recommendations:

- Do not expose the API directly to the public internet.
- Do not configure public port forwarding.
- Restrict Tailscale network membership to authorised people and devices.
- Restrict access to the API port using Tailscale ACLs or grants and Windows Firewall.
- Keep PostgreSQL accessible only locally on the office PC.
- Back up the PostgreSQL database regularly.
- Use HTTPS and device API-key authentication before wider deployment.

### Current Security Status

| Security feature | Status |
|---|---|
| Tailscale private network | Implemented |
| Windows Firewall restriction | Configured for private access |
| PostgreSQL local access | Intended configuration |
| Device API-key authentication | Pending |
| Dashboard authentication | Pending |
| HTTPS | Pending |
| Automated database backups | To be configured |

The current API uses HTTP within the private Tailscale network. It should not be exposed to untrusted networks.

---

## 11. Troubleshooting

### Computer Shows OFFLINE

Check:

1. Is the onsite monitoring agent running?
2. Is the office API running?
3. Is Tailscale connected on both computers?
4. Is the configured office Tailscale IP correct?
5. Is TCP port 5080 accessible?
6. Is the device ID correct?

Check the device information using:

companytext
GET /api/devices
company

Review the `LastHeartbeat` value.

### Heartbeat Not Received

Verify that the agent can access:

companytext
http://OFFICE-TAILSCALE-IP:5080/health/db
company

### Dashboard Does Not Update

Check whether the API is running and whether the browser can access:

companytext
http://localhost:5080/
company

### Employee Status Incorrect

Check the latest events in the `MonitoringEvents` table.

companysql
SELECT *
FROM "MonitoringEvents"
ORDER BY "EventTime" DESC
LIMIT 50;
company

---

## 12. Future Improvements

Potential improvements include:

- Device-specific API-key authentication.
- Dashboard administrator login.
- HTTPS support.
- Employee attendance reports.
- Daily and monthly working-hour summaries.
- Break duration reports.
- Last clock-in and clock-out display.
- Automatic database backups.
- Windows service or managed startup deployment.
- Better recovery after agent or computer restart.
- Employee and device management interface.

---

## 13. Project Design Principles

The system is designed around the following principles:

1. Lightweight monitoring with minimal resource consumption.
2. Centralised data storage and reporting.
3. Private network communication through Tailscale.
4. No screenshots or keystroke recording.
5. Clear employee attendance and activity records.
6. Simple operation for small construction teams.
7. Security and privacy as deployment priorities.

---

**Project:** Onsite Employee PC Monitoring System  
**Backend:** `company_monitoring`  
**Monitoring Agent:** `SiteMonitor.Agent`  
**Database:** PostgreSQL 18  
**Framework:** .NET 10  
**Network:** Tailscale  
**API Port:** 5080
