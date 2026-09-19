# AGENTS.md

## Overview

Azure Event Grid Simulator - local HTTPS simulator for Azure Event Grid topics/subscribers. Compatible with the Azure.Messaging.EventGrid SDK (and the legacy Microsoft.Azure.EventGrid client), supports EventGrid and CloudEvents v1.0 schemas.

**Stack:** .NET 10 (net10.0), C#, Serilog, xUnit/Shouldly/NSubstitute

## Commands

```bash
# Build
dotnet build src/AzureEventGridSimulator.slnx --configuration Release

# Test
dotnet test src/AzureEventGridSimulator.slnx --configuration Release    # every category, including integration-actual
dotnet test src/AzureEventGridSimulator.slnx --filter "Category!=integration-actual"    # the tests CI runs
dotnet test src/AzureEventGridSimulator.slnx --filter "Category=unit"
dotnet test src/AzureEventGridSimulator.slnx --filter "Category=integration"

# Run
dotnet run --project src/AzureEventGridSimulator/AzureEventGridSimulator.csproj

# Format (runs automatically on build)
dotnet csharpier format src
```

**Test categories:** `unit`; `integration` (no Docker or external services needed; `IntegrationTests/` hosts the app in-process with `WebApplicationFactory`); `integration-actual` (starts the built simulator on `https://localhost:60101`, so it clashes with anything already listening on that port). CI doesn't run `integration-actual`.

**Parity tests:** `src/postman/` holds a Postman/newman collection designed to run against both real Azure Event Grid and the simulator. See [Using Postman](https://github.com/pm7y/AzureEventGridSimulator/wiki/Schema-Support#using-postman).

## Project Structure

```
src/
├── AzureEventGridSimulator/                  # The simulator (also packed as the .NET tool)
│   ├── Controllers/                          # HTTP endpoints
│   ├── Dashboard/                            # Embedded dashboard UI assets
│   ├── Domain/
│   │   ├── Commands/                         # Command handlers (mediator pattern)
│   │   ├── Entities/                         # Domain models (Dashboard/ for dashboard entities)
│   │   ├── Filtering/                        # Subscription filter evaluation (EventFilterEvaluator)
│   │   └── Services/                         # Schema detection/parsing/formatting; Dashboard/, Delivery/, Retry/, Routing/, Validation/
│   ├── Infrastructure/
│   │   ├── Dashboard/                        # Dashboard middleware and endpoints
│   │   ├── Extensions/                       # Configuration, Kestrel and DI extensions
│   │   ├── JsonConverters/                   # Custom JSON serialization
│   │   ├── Mediator/                         # Custom mediator (no MediatR)
│   │   ├── Middleware/                       # Request validation and parsing, SAS auth
│   │   └── Settings/                         # Configuration models (Subscribers/ for subscriber settings)
│   └── Program.cs                            # Entry point
├── AzureEventGridSimulator.AppHost/          # .NET Aspire orchestration (local development)
├── AzureEventGridSimulator.ServiceDefaults/  # Aspire service defaults (OpenTelemetry, health checks)
├── AzureEventGridSimulator.Tests/
│   ├── UnitTests/                            # Unit tests, one folder per area (Dashboard/, Retry/, Routing/, ...)
│   │   └── Common/                           # Shared test helpers (TestHelpers, FakeTimeProvider)
│   ├── IntegrationTests/                     # In-process tests using WebApplicationFactory
│   └── ActualSimulatorTests/                 # Tests that launch the built simulator as a separate process
├── postman/                                  # Postman/newman parity collection and environments
├── Directory.Build.props                     # Shared MSBuild properties
└── Directory.Packages.props                  # Central Package Management
```

The canonical, fuller tree is on the wiki: [Architecture: Source Code Structure](https://github.com/pm7y/AzureEventGridSimulator/wiki/Architecture#source-code-structure).

## Code Style

- **Framework:** .NET 10.0, latest C# features
- **Nullable:** Enabled
- **Formatter:** CSharpier (100 char width, runs on build)
- **Namespaces:** File-scoped required
- **Preferences:** `var` for variables, pattern matching, primary constructors, collection expressions (`[]`)

## Testing

Name tests `GivenX_WhenY_ThenZ`, e.g. `GivenExpiredAuthorizationHeader_WhenValidated_ThenReturnsFalse`.

```csharp
[Trait("Category", "unit")]  // or "integration"
public class MyTests
{
    [Fact]
    public void GivenX_WhenY_ThenZ()
    {
        // Arrange/Act/Assert with Shouldly
        result.ShouldBe(expected);
    }
}
```

## Key Constraints

- **HTTPS only** - all topic endpoints require HTTPS
- **Authentication** - `aeg-sas-key`, `aeg-sas-token` or `Authorization: SharedAccessSignature` header when topic has `key` configured
- **Message limits** - defaults: 1,049,600 bytes (~1 MB) per event, 1,536,000 bytes (~1.5 MB) overall body
- **Schemas** - EventGrid (default) or CloudEvents v1.0, auto-detected or configured
- **Build flavours** - the AppHost builds the simulator with `ASPIRE_ENABLED=true` into separate `bin/aspire/` and `obj/aspire/` paths; the regular build uses `bin/`/`obj/`. Don't mix artifacts between the two.
- **Vendored DOMPurify** - the dashboard serves its own copy, `Dashboard/purify-<version>.min.js` (embedded, with `-text` in `.gitattributes` so its bytes never change). Dependabot can't see it, so bump it by hand: replace the file and update the `<script src>` in `Dashboard/index.html`.

## Commits & Releases

Uses [Conventional Commits](https://www.conventionalcommits.org/) with Release Please:

- `feat:` = minor bump, `fix:` = patch bump, `feat!:`/`fix!:` = major bump
- `docs`, `style`, `refactor`, `test`, `chore` = no version bump

Merging Release Please PRs triggers GitHub releases, NuGet publish, and Docker builds.

## Configuration

Topics configured in `appsettings.json`. Each topic needs `name`, `port`, optional `key`. Subscribers support HTTP webhooks, Service Bus, Storage Queue, and Event Hub with filtering, retry, and dead-letter options.
