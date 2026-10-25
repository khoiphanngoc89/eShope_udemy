# Tasks: Shared Structured Logging with Trace Correlation

**Input**: Design documents from `/specs/001-serilog-otel-logging/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Plan, quickstart, and workspace TDD rules require Red (failing tests, production `NotImplementedException`) then Green.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Library: `src/BuildingBlocks/BuildBlock.Logging/`
- First consumer: `src/Services/eShop.Catalog.Api/`
- Tests: `tests/BuildingBlocks/BuildBlock.Logging.Tests/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the existing logging project, test project, and solution so later phases can compile.

- [x] T001 Create xUnit test project `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` targeting net10.0 with Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio, Microsoft.AspNetCore.Mvc.Testing, and a ProjectReference to `src/BuildingBlocks/BuildBlock.Logging/BuildBlock.Logging.csproj`
- [x] T002 [P] Add FrameworkReference `Microsoft.AspNetCore.App` plus packages Serilog.AspNetCore, Serilog.Sinks.OpenTelemetry, and Serilog.Enrichers.Environment (latest stable compatible with net10.0) to `src/BuildingBlocks/BuildBlock.Logging/BuildBlock.Logging.csproj`
- [x] T003 [P] Add `src/BuildingBlocks/BuildBlock.Logging/BuildBlock.Logging.csproj` and `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` to `eShop.slnx`
- [x] T004 [P] Delete scaffold `src/BuildingBlocks/BuildBlock.Logging/Class1.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared stubs and test host that every user story uses. No story work until this phase is done.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T005 Create `AddAppLogging(this WebApplicationBuilder builder)` in `src/BuildingBlocks/BuildBlock.Logging/Extensions/LoggingExtensions.cs` that throws `NotImplementedException` (contract signature from `specs/001-serilog-otel-logging/contracts/public-api.md`)
- [x] T006 [P] Create collecting sink `tests/BuildingBlocks/BuildBlock.Logging.Tests/Support/CollectingSink.cs` that stores Serilog events for assertions
- [x] T007 Create host helper `tests/BuildingBlocks/BuildBlock.Logging.Tests/Support/LoggingTestHost.cs` that builds a `WebApplication` with `AddAppLogging()`, optional in-memory `Serilog` config, and `CollectingSink`

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 - Enable shared structured logging from one building block (Priority: P1) 🎯 MVP

**Goal**: One `AddAppLogging()` call gives a service structured diagnostics with timestamp, severity, message, service identity, and environment. Secrets are redacted. Catalog enables the building block.

**Independent Test**: Boot `LoggingTestHost` with a valid local config and confirm structured events for startup. Call `AddAppLogging()` from Catalog with no other Serilog wiring.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T008 [US1] Write failing contract/enable tests in `tests/BuildingBlocks/BuildBlock.Logging.Tests/LoggingEnableTests.cs`: null `builder` throws `ArgumentNullException`; after enable+start, each Diagnostic Record includes Timestamp (required), Severity (required), Message (required), `service.name` (required, `IHostEnvironment.ApplicationName`), and `deployment.environment` (required, host environment name)
- [x] T009 [P] [US1] Write failing redaction tests in `tests/BuildingBlocks/BuildBlock.Logging.Tests/LoggingRedactionTests.cs`: scalar names containing Password, Secret, Token, ConnectionString, or Authorization (case-insensitive) are stored as `***`

### Implementation for User Story 1

- [x] T010 [US1] Implement `AddAppLogging` in `src/BuildingBlocks/BuildBlock.Logging/Extensions/LoggingExtensions.cs`: null check; `UseSerilog`; always enrich `service.name` and `deployment.environment`; return the same `WebApplicationBuilder`. Leave `ReadFrom.Configuration` for US2 if needed to keep this increment minimal; production must not stay on `NotImplementedException`
- [x] T011 [US1] Implement `src/BuildingBlocks/BuildBlock.Logging/Enrichers/SensitiveDataEnricher.cs` and register it from `LoggingExtensions.cs` so listed sensitive names become `***`
- [x] T012 [P] [US1] Add ProjectReference to `src/BuildingBlocks/BuildBlock.Logging/BuildBlock.Logging.csproj` in `src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj` (Catalog must not add its own Serilog package references)
- [x] T013 [US1] Call `builder.AddAppLogging()` at the start of `AddCatalogServices` in `src/Services/eShop.Catalog.Api/DependencyInjection.cs`

**Checkpoint**: User Story 1 is independently testable (enable + identity + redaction + Catalog hook)

---

## Phase 4: User Story 2 - Control logging per service through that service's settings (Priority: P2)

**Goal**: Each service owns a `Serilog` section in its `appsettings*.json`. Environment files win. Missing section uses safe defaults. Two hosts honor their own minimum severity.

**Independent Test**: Change only in-memory/temp settings (or Catalog `appsettings*.json`), restart that host, and confirm verbosity/outputs change without editing `BuildBlock.Logging`.

### Tests for User Story 2 ⚠️

- [x] T014 [US2] Write failing configuration tests in `tests/BuildingBlocks/BuildBlock.Logging.Tests/LoggingConfigurationTests.cs`: missing entire `Serilog` section is valid and applies safe defaults (local console, Default `Information`, `Microsoft.AspNetCore` → `Warning`, no OpenTelemetry sink); environment-specific `MinimumLevel` overrides base (environment-specific values win); two hosts with different `MinimumLevel.Default` emit independently; a present `Serilog` section applies `MinimumLevel` and `WriteTo` without a building-block code change; empty `WriteTo` still writes console

### Implementation for User Story 2

- [x] T015 [US2] Implement `ReadFrom.Configuration` and `ReadFrom.Services` in `src/BuildingBlocks/BuildBlock.Logging/Extensions/LoggingExtensions.cs`. When the `Serilog` section is missing, apply console + `Information` + `Microsoft.AspNetCore` Warning + no OTLP. When `WriteTo` is empty, still write console (data-model: building-block defaults MUST still write to console)
- [x] T016 [P] [US2] Replace the MEL `Logging` section with a `Serilog` section in `src/Services/eShop.Catalog.Api/appsettings.json` matching `specs/001-serilog-otel-logging/contracts/logging-configuration.schema.json` (Using Console + Environment; MinimumLevel Default Information; Override Microsoft.AspNetCore Warning; Enrich FromLogContext, WithEnvironmentName; WriteTo Console only — omit OpenTelemetry)
- [x] T017 [P] [US2] Add environment-specific `Serilog` overrides in `src/Services/eShop.Catalog.Api/appsettings.Development.json` so Development values win over `appsettings.json`

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Correlate diagnostic records with distributed traces (Priority: P3)

**Goal**: Request activities stamp TraceId/SpanId on events. OTLP export is optional via appsettings and fail-open. Events with no activity stay valid.

**Independent Test**: Handle work under `Activity.Current` and assert matching IDs; start with OTLP omitted or unreachable and still get local events.

### Tests for User Story 3 ⚠️

- [x] T018 [US3] Write failing correlation/export tests in `tests/BuildingBlocks/BuildBlock.Logging.Tests/TraceCorrelationTests.cs`: with `Activity.Current`, at least 95% of events include TraceId (32-hex W3C) and SpanId (16-hex W3C) matching that activity; without activity, records remain valid and omit those IDs; OpenTelemetry `WriteTo` omitted → host starts and local events are written; OpenTelemetry sink with unreachable `endpoint` → host starts and local events are written (request handling not blocked)

### Implementation for User Story 3

- [x] T019 [US3] Capture `Activity` TraceId/SpanId on `LogEvent` in `src/BuildingBlocks/BuildBlock.Logging/Extensions/LoggingExtensions.cs` (Serilog 4 first-class fields; add an Activity listener only if tests prove none exists). Do not add an OpenTelemetry tracing/metrics SDK
- [x] T020 [US3] Keep Catalog export off by default (no OpenTelemetry `WriteTo`) in `src/Services/eShop.Catalog.Api/appsettings.json`. Prove the sink is available when tests supply `Using: Serilog.Sinks.OpenTelemetry` and `WriteTo` Name `OpenTelemetry` Args `endpoint` + `protocol` `Grpc` or `HttpProtobuf` via `ReadFrom.Configuration` already in `LoggingExtensions.cs`

**Checkpoint**: All three user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate the quickstart and leave the library lean.

- [x] T021 Run `dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` and fix only regressions
- [x] T022 Run Catalog per `specs/001-serilog-otel-logging/quickstart.md` (`dotnet run --project src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`) and confirm structured startup diagnostics for `eShop.Catalog.Api` within 1 minute
- [x] T023 [P] Remove unused usings/types from `src/BuildingBlocks/BuildBlock.Logging/` and confirm no second public API beyond `LoggingExtensions` and the internal enricher

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: Depend on Foundational
  - Prefer P1 → P2 → P3 (same `LoggingExtensions.cs` file)
  - US2/US3 tests can be drafted in parallel after Phase 2 because they use different test files
- **Polish (Phase 6)**: Depends on the stories you intend to ship

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 only. MVP.
- **User Story 2 (P2)**: After Phase 2. Extends `LoggingExtensions.cs` with `ReadFrom.Configuration`. Independently testable via `LoggingConfigurationTests.cs`.
- **User Story 3 (P3)**: After Phase 2. Needs a working host (US1). Config-driven OTLP is cleaner after US2. Independently testable via `TraceCorrelationTests.cs`.

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Implementation in `LoggingExtensions.cs` before Catalog wiring when both touch behavior
- Story complete before the next priority if one person is implementing (shared extension file)

### Parallel Opportunities

- T002, T003, T004 after T001 is sketched (different files)
- T006 in parallel with T005
- T008 and T009 in parallel (different test files)
- T012 in parallel with T010/T011 (Catalog csproj vs library)
- T016 and T017 in parallel (two appsettings files)
- After Phase 2, a second person can draft T014 and T018 while US1 is implemented

---

## Parallel Example: User Story 1

```bash
# After Phase 2, draft both US1 test files together:
Task: "Failing enable tests in tests/BuildingBlocks/BuildBlock.Logging.Tests/LoggingEnableTests.cs"
Task: "Failing redaction tests in tests/BuildingBlocks/BuildBlock.Logging.Tests/LoggingRedactionTests.cs"

