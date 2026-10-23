# StangaNetLib.Jobs — Copilot instructions

- Background jobs: in-memory backend + Hangfire adapter; cron via Cronos.
- Prefer `Result` / `Result<T>` and `Guard` from `StangaNetLib.Core` — do not reintroduce local Result/Guard types.
- Keep public APIs documented (`///`) and options-driven via `JobsSettings`.
- DI: `AddStangaNetLibJobs` / `AddStangaNetLibJobsWithHangfire`.
- Optional `IKeyedLock` from `StangaNetLib.Concurrency` for recurring-job trigger safety.
- Match existing naming, file-scoped namespaces, and EditorConfig / Roslyn conventions.
- Tests use xUnit v3 + FluentAssertions; cover `net8.0`, `net9.0`, and `net10.0` when changing shared behavior.
- Do not add a second job provider without keeping the abstraction surface stable.
