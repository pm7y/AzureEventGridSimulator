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
dotnet test src/AzureEventGridSimulator.slnx --filter "Category!=integration-actual"    # the main CI test step (all three OSes)
dotnet test src/AzureEventGridSimulator.slnx --filter "Category=unit"
dotnet test src/AzureEventGridSimulator.slnx --filter "Category=integration"

# Run
dotnet run --project src/AzureEventGridSimulator/AzureEventGridSimulator.csproj

# Format (runs automatically on build)
dotnet csharpier format src
```

**Test categories:** `unit`; `integration` (no Docker or external services needed; `IntegrationTests/` hosts the app in-process with `WebApplicationFactory`); `integration-actual` (starts the built simulator on `https://localhost:60101`, so it clashes with anything already listening on that port). CI runs `Category!=integration-actual` on Windows, Linux and macOS, runs `integration-actual` on the ubuntu leg only (after `dotnet dev-certs https`), and runs the Postman parity suite in a separate job.

**Parity tests:** `src/postman/` holds a Postman/newman collection designed to run against both real Azure Event Grid and the simulator. See [Using Postman](https://github.com/pm7y/AzureEventGridSimulator/wiki/Schema-Support#using-postman).

## Project Structure

The source tree is documented on the wiki: [Architecture: Source Code Structure](https://github.com/pm7y/AzureEventGridSimulator/wiki/Architecture#source-code-structure).

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
