# Changelog

All notable changes to **StangaNetLib.Jobs** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]


## [1.1.0] - 2026-10-23

### Added
- Idempotent enqueue/schedule overloads with keyed lock (`IKeyedLock`) on the in-memory backend.
- `JobsSettings.MaterializeHorizonDays` — materialize cron occurrences into scheduled one-off jobs for the next N days (in-memory).

### Notes
- Hangfire ignores idempotency keys and horizon materialization (uses Hangfire storage scheduling).

## [1.0.1] - 2026-10-23

### Changed
- Add `net10.0` target framework alongside `net8.0` and `net9.0`.
- Require .NET SDK 10 (global.json) to multi-target `net8.0` / `net9.0` / `net10.0`; opt in Microsoft.Testing.Platform as the `dotnet test` runner.
- Remove redundant Microsoft.Extensions.* PackageReferences from ASP.NET Core projects (provided by Microsoft.AspNetCore.App; avoids NU1510 under the .NET 10 SDK).

## [1.0.0] - 2026-10-18

### Added
- `IBackgroundJob<TArgs>`, `IJobScheduler`, and `IJobManager` provider-agnostic abstractions.
- In-memory backend with workers, retries, delayed/scheduled jobs, and cron recurring jobs (Cronos).
- Optional `IKeyedLock` integration (from Concurrency) for safe recurring-job triggers.
- Hangfire adapter (`AddStangaNetLibJobsWithHangfire`) bridging to Hangfire storage/server.
- `JobsSettings` with options validation; `JobsErrors` well-known failures.
- Dual targeting `net8.0` / `net9.0`, XML docs, symbols (`snupkg`), package icon, and MSBuild consumer metadata stamp.
- Depends on `StangaNetLib.Core` 1.0.0 and `StangaNetLib.Concurrency` 1.0.0; Microsoft.Extensions.* pinned to 8.x.
- CI via shared StangaNet reusable workflow (push / PR / manual; publish outside PRs).

[Unreleased]: https://github.com/StangaNet/StangaNetLib.Jobs/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/StangaNet/StangaNetLib.Jobs/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/StangaNet/StangaNetLib.Jobs/releases/tag/v1.0.0
