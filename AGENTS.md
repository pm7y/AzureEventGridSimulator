# AGENTS.md

## Overview

Azure Event Grid Simulator - local HTTPS simulator for Azure Event Grid topics/subscribers. Compatible with Microsoft.Azure.EventGrid client library, supports EventGrid and CloudEvents v1.0 schemas.

**Stack:** .NET 10.0, C#, Serilog, xUnit/Shouldly/NSubstitute

## Commands

```bash
# Build
dotnet build src/AzureEventGridSimulator.sln --configuration Release

# Test
dotnet test src/AzureEventGridSimulator.sln --configuration Release
dotnet test src/AzureEventGridSimulator.sln --filter "Category=unit"
dotnet test src/AzureEventGridSimulator.sln --filter "Category=integration"

# Run
dotnet run --project src/AzureEventGridSimulator/AzureEventGridSimulator.csproj

# Format (runs automatically on build)
dotnet csharpier format src
```

## Project Structure

```
src/
├── AzureEventGridSimulator/           # Main application
│   ├── Controllers/                    # API endpoints
│   ├── Domain/                         # Business logic
│   │   ├── Commands/                   # Command handlers
│   │   ├── Entities/                   # Domain models
│   │   └── Services/                   # Domain services (Delivery/, Retry/)
│   ├── Infrastructure/                 # Cross-cutting concerns
│   │   ├── Mediator/                   # Custom mediator (no MediatR)
│   │   ├── Middleware/                 # HTTP middleware
│   │   └── Settings/                   # Configuration models
│   └── Program.cs                      # Entry point
├── AzureEventGridSimulator.Tests/      # Tests (UnitTests/, IntegrationTests/)
├── Directory.Build.props               # Shared MSBuild properties
└── Directory.Packages.props            # Central Package Management
```

## Code Style

- **Framework:** .NET 10.0, latest C# features
- **Nullable:** Enabled
- **Formatter:** CSharpier (100 char width, runs on build)
- **Namespaces:** File-scoped required
- **Preferences:** `var` for variables, pattern matching, primary constructors, collection expressions (`[]`)

## Testing

```csharp
[Trait("Category", "unit")]  // or "integration"
public class MyTests
{
    [Fact]
    public void Should_DoX_When_Y()
    {
        // Arrange/Act/Assert with Shouldly
        result.ShouldBe(expected);
    }
}
```

## Key Constraints

- **HTTPS only** - all topic endpoints require HTTPS
- **Authentication** - `aeg-sas-key` or `aeg-sas-token` headers when topic has `key` configured
- **Message limits** - 1 MB max per event and overall body
- **Schemas** - EventGrid (default) or CloudEvents v1.0, auto-detected or configured

## Commits & Releases

Uses [Conventional Commits](https://www.conventionalcommits.org/) with Release Please:

- `feat:` = minor bump, `fix:` = patch bump, `feat!:`/`fix!:` = major bump
- `docs`, `style`, `refactor`, `test`, `chore` = no version bump

Merging Release Please PRs triggers GitHub releases, NuGet publish, and Docker builds.

## Configuration

Topics configured in `appsettings.json`. Each topic needs `name`, `port`, optional `key`. Subscribers support HTTP webhooks, Service Bus, Storage Queue, and Event Hub with filtering, retry, and dead-letter options.
