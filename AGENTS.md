# AGENTS.md

## Overview

Azure Event Grid Simulator is a local development simulator that provides HTTPS endpoints mimicking Azure Event Grid topics and subscribers. It is compatible with the Microsoft.Azure.EventGrid client library and supports both EventGrid and CloudEvents v1.0 schemas.

**Technology Stack:**

- .NET 10.0 (ASP.NET Core Web API)
- C# with latest language features
- Serilog for structured logging
- xUnit for testing with Shouldly assertions and NSubstitute for mocking

**Key Features:**

- Multi-topic support with individual HTTPS endpoints
- HTTP webhook and Azure Service Bus subscriber delivery
- Event filtering (subject-based and advanced)
- Schema transformation between EventGrid and CloudEvents
- Authentication via aeg-sas-key or aeg-sas-token headers

## Development Environment

### Prerequisites

- .NET 10.0 SDK (specified in `global.json`)
- Docker (optional, for containerized development)
- HTTPS development certificate (required for local testing)

### Initial Setup

```bash
# Clone and navigate to the repository
cd /src/AzureEventGridSimulator

# Restore .NET tools (includes CSharpier formatter)
dotnet tool restore

# Restore dependencies
dotnet restore /src/AzureEventGridSimulator/src/AzureEventGridSimulator.sln

# Trust the development certificate (required for HTTPS)
dotnet dev-certs https --trust
```

### Configuration

The simulator uses `appsettings.json` for topic and subscriber configuration. Key settings:

- **Topics**: Each topic requires `name`, `port`, and optional `key` for authentication
- **Subscribers**: Supports HTTP webhooks and Azure Service Bus (queues/topics)
- **Filtering**: Event type, subject-based, and advanced filtering per subscriber

Example configuration: `/src/AzureEventGridSimulator/src/AzureEventGridSimulator/appsettings.json`

Docker configuration: `/src/AzureEventGridSimulator/docker/appsettings.docker.json`

## Commands

### Build and Run

```bash
# Build the solution
dotnet build /src/AzureEventGridSimulator/src/AzureEventGridSimulator.sln --configuration Release

# Run the simulator
dotnet run --project /src/AzureEventGridSimulator/src/AzureEventGridSimulator/AzureEventGridSimulator.csproj

# Run with custom config file
dotnet run --project /src/AzureEventGridSimulator/src/AzureEventGridSimulator/AzureEventGridSimulator.csproj -- --ConfigFile=/path/to/config.json
```

### Testing

```bash
# Run all tests
dotnet test /src/AzureEventGridSimulator/src/AzureEventGridSimulator.sln --configuration Release

# Run only unit tests
dotnet test /src/AzureEventGridSimulator/src/AzureEventGridSimulator.sln --filter "Category=unit"

# Run only integration tests
dotnet test /src/AzureEventGridSimulator/src/AzureEventGridSimulator.sln --filter "Category=integration"

# Run tests with coverage
dotnet test /src/AzureEventGridSimulator/src/AzureEventGridSimulator.sln --collect:"XPlat Code Coverage"
```

### Code Formatting

CSharpier runs automatically on build (via MSBuild target in `Directory.Build.props`). To format manually:

```bash
# Format all code
dotnet csharpier format /src/AzureEventGridSimulator/src

# Check formatting without applying changes
dotnet csharpier check /src/AzureEventGridSimulator/src
```

### Docker

```bash
# Build Docker image
docker build -t azureeventgridsimulator:dev -f /src/AzureEventGridSimulator/Dockerfile /src/AzureEventGridSimulator

# Run with docker-compose (includes Service Bus emulator, SQL Server, Seq, and Azurite)
docker-compose -f /src/AzureEventGridSimulator/docker-compose.yml up --build --detach

# Stop docker-compose services
docker-compose -f /src/AzureEventGridSimulator/docker-compose.yml down
```

## Architecture

### Project Structure

```
/src
├── AzureEventGridSimulator/          # Main application
│   ├── Controllers/                   # API endpoints
│   ├── Domain/                        # Business logic layer
│   │   ├── Commands/                  # Command handlers
│   │   ├── Entities/                  # Domain models
│   │   └── Services/                  # Domain services
│   ├── Infrastructure/                # Cross-cutting concerns
│   │   ├── Extensions/                # Extension methods
│   │   ├── Mediator/                  # Custom mediator implementation
│   │   ├── Middleware/                # HTTP middleware
│   │   └── Settings/                  # Configuration models
│   └── Program.cs                     # Application entry point
├── AzureEventGridSimulator.Tests/     # Test project
│   ├── UnitTests/                     # Unit tests
│   ├── IntegrationTests/              # Integration tests
│   └── ActualSimulatorTests/          # End-to-end tests
├── Directory.Build.props              # Shared MSBuild properties
└── Directory.Packages.props           # Central Package Management (CPM)
```

### Key Design Patterns

