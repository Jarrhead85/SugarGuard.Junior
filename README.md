# SugarGuard Junior

SugarGuard Junior is a child-focused diabetes monitoring system with a MAUI mobile app, ASP.NET Core API, Blazor web portals, and Telegram notifications.

The product connects a child, parent, doctor, and administrator around one shared data model: glucose measurements, backpack snacks, nutrition and insulin diary, meal schedules, reminders, doctor notes, achievements, exports, and audit.

## Projects

| Project | Purpose |
|---|---|
| `SugarGuard.API` | ASP.NET Core Web API, auth, domain workflows, integrations |
| `SugarGuard.Web` | Blazor Server web portals for Parent, Doctor, and Admin |
| `SugarGuard.Junior` | .NET MAUI mobile app for the child |
| `SugarGuard.Domain` | Domain entities and rules |
| `SugarGuard.Application` | Application contracts and use cases |
| `SugarGuard.Infrastructure` | EF Core repositories and infrastructure services |
| `SugarGuard.Shared` | DTOs, validation, constants, shared helpers |
| `SugarGuard.Tests` | xUnit test project |

## Main Features

- Child mobile app: glucose input, AI advice, backpack, nutrition/insulin diary, meal reminders, achievements, parent/doctor link codes.
- Parent portal: live dashboard, measurements, notifications, backpack CRUD, nutrition diary and schedule, doctor notes, profile, access management, exports.
- Doctor portal: patient list, risk groups, patient cards, glucose analytics, clinical notes, profile settings.
- Admin portal: users, roles, doctor verification, role links, audit, sync/export logs, system status.
- Integrations: SMTP email, Telegram bot, GigaChat recommendations, CSV/PDF exports.
- Security: JWT + refresh tokens, role-based authorization, encrypted PHI fields, HMAC connection codes.

All verification, reset, and linking codes use the unified `ABCD-1234` format.

## Requirements

- .NET SDK 9
- PostgreSQL for production
- Android SDK and MAUI workload for Android builds
- macOS + Xcode for iOS builds
- Optional: Telegram bot token, GigaChat credentials, SMTP account

## Configuration

Use environment variables, user-secrets, or server configuration. Do not commit secrets.

Typical keys:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
Jwt__Issuer
Jwt__Audience
CONNECTION_CODE_KEY
Smtp__Host
Smtp__Port
Smtp__Username
Smtp__Password
Smtp__FromEmail
Telegram__BotToken
GigaChat__AuthorizationKey
GigaChat__ClientId
GigaChat__ClientSecret
```

## Local Development

Restore, build, and test:

```powershell
dotnet restore .\SugarGuard.Junior.slnx
dotnet build .\SugarGuard.Junior.slnx -v minimal
dotnet test .\SugarGuard.Tests\SugarGuard.Tests.csproj -v minimal
```

Run API:

```powershell
dotnet run --project .\SugarGuard.API\SugarGuard.API.csproj
```

Run Web:

```powershell
dotnet run --project .\SugarGuard.Web\SugarGuard.Web.csproj
```

Build Android app:

```powershell
dotnet build .\SugarGuard.Junior\SugarGuard.Junior.csproj -f net9.0-android -v minimal
```

## Deployment

The repository contains deployment scripts for the VDS environment.

```powershell
.\scripts\deploy-vds.ps1
```

Production services are expected to run behind HTTPS as separate systemd units for API and Web. Secrets are stored on the server, not in the repository.

## Documentation

- [Project description](./Описание-v3.md)
- [UI Kit](./UIKit-v2.md)

## Development Notes

- Keep Parent, Doctor, and Admin web portals on the shared visual system.
- Parent sidebar and top tabs must use the same section names and order.
- Prefer real persisted data over demo placeholders.
- New API behavior should be covered with xUnit tests when it changes domain rules, authorization, synchronization, or exports.
