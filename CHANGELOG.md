# Changelog

All notable changes to **StangaNetLib.Jobs** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/StangaNet/StangaNetLib.Jobs/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/StangaNet/StangaNetLib.Jobs/releases/tag/v1.0.0