- **Mediator Pattern**: Custom in-process mediator for command handling (no MediatR dependency)
- **Middleware Pipeline**: Request validation, authentication, and logging
- **Domain-Driven Design**: Clear separation between domain logic, infrastructure, and API
- **File-Scoped Namespaces**: Required by .editorconfig (C# 10+)
- **Dependency Injection**: ASP.NET Core built-in DI container

### Central Package Management

This project uses Central Package Management (CPM). Package versions are defined in `/src/AzureEventGridSimulator/src/Directory.Packages.props` and referenced without versions in `.csproj` files.

## Code Style

### Language and Formatting

- **Target Framework**: .NET 10.0 (`net10.0`)
- **Language Version**: Latest C# features enabled
- **Nullable**: Disabled project-wide
- **Formatter**: CSharpier (100 character line width)
- **Line Endings**: CRLF (Windows-style)
- **Encoding**: UTF-8 with BOM
- **Indentation**: 4 spaces for C#, 2 spaces for JSON/XML

### C# Conventions (from .editorconfig)

- **Namespaces**: File-scoped namespaces required (`warning` level)
- **Usings**: Outside namespace, system directives first
- **var**: Preferred for all variable declarations
- **Expression bodies**: Preferred for properties/indexers/lambdas, block bodies for methods
- **Pattern matching**: Preferred over `is` with cast and `as` with null check
- **Primary constructors**: Preferred for simple classes and records
- **Collection expressions**: Preferred (`[]` syntax)

### Code Organization

- All domain logic in `/Domain` namespace
- Infrastructure concerns in `/Infrastructure` namespace
- API controllers use API versioning (`Asp.Versioning.Mvc`)
- Use `InternalsVisibleTo` for test access (see `Program.cs`)

## Testing

### Test Framework

- **Framework**: xUnit 2.9.3
- **Assertions**: Shouldly (fluent assertions)
- **Mocking**: NSubstitute
- **Integration Tests**: `Microsoft.AspNetCore.Mvc.Testing`

### Test Categories

Tests are organized using xUnit traits:

```csharp
[Trait("Category", "unit")]        // Unit tests
[Trait("Category", "integration")]  // Integration tests
```

Run specific categories using `--filter "Category=unit"`.

### Test Structure

- **UnitTests/**: Fast, isolated tests for individual components
- **IntegrationTests/**: Tests involving multiple components or external dependencies
- **ActualSimulatorTests/**: End-to-end tests running the full simulator

### Writing Tests

```csharp
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.YourFeature;

[Trait("Category", "unit")]
public class YourFeatureTests
{
    [Fact]
    public void Should_DoSomething_When_Condition()
    {
        // Arrange
        var sut = new YourClass();

        // Act
        var result = sut.DoSomething();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(expected);
    }
}
```

## Common Tasks

### Adding a New Topic Configuration

Edit `appsettings.json` or `docker/appsettings.docker.json`:

```json
{
  "topics": [
    {
      "name": "MyNewTopic",
      "port": 60102,
      "key": "YourSecretKey=",
      "subscribers": {
        "http": [
          {
            "name": "MyWebhook",
            "endpoint": "https://example.com/webhook",
            "disableValidation": false
          }
        ]
      }
    }
  ]
}
```

### Testing Event Publishing

```bash
# Using cURL (EventGrid schema)
curl -k \
  -H "Content-Type: application/json" \
  -H "aeg-sas-key: TheLocal+DevelopmentKey=" \
  -X POST "https://localhost:60101/api/events?api-version=2018-01-01" \
  -d '[{"id":"123","subject":"/test","data":{},"eventType":"Test.Event","eventTime":"2025-01-01T00:00:00Z","dataVersion":"1"}]'

# Using cURL (CloudEvents structured mode)
curl -k \
  -H "Content-Type: application/cloudevents+json" \
  -H "aeg-sas-key: TheLocal+DevelopmentKey=" \
  -X POST "https://localhost:60101/api/events?api-version=2018-01-01" \
  -d '{"specversion":"1.0","type":"com.example.test","source":"/test","id":"123","data":{}}'
```

### Debugging with Logs

The simulator uses Serilog with console and file sinks. Logs are written to `log_YYYYMMDD.txt` in the application directory.

Configure log levels in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

For Docker environments, use Seq (included in docker-compose) at `http://localhost:8081`.

### Generating SSL Certificates

For local development:

```bash
dotnet dev-certs https --trust
```

For Docker:

```bash
dotnet dev-certs https \
  --export-path /src/AzureEventGridSimulator/docker/azureEventGridSimulator.pfx \
  --password Y0urSup3rCrypt1cPa55w0rd!
```

### Adding a New Subscriber Type

1. Create delivery service in `/Domain/Services/Delivery/`
2. Implement `ISubscriberEventDeliveryHandler` interface
3. Register service in `Program.cs` DI configuration
4. Add configuration model in `/Infrastructure/Settings/`
5. Update configuration parsing in settings classes

## Important Considerations

### HTTPS Only

Azure Event Grid only accepts HTTPS connections. The simulator enforces HTTPS for all topic endpoints. The `Microsoft.Azure.EventGrid` client library will always use HTTPS regardless of the URL scheme provided.

### Topic Endpoint Pattern

- Azure Event Grid pattern: `https://topic-name.location.eventgrid.azure.net/api/events`
- Simulator pattern: `https://localhost:{port}/api/events?api-version=2018-01-01`
- Each topic uses a unique port to distinguish between topics
- The EventGrid client reduces URLs to `https://host:port` and drops query strings

### Authentication Validation

- If a topic has a `key` configured, requests must include `aeg-sas-key` or `aeg-sas-token` header
- Set `key` to `null` to disable authentication for a topic
- SAS tokens must be generated according to Azure Event Grid specifications

### Message Size Limits

- Overall message body: ≤ 1,048,576 bytes (1 MB)
- Individual event: ≤ 1,048,576 bytes (1 MB)
- Enforced in validation middleware

### Event Schemas

- **EventGrid schema**: Azure's proprietary format (default)
- **CloudEvents v1.0**: CNCF standard, supports structured and binary content modes
- Schema can be auto-detected, explicitly set per topic, or transformed on output
- See `Domain/Services/EventSchemaDetector.cs` for detection logic

### Subscriber Validation

The simulator mimics Azure Event Grid's subscription validation handshake:

1. On startup, sends validation event to each subscriber
2. Expects `validationCode` echo response
3. Disables subscriber if validation fails (unless `disableValidation: true`)

### Event Filtering

Advanced filters support up to 25 conditions per subscriber. String comparisons are case-insensitive. "Not" operators return `true` when the key doesn't exist. Dot notation supports nested data property access.

### CI/CD

GitHub Actions workflows in `.github/workflows/`:

- **ci.yml**: Multi-platform build and test (Windows, Linux, macOS)
- **release.yml**: GitHub releases with binaries
- **docker-release.yml**: Docker Hub image publishing

Builds require warnings-as-errors to pass (`TreatWarningsAsErrors=true`).

### Build-Time Code Formatting

CSharpier runs automatically before every build of the main project. To skip formatting (e.g., in Docker builds), set `DesignTimeBuild=true`:

```bash
dotnet build -p:DesignTimeBuild=true
```

### No Nullable Reference Types

This project has nullable reference types disabled (`<Nullable>disable</Nullable>`). Do not add nullable annotations (`?`, `!`) to code.

### Custom Mediator Implementation

The project previously used MediatR but now uses a custom in-process mediator (`Infrastructure/Mediator/`). This is due to trimming and single-file publishing constraints. Command handlers implement `ICommandHandler<TCommand, TResult>`.

### Testing Against Real Azure Service Bus

Integration tests can use real Azure Service Bus if connection strings are provided via environment variables. Otherwise, use the Service Bus emulator included in docker-compose.

### Branch Strategy

- Main branch: `master`
- Active development may occur on feature branches
- CI runs on `master`, `main`, and pull requests

### Commit Message Convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/) for automated versioning via Release Please. Commit messages must follow this format:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types that trigger version bumps:**

| Type | Description | Version Bump |
|------|-------------|--------------|
| `feat` | New feature | Minor (4.1.0 → 4.2.0) |
| `fix` | Bug fix | Patch (4.1.0 → 4.1.1) |
| `feat!` or `fix!` | Breaking change (note the `!`) | Major (4.1.0 → 5.0.0) |

**Types that do NOT trigger version bumps:**

- `docs` - Documentation changes
- `style` - Formatting, whitespace
- `refactor` - Code restructuring without behavior change
- `perf` - Performance improvements
- `test` - Adding or updating tests
- `build` - Build system or dependencies
- `ci` - CI/CD configuration
- `chore` - Other maintenance tasks

**Examples:**

```bash
feat: add Azure Storage Queue subscriber support
fix: correct SAS token validation for special characters
feat!: change configuration schema for multi-topic setup
docs: update README with Docker instructions
chore: update NuGet dependencies
```

### Release Process

Releases are automated via Release Please:

1. Push commits to `master` using conventional commit messages
2. Release Please creates/updates a "Release PR" with changelog
3. Merge the Release PR to trigger:
   - Git tag creation (e.g., `4.2.0`)
   - GitHub Release with generated changelog
   - NuGet package publish
   - Docker image build and push
   - Platform binaries upload

## Documentation Maintenance

When making changes to the codebase, keep documentation in sync:

- **AGENTS.md**: Update when changing architecture, conventions, build processes, or development workflows
- **README.md**: Update when changing user-facing features, configuration options, or usage instructions
- **DOCKER.md**: Update when changing Docker configuration, environment variables, or container behavior
- **.github/copilot-instructions.md**: Update when changing code style conventions or project patterns

Documentation should be updated in the same commit or PR as the related code changes.