# After tests fail, library + Catalog reference can proceed on different files:
Task: "Implement AddAppLogging in src/BuildingBlocks/BuildBlock.Logging/Extensions/LoggingExtensions.cs"
Task: "Add ProjectReference in src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj"
```

---

## Parallel Example: User Story 2

```bash
# Settings files do not depend on each other:
Task: "Serilog section in src/Services/eShop.Catalog.Api/appsettings.json"
Task: "Overrides in src/Services/eShop.Catalog.Api/appsettings.Development.json"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: `dotnet test` filter `LoggingEnableTests|LoggingRedactionTests`
5. Demo Catalog startup logs if settings defaults are enough

### Incremental Delivery

1. Setup + Foundational → stub + test host ready
2. US1 → shared enable + Catalog hook (MVP)
3. US2 → per-service `appsettings` control
4. US3 → TraceId/SpanId + optional fail-open OTLP
5. Polish → quickstart validation

### Parallel Team Strategy

1. Together: Phase 1–2
2. Then:
   - Dev A: US1 (`LoggingExtensions.cs`, Catalog DI)
   - Dev B: US2/US3 test files (`LoggingConfigurationTests.cs`, `TraceCorrelationTests.cs`)
3. Dev A (or B) applies US2/US3 production changes in `LoggingExtensions.cs` sequentially to avoid merge conflicts

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work
- Do not create interfaces, options wrappers, or an OpenTelemetry building block
- Do not rename `BuildBlock.Logging`
- Verify tests fail before implementing each story
- Stop at any checkpoint to validate the story independently
