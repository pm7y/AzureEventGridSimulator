# Copilot Instructions for Azure Event Grid Simulator

## Project Overview

This is a .NET 10.0 ASP.NET Core Web API that simulates Azure Event Grid for local development. It provides HTTPS endpoints mimicking Event Grid topics with support for HTTP webhooks, Azure Service Bus, and Azure Storage Queue subscribers.

## Code Style Requirements

### C# Conventions

- Use **file-scoped namespaces** (required, warning-level enforcement)
- Prefer **var** for all variable declarations
- Use **expression bodies** for properties, indexers, and lambdas; use **block bodies** for methods and constructors
- Prefer **pattern matching** over `is` with cast or `as` with null check
- System usings first, placed outside namespace
- 4-space indentation for C#, 2-spaces for JSON/XML

### Modern C# Features

- **Nullable reference types are DISABLED** - do not add `?` or `!` annotations
- **Primary constructors** - prefer for simple classes and records
- **Collection expressions** - prefer `[]` syntax for collections
- CSharpier formats code automatically on build (100 char line width)
- Line endings are CRLF (Windows-style)

### Naming Conventions

- Classes/Methods/Properties: PascalCase
- Interfaces: PascalCase with `I` prefix
- Parameters: camelCase
- Private fields: _camelCase (with underscore prefix)
- Constants: PascalCase

## Architecture

### Project Structure

```
/src/AzureEventGridSimulator/          # Main application
    /Controllers/                       # API endpoints
    /Domain/Commands/                   # Command handlers (CQRS-lite)
    /Domain/Entities/                   # Domain models
    /Domain/Services/                   # Business logic
    /Infrastructure/Extensions/         # Extension methods
    /Infrastructure/Mediator/           # Custom mediator (not MediatR)
    /Infrastructure/Middleware/         # HTTP middleware
    /Infrastructure/Settings/           # Configuration models
/src/AzureEventGridSimulator.Tests/    # Test project
    /UnitTests/                         # Fast isolated tests
    /IntegrationTests/                  # Multi-component tests
```

### Key Patterns

- **Custom Mediator**: Uses reflection-based handler discovery (no MediatR dependency for trimming compatibility)
- **Central Package Management**: Versions in `Directory.Packages.props`, no versions in `.csproj` files
- **InternalsVisibleTo**: Test project can access internal members

## Testing

### Framework

- xUnit for test framework
- Shouldly for fluent assertions
- NSubstitute for mocking

### Test Categories

Use xUnit traits for categorization:
```csharp
[Trait("Category", "unit")]        // Unit tests
[Trait("Category", "integration")]  // Integration tests
```

### Test Style

```csharp
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
```

## Build Commands

```bash
# Build
dotnet build src/AzureEventGridSimulator.sln --configuration Release

# Run all tests
dotnet test src/AzureEventGridSimulator.sln --configuration Release

# Run unit tests only
dotnet test src/AzureEventGridSimulator.sln --filter "Category=unit"

# Format code
dotnet csharpier format src
```

## Commit Messages

Use Conventional Commits format:
- `feat:` - New feature (minor version bump)
- `fix:` - Bug fix (patch version bump)
- `feat!:` or `fix!:` - Breaking change (major version bump)
- `docs:`, `style:`, `refactor:`, `test:`, `chore:` - No version bump

## Documentation Maintenance

When making changes, keep documentation in sync:

- **AGENTS.md**: Architecture, conventions, build processes, workflows
- **README.md**: User-facing features, configuration, usage
- **DOCKER.md**: Docker configuration, environment variables
- **copilot-instructions.md**: Code style conventions, project patterns

Update documentation in the same commit or PR as related code changes.

## Additional Context

For comprehensive documentation including subscriber configuration, event schemas, Docker setup, and advanced filtering, see the `AGENTS.md` file in the repository root.
