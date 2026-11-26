# Golddie Development Instructions

## Project

Golddie is one WPF application for maternity clinic management, built with C#, .NET 10 for Windows, EF Core, SQLite, CommunityToolkit.Mvvm, and MaterialDesignInXamlToolkit.

Keep one application with shared services and a shared data layer. Provide role-specific Admin and Nurse/Midwife experiences within it. Prioritize Nurse-side workflows. Preserve manual vital-sign entry alongside planned ESP32 device integration.

## Architecture and workflow

The solution uses WPF views and view models, services, repositories, EF Core data access, and SQLite. Follow the patterns in the relevant existing files; do not introduce a new framework or architectural layer without a concrete need.

Before editing:

1. Inspect relevant files, callers, tests, git status, and diffs.
2. Preserve all existing user changes, including the 13 pre-existing modified paths documented in DEVELOPMENT_PROGRESS.md. Never overwrite or discard those changes. Do not stage, stash, reset, or clean them.
3. Make only the focused changes needed for the request.
4. Review the final diff and report verification performed.

Avoid unnecessary rewrites, unrelated cleanup, package installation, and project configuration changes.

## Database and verification

The application applies EF Core migrations at startup. Treat migrations and data backfills as data-sensitive. Do not run migrations or update, reset, recreate, or delete a database unless explicitly requested. Inspect the context, snapshot, and migration history before a requested schema change.

From the repository root, build and test with:

    dotnet build D:\Thesis\MaternityManagementSystem\MaternityManagementSystem.slnx
    dotnet test D:\Thesis\MaternityManagementSystem\MaternityManagementSystem.slnx

Report build warnings and errors separately. Do not fix unrelated pre-existing issues.

## Clinical and workflow safety

Do not invent medical thresholds, clinical rules, or clinic workflow policy. Mark unverified policies as requiring confirmation and obtain clinic approval before implementing them. Keep technical validation distinct from clinical decisions.