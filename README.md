# StangaNetLib.Jobs

[![.NET CI](https://github.com/StangaNet/StangaNetLib.Jobs/actions/workflows/main.yml/badge.svg)](https://github.com/StangaNet/StangaNetLib.Jobs/actions/workflows/main.yml)
![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)

Background jobs — fire-and-forget, delayed, and cron recurring — with a built-in in-memory backend and a Hangfire adapter. Uses `Result` / `Guard` from [`StangaNetLib.Core`](https://github.com/StangaNet/StangaNetLib.Core) and optional keyed locks from [`StangaNetLib.Concurrency`](https://github.com/StangaNet/StangaNetLib.Concurrency).

## Installation

The package is hosted on **GitHub Packages** (pulls Core/Concurrency from the same feed).

```xml
<!-- NuGet.config — source key must match your packageSourceCredentials -->
<configuration>
  <packageSources>
    <add key="StangaNet" value="https://nuget.pkg.github.com/StangaNet/index.json" />
  </packageSources>
</configuration>
```

```xml
<!-- .csproj -->
<PackageReference Include="StangaNetLib.Jobs" Version="1.0.0" />
```

See [CHANGELOG.md](./CHANGELOG.md) for release history.

## Core Components

| Namespace | Components | Purpose |
|---|---|---|
| `StangaNetLib.Jobs.Abstractions` | `IBackgroundJob<TArgs>`, `IJobScheduler`, `IJobManager` | Contracts |
| `StangaNetLib.Jobs.Models` | `JobContext`, `JobInfo`, `JobState` | Shared models |
| `StangaNetLib.Jobs.Errors` | `JobsErrors` | Well-known failures |
| `StangaNetLib.Jobs.Settings` | `JobsSettings` | Options |
| `StangaNetLib.Jobs.Extensions` | `AddStangaNetLibJobs`, `AddStangaNetLibJobsWithHangfire` | DI |

## Quick Start

```csharp
// In-memory (dev / single instance)
builder.Services.AddStangaNetLibJobs(builder.Configuration);
builder.Services.AddTransient<SendWelcomeEmailJob>();

// Hangfire
builder.Services.AddStangaNetLibJobsWithHangfire(builder.Configuration, cfg =>
    cfg.UseInMemoryStorage()); // or UseSqlServerStorage(...)
```

```csharp
await scheduler.EnqueueAsync<SendWelcomeEmailJob, SendWelcomeEmailArgs>(
    new SendWelcomeEmailArgs("a@b.com", "Ada"));

scheduler.AddOrUpdateRecurring<CleanupJob, CleanupArgs>("nightly-cleanup", args, "0 2 * * *");
```

Optional `appsettings.json`:

```json
{
  "JobsSettings": {
    "DefaultRetryCount": 3,
    "DefaultRetryDelay": "00:00:05",
    "WorkerCount": 4,
    "RecurringCheckInterval": "00:00:30"
  }
}
```

## License

Apache-2.0 — see [LICENSE](./LICENSE) and [NOTICE](./NOTICE).
