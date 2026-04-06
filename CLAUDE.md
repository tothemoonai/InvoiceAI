# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build entire solution
dotnet build InvoiceAI.sln

# Build MAUI app for Windows only (faster)
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0

# Run tests
dotnet test tests/InvoiceAI.Core.Tests/

# Run a single test by name
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~TestMethodName"

# Run the Windows desktop app
dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0
```

## Architecture

4-layer architecture with strict dependency direction: **App → Core → Data → Models**

- **InvoiceAI.Models** (net9.0) — Pure POCO entities, no dependencies. Key types: `Invoice`, `InvoiceItem`, `InvoiceType` enum, `GlmInvoiceResponse`
- **InvoiceAI.Data** (net9.0) — EF Core + SQLite. `AppDbContext` with `Invoices` DbSet, `InvoiceConfiguration` for field mappings and indexes
- **InvoiceAI.Core** (net9.0) — All business logic: Services, ViewModels, GLM prompt template, AppSettings config model
- **InvoiceAI.App** (net9.0-windows10.0.19041.0) — MAUI entry point: `MauiProgram.cs` (DI), `App.cs` (lifecycle), `Pages/` (C# Markup UI)

## Key Patterns

**UI: C# Markup only, no XAML.** Uses `CommunityToolkit.Maui.Markup` fluent API. All pages are pure C# classes in `Pages/`.

**MVVM:** `CommunityToolkit.Mvvm` with `[ObservableProperty]` and `[RelayCommand]` source generators. ViewModels in `Core/ViewModels/`, constructor-injected via DI.

**DI Registration** (MauiProgram.cs):
- Singleton: HttpClient, all service implementations (OCR, GLM, File, Invoice, Excel, Settings)
- Transient: All ViewModels and Pages

**Service interfaces** live alongside implementations in `Core/Services/` — each service has `IXxxService.cs` + `XxxService.cs`.

**Database:** SQLite via EF Core Code First. Path: `FileSystem.AppDataDirectory/invoices.db`. Migration commands use `--startup-project src/InvoiceAI.App/InvoiceAI.App.csproj`.

**Config:** `AppSettingsService` reads/writes JSON to `%LOCALAPPDATA%/InvoiceAI/appsettings.json`. Stores API keys, language, category list.

## MAUI 9 API Rules

- `CollectionView` not `ListView` (deprecated in .NET 10)
- `Border` not `Frame`
- `MainThread.BeginInvokeOnMainThread()` not `Device.*`
- `VerticalStackLayout`/`HorizontalStackLayout` not compatibility `StackLayout`
- `DisplayAlertAsync()` not `DisplayAlert()`
- EPPlus 8+: use `ExcelPackage.LicenseContext` with `#pragma warning disable CS0618`

## Domain: Japanese Invoice Processing

The app processes Japanese invoices (適格請求書) per National Tax Agency rules. The GLM prompt in `Core/Prompts/InvoicePrompt.cs` embeds the full 6-item compliance checklist and returns structured JSON with `invoiceType` (Standard/Simplified/NonQualified) and `missingFields`.

**Processing pipeline:** File import → Baidu OCR (images + PDF directly) → GLM API (text analysis) → SQLite storage → Excel export.

## Project Status

See `docs/26040521-handoff.md` for detailed task tracking. Tasks 1-11 complete (scaffold through DI). Remaining: MainPage 3-column UI (Task 12), drag-drop import (Task 13), settings page (Task 14), unit tests (Task 15), integration build (Task 16).
