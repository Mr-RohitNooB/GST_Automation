# GST Automation Suite — "Automation Cafe"

A commercial RPA (Robotic Process Automation) platform for automating GST and tax-related workflows on Indian government portals. Built with an ASP.NET Core 8 backend API and a WPF desktop client.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Project Structure](#project-structure)
3. [AutomationServer.Api (Backend)](#automationserverapi-backend)
4. [GST\_Suite\_AutomationCafe (Desktop Client)](#gst_suite_automationcafe-desktop-client)
5. [Database Schema](#database-schema)
6. [Configuration](#configuration)
7. [Data Flow](#data-flow)
8. [Setup & Running](#setup--running)
9. [Security Notes](#security-notes)

---

## Architecture Overview

```
┌──────────────────────────────────┐       HTTPS/JWT        ┌────────────────────────────────┐
│  GST_Suite_AutomationCafe        │ ─────────────────────► │  AutomationServer.Api          │
│  (WPF Desktop, .NET 8 Windows)   │ ◄───────────────────── │  (ASP.NET Core 8 Web API)      │
│                                  │   JSON / JWT Token     │                                │
│  • Login UI (MainWindow)         │                        │  • POST /api/auth/login        │
│  • Dashboard UI (DashboardWindow)│                        │  • POST /api/auth/register     │
│  • ApiService (HTTP client)      │                        │  • JWT generation              │
│  • AutomationEngine (Playwright) │                        │  • Session management          │
└──────────────────────────────────┘                        └───────────────┬────────────────┘
                                                                            │ SqlClient
                                                                            ▼
                                                            ┌────────────────────────────────┐
                                                            │  SQL Server (GST_DB)           │
                                                            │  • Users, Packages, Modules    │
                                                            │  • UserSubscriptions           │
                                                            │  • ActiveSessions              │
                                                            │  • AutomationScripts           │
                                                            └────────────────────────────────┘
```

---

## Project Structure

```
GST_Automation/
├── AutomationServer.Api/                  # ASP.NET Core 8 backend
│   ├── Controllers/
│   │   └── AuthController.cs             # /api/auth endpoints
│   ├── Models/
│   │   └── AuthModels.cs                 # Request/response models
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Program.cs                        # App startup, JWT config, DI
│   ├── appsettings.json                  # DB connection string, JWT settings
│   ├── appsettings.Development.json
│   └── AutomationServer.Api.csproj
│
├── GST_Suite_AutomationCafe/             # WPF desktop client
│   ├── Services/
│   │   ├── ApiService.cs                 # HTTP client to backend API
│   │   └── AutomationEngine.cs          # Playwright script executor
│   ├── Models/
│   │   └── AutomationModels.cs          # AutomationScript, AutomationStep, etc.
│   ├── MainWindow.xaml / .cs            # Login screen
│   ├── DashboardWindow.xaml / .cs       # Module launcher
│   ├── App.xaml / .cs
│   └── GST_Suite_AutomationCafe.csproj
│
├── master_DB.sql                         # Full database schema + seed data
├── app_setting.json                      # Root-level config (currently empty)
└── README.md
```

---

## AutomationServer.Api (Backend)

### Technology Stack

| Concern | Library |
|---|---|
| Framework | ASP.NET Core 8.0 |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0`) |
| Password hashing | BCrypt.Net-Next 4.1.0 |
| Database | SQL Server via `Microsoft.Data.SqlClient 6.1.4` |
| API docs | Swashbuckle/Swagger 6.6.2 |

### Endpoints

#### `POST /api/auth/register`

Registers a new user account.

**Request body:**
```json
{
  "email": "user@example.com",
  "password": "plaintext",
  "fullName": "John Doe"
}
```

**Logic:** Checks email uniqueness → hashes password with BCrypt → inserts into `Users` table.

---

#### `POST /api/auth/login`

Authenticates a user and returns a JWT token.

**Request body:**
```json
{
  "email": "user@example.com",
  "password": "plaintext"
}
```

**Response body:**
```json
{
  "token": "<JWT>",
  "sessionId": "<GUID>",
  "message": "Login successful."
}
```

**Logic:**
1. Fetches stored `PasswordHash` from `Users` table.
2. Verifies with BCrypt.
3. Calls `sp_UserLoginAndSessionCheck`:
   - Removes stale sessions (no heartbeat > 5 minutes).
   - Enforces a **5-concurrent-device limit** per user.
   - Inserts new session record; returns `@LoginStatus`:
     - `1` = success
     - `-1` = max sessions reached
     - `0` = invalid credentials
4. Calls `sp_GetAllowedModulesForUser` → fetches module IDs from active subscription.
5. Generates JWT (12-hour expiry) with claims:
   - `sub` — user email
   - `session_id` — new session GUID
   - `allowed_modules` — JSON array of module IDs, e.g. `[1,2]`

---

### `Program.cs` Highlights

- Configures JWT Bearer middleware with issuer/audience validation.
- Registers Swagger for dev-time API testing.
- Enforces HTTPS redirection.

---

## GST\_Suite\_AutomationCafe (Desktop Client)

### Technology Stack

| Concern | Library |
|---|---|
| UI Framework | WPF (.NET 8 Windows) |
| Browser Automation | Microsoft.Playwright 1.58.0 (Chromium) |
| JWT parsing | System.IdentityModel.Tokens.Jwt 8.16.0 |

---

### MainWindow (Login Screen)

`MainWindow.xaml / MainWindow.xaml.cs`

- Email + Password fields, "SECURE LOGIN" button.
- Calls `ApiService.LoginAsync()`.
- On success: stores JWT in `MainWindow.CurrentJwtToken` (static), opens `DashboardWindow`, closes itself.
- On failure: displays error message in red.
- Button is disabled with "AUTHENTICATING..." text during the request.

---

### DashboardWindow (Module Launcher)

`DashboardWindow.xaml / DashboardWindow.xaml.cs`

- Left sidebar dynamically builds buttons from JWT claims.
- **`LoadUserModules()`**:
  - Parses `allowed_modules` claim from JWT.
  - Maps IDs to names:

    | ID | Module Name |
    |---|---|
    | 1 | GST Portal Downloader |
    | 2 | ITR Auto-Filer |
    | 3 | LinkedIn Scraper |

- **`TriggerAutomationEngine(moduleId)`**:
  1. Calls `ApiService.GetModuleDownloadUrlAsync()` to get CDN URL.
  2. Calls `ApiService.DownloadScriptJsonAsync()` to fetch JSON script.
  3. Passes script + user credentials to `AutomationEngine.RunScriptAsync()`.
  4. Shows wait cursor during execution.

---

### ApiService

`Services/ApiService.cs` — HTTP client wrapper.

| Method | Endpoint | Notes |
|---|---|---|
| `LoginAsync(email, password)` | `POST Auth/login` | Returns `LoginResponse` |
| `GetModuleDownloadUrlAsync(moduleId, token)` | `GET Modules/download/{id}` | Requires Bearer token; planned endpoint |
| `DownloadScriptJsonAsync(cdnUrl)` | External CDN GET | Currently returns a hardcoded mock script |

**Base URL:** `https://localhost:7045/api/`

---

### AutomationEngine

`Services/AutomationEngine.cs` — Playwright-based JSON script runner.

**Method:** `RunScriptAsync(jsonContent, userInputs)`

1. Deserializes JSON to `AutomationScript`.
2. Launches **visible** Chromium browser (`Headless = false`).
3. Iterates over `Steps`, executing each action:

| Action | Behavior |
|---|---|
| `goto` | `page.GotoAsync(value)` |
| `type` | Replaces `{PLACEHOLDER}` in `value`, then `page.Locator(selector).FillAsync(value)` |
| `click` | `page.Locator(selector).ClickAsync()` |
| `wait` | `Task.Delay(milliseconds)` |

4. Leaves browser open 3 seconds after last step, then closes.

**Placeholder replacement:** `{GST_USERNAME}` and `{GST_PASSWORD}` are substituted from the `userInputs` dictionary before sending to the browser.

---

### AutomationModels

`Models/AutomationModels.cs`

```csharp
class AutomationScript
{
    string ModuleName;
    string Version;
    List<AutomationStep> Steps;
}

class AutomationStep
{
    string Action;    // "goto" | "type" | "click" | "wait"
    string? Selector; // CSS/XPath selector (for type/click)
    string? Value;    // URL, text, or ms delay
}
```

---

## Database Schema

Full schema in `master_DB.sql`.

### Tables

| Table | Purpose |
|---|---|
| `Users` | Registered user accounts (email, BCrypt hash, name) |
| `Packages` | Subscription tiers (e.g., "Tax Pro Suite") with concurrent-user limits |
| `Modules` | Automation tools (e.g., "GST Downloader", "ITR Filer") |
| `PackageModules` | Many-to-many mapping: which modules belong to which package |
| `UserSubscriptions` | User ↔ Package with start/end dates |
| `ActiveSessions` | Tracks live sessions; used to enforce 5-device limit |
| `AutomationScripts` | Stores JSON script content per module (not yet wired up) |

### Stored Procedures

#### `sp_UserLoginAndSessionCheck`

```
IN:  @Email, @PasswordHash
OUT: @NewSessionId (UNIQUEIDENTIFIER), @LoginStatus (INT)
```

- Deletes sessions with `LastHeartbeat` older than 5 minutes.
- Returns `-1` if 5 sessions already active; `1` on success; `0` on bad credentials.

#### `sp_GetAllowedModulesForUser`

```
IN:  @UserId
OUT: Result set of (ModuleId, ModuleName)
```

- Joins `UserSubscriptions → PackageModules → Modules`.
- Filters by active, non-expired subscription and `IsActive = 1` modules.

### Seed Data

- **User:** Rohit Chauhan (`rohitchauhaninfo@gmail.com`) — password hash pre-seeded.
- **Packages:** "Tax Pro Suite" (GST Downloader + ITR Filer), "Marketing Suite" (LinkedIn Scraper).
- **Subscription:** Rohit → Tax Pro Suite, 1-year validity.

---

## Configuration

### `AutomationServer.Api/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=52.66.105.234;Database=GST_DB;User Id=sa;Password=...;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "SecretKey": "...",
    "Issuer": "AutomationServerAPI",
    "Audience": "WpfDesktopClient"
  }
}
```

> **Important:** Move credentials and JWT secret to Azure Key Vault or environment variables before deploying to production.

### `GST_Suite_AutomationCafe/Services/ApiService.cs`

Base URL is hardcoded as `https://localhost:7045/api/`. Update this for staging/production deployments.

---

## Data Flow

```
User enters credentials
       │
       ▼
MainWindow → ApiService.LoginAsync()
       │
       ▼  POST /api/auth/login
AuthController.Login()
  ├── BCrypt.Verify(password, hash)
  ├── sp_UserLoginAndSessionCheck  →  Creates session, enforces 5-device limit
  ├── sp_GetAllowedModulesForUser  →  Returns subscribed module IDs
  └── Returns JWT { sub, session_id, allowed_modules: [1,2] }
       │
       ▼
DashboardWindow
  ├── Parses JWT → reads allowed_modules
  ├── Renders buttons: [GST Portal Downloader] [ITR Auto-Filer]
  └── User clicks module
           │
           ▼
  TriggerAutomationEngine(moduleId)
    ├── ApiService.GetModuleDownloadUrlAsync()  →  (future: secure CDN URL)
    ├── ApiService.DownloadScriptJsonAsync()    →  Returns JSON script
    └── AutomationEngine.RunScriptAsync()
          ├── Launch Chromium (visible)
          ├── goto  https://services.gst.gov.in/services/login
          ├── type  #username  ←  {GST_USERNAME}
          ├── type  #user_pass ←  {GST_PASSWORD}
          └── wait  3000ms
```

---

## Setup & Running

### Prerequisites

- .NET 8 SDK
- SQL Server (or connect to the remote instance in `appsettings.json`)
- Playwright browsers: run once after build:
  ```bash
  pwsh bin/Debug/net8.0-windows/playwright.ps1 install chromium
  ```

### 1. Initialize the Database

Run `master_DB.sql` against your SQL Server instance to create schema and seed data.

### 2. Start the API

```bash
cd AutomationServer.Api
dotnet run
# Swagger UI available at https://localhost:7045/swagger
```

### 3. Start the Desktop Client

```bash
cd GST_Suite_AutomationCafe
dotnet run
```

Login with seeded credentials, then click a module button to trigger automation.

---

## Security Notes

| Item | Status | Recommendation |
|---|---|---|
| Password storage | BCrypt hashing | Good |
| Auth tokens | JWT, 12-hour expiry | Good |
| Session management | 5-device limit, stale cleanup | Good |
| DB credentials in config | Plaintext in `appsettings.json` | Move to Key Vault / env vars |
| JWT secret in config | Plaintext in `appsettings.json` | Move to Key Vault / env vars |
| Hardcoded user credentials | `DashboardWindow.xaml.cs` | Read from secure vault or user prompt |
| Script delivery | Mock JSON hardcoded | Implement real `AutomationScripts` table lookup |
| TrustServerCertificate | `True` in connection string | Use proper cert in production |
