# Contributing to Azure Event Grid Simulator

Thank you for your interest in contributing to Azure Event Grid Simulator! This document provides guidelines and information for contributors.

> **Note:** This simulator is intended for **local development and testing only**. When contributing, keep in mind that features should support local development scenarios, not production use cases.

## Getting Started

### Prerequisites

- .NET 10.0 SDK (required by global.json to build)
- Git
- Your favorite IDE (Visual Studio, VS Code, Rider, etc.)

> **Note:** While .NET 10.0 SDK is required to build, the project multi-targets .NET 8.0, 9.0, and 10.0 to support users on any of these runtimes.

### Setting Up the Development Environment

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/AzureEventGridSimulator.git
   cd AzureEventGridSimulator
   ```
3. Restore dependencies:
   ```bash
   dotnet restore src/AzureEventGridSimulator.sln
   ```
4. Build the project:
   ```bash
   dotnet build src/AzureEventGridSimulator.sln
   ```
5. Run tests:
   ```bash
   dotnet test src/AzureEventGridSimulator.sln
   ```
Git hooks are configured automatically on first build to validate commit messages follow conventional commit format.

## Code Style

This project uses [CSharpier](https://csharpier.com/) for code formatting. CSharpier runs automatically on every build, so your code will be formatted before compilation.

To manually format the code:
```bash
dotnet tool restore
dotnet csharpier format src
```

## Making Changes

### Branch Naming

Create a descriptive branch name for your changes:
- `feature/add-new-subscriber-type`
- `fix/validation-error-handling`
- `docs/update-readme`

### Commit Messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/). Please format your commit messages as:

- `feat:` - A new feature
- `fix:` - A bug fix
- `docs:` - Documentation changes
- `chore:` - Maintenance tasks
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests

Example: `feat: add Azure Storage Queue subscriber support`

### Pull Requests

1. Ensure your code builds without errors
2. Ensure all tests pass
3. Update documentation if needed
4. Fill out the pull request template
5. Link any related issues

## Testing

- Write tests for new functionality
- Ensure existing tests pass before submitting a PR
- Run the full test suite:
  ```bash
  dotnet test src/AzureEventGridSimulator.sln
  ```

## Reporting Issues

- Use the GitHub issue templates for bug reports and feature requests
- Search existing issues before creating a new one
- Provide as much detail as possible

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](../CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Questions?

If you have questions, feel free to open a discussion or issue on GitHub.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
