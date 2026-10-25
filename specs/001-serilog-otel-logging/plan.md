# Implementation Plan: Shared Structured Logging with Trace Correlation

**Branch**: `001-serilog-otel-logging` | **Date**: 2026-10-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-serilog-otel-logging/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give every eShop service the same structured logging setup from one building-block call, with per-service `Serilog` settings in that service's `appsettings*.json`, and optional OpenTelemetry export plus request trace/span correlation.

Technical approach: keep the existing `BuildBlock.Logging` project. Expose a single `WebApplicationBuilder.AddAppLogging()` extension that calls `Host.UseSerilog` and `ReadFrom.Configuration`. Catalog is the first consumer. No OpenTelemetry tracing/metrics SDK. No new interfaces or options wrappers.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: Serilog.AspNetCore, Serilog.Sinks.OpenTelemetry, Serilog.Enrichers.Environment (latest stable compatible with net10.0)

**Storage**: N/A (diagnostics are emitted, not persisted by this feature)

**Testing**: xUnit, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing (test host)

**Target Platform**: ASP.NET Core services on Windows/Linux (Catalog first)

**Project Type**: Shared library consumed by web services

**Performance Goals**: Local diagnostics appear within 1 minute of start (SC-001); request handling is not blocked by remote export (edge case)

**Constraints**: One enable call per service; config lives only in that service's settings files; remote export optional and fail-open; secrets/tokens/connection strings never written; no extra architectural layers

**Scale/Scope**: One empty logging project, one first consumer (Catalog), reusable by later services; local console + optional OTLP only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates below use that template's example principles plus the workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | PASS | All wiring lives in `BuildBlock.Logging`. Services only call one extension and own settings files. |
| Test-first (non-negotiable) | PASS | Implementation must follow Red (failing tests, production `NotImplementedException`) then Green. See [quickstart.md](./quickstart.md). |
| Integration / contract tests | PASS | Test host covers enable, settings overrides, missing optional export, and request correlation. Public contract is [contracts/public-api.md](./contracts/public-api.md). |
| Observability | PASS | This feature *is* structured logging plus optional OTLP export and trace correlation. |
| Simplicity / abstraction ban | PASS | No new interfaces, options types, or logging facades. Reuse Serilog native configuration. One small redaction helper is authorized because FR-011 is a safety rule. |
| CLI interface | N/A | Constitution not ratified; this is a host library, not a CLI. |
| Spec-first | PASS | Spec exists at `specs/001-serilog-otel-logging/spec.md`. |

**Post-design re-check**: PASS. Phase 1 adds a JSON settings contract and a one-method public API. That does not add a new layer.

## Project Structure

### Documentation (this feature)

```text
specs/001-serilog-otel-logging/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── public-api.md
│   └── logging-configuration.schema.json
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/BuildingBlocks/BuildBlock.Logging/
├── BuildBlock.Logging.csproj
└── Extensions/
    └── LoggingExtensions.cs

src/Services/eShop.Catalog.Api/
├── DependencyInjection.cs          # calls AddAppLogging()
├── eShop.Catalog.Api.csproj        # project reference
├── appsettings.json                # Serilog section
└── appsettings.Development.json    # environment overrides

tests/BuildingBlocks/BuildBlock.Logging.Tests/
├── BuildBlock.Logging.Tests.csproj
├── LoggingEnableTests.cs
├── LoggingConfigurationTests.cs
└── TraceCorrelationTests.cs

eShop.slnx                          # include logging + test projects
```

**Structure Decision**: Extend the existing BuildingBlocks + Services layout. Do not rename `BuildBlock.Logging` (empty scaffold already exists). Do not add a second logging project, a shared options package, or an OpenTelemetry building block. Tests sit under `tests/BuildingBlocks/` so they stay next to the library they prove.

## Complexity Tracking

> No constitution violations to justify.
