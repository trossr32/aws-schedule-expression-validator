# AGENTS.md for aws-cron-ai-test

This file provides guidelines and commands for agents working on the aws-cron-ai-test repository, a .NET solution for validating AWS EventBridge Scheduler CRON expressions.

## Project Overview

- **Solution**: AwsCronValidator.sln
- **NuGet Package**: AwsCronValidator (class library for AWS EventBridge schedule expression validation: CRON, rate, at)
- **Demo App**: AwsCronValidatorDemo (Blazor WebAssembly for web-based validation)
- **Tests**: AwsCronValidator.Tests (xUnit tests)
- **Target Framework**: .NET 8.0
- **Deployment**: GitHub Pages for demo app

## Build/Lint/Test Commands

### Build
- Build entire solution: `dotnet build`
- Build specific project: `dotnet build <project>.csproj`
- Publish NuGet package: `dotnet pack AwsCronValidator/AwsCronValidator.csproj -c Release`

### Test
- Run all tests: `dotnet test`
- Run single test: `dotnet test --filter "FullyQualifiedName~TestName"`
  Example: `dotnet test --filter "FullyQualifiedName~AwsCronValidatorTests.ValidateFormat_ValidCrons_ReturnsTrue"`
- Run tests with coverage: Install coverlet and run `dotnet test /p:CollectCoverage=true`

### Lint
- No dedicated linter; rely on compiler warnings/errors.
- For code analysis: `dotnet build /warnaserror`
- Formatting: `dotnet format` (if dotnet-format installed)

### Other Commands
- Restore packages: `dotnet restore`
- Clean: `dotnet clean`
- Run Blazor demo locally: `dotnet run --project AwsCronValidatorDemo`
- Publish Blazor for GitHub Pages: `dotnet publish AwsCronValidatorDemo -c Release -o publish`

## Code Style Guidelines

### Imports
- Use `using` directives at top of file.
- Group: System namespaces first, then external packages, then project namespaces.
- No wildcard imports (`using System.*;`).

### Formatting
- Use 4 spaces indentation (default in .csproj).
- Max line length: 120 characters.
- Consistent brace placement: Allman style (braces on new line).

### Types
- Use `var` for implicit typing where obvious.
- Prefer explicit types for public APIs.
- Nullable reference types enabled: Use `?` for nullable refs, `!` for null-forgiving.

### Naming Conventions
- Classes: PascalCase (e.g., `AwsCronValidator`)
- Methods/Properties: PascalCase (e.g., `ValidateFormat`)
- Fields: camelCase with underscore prefix (e.g., `_minInterval`)
- Constants: UPPER_SNAKE_CASE
- Local variables: camelCase
- Interfaces: PascalCase with I prefix (e.g., `ICronValidator`)

### Error Handling
- Use try-catch for expected exceptions (e.g., invalid schedule expressions).
- Throw custom exceptions with descriptive messages.
- Avoid empty catch blocks; log or rethrow.
- Validate inputs early with ArgumentException for invalid args.

### Async/Await
- Use async/await for I/O operations.
- Avoid async void; use async Task.
- ConfigureAwait(false) for library code.

### Security
- No secrets in code; use environment variables or config.
- Validate all inputs to prevent injection.
- Use parameterized queries if database involved (not applicable here).

### Architecture
- Separation of concerns: Validation logic in static methods, UI in Blazor components.
- Dependency injection for services in Blazor.
- SOLID principles: Single responsibility (e.g., validator class separate from UI).

### Comments
- XML comments for public APIs.
- Inline comments for complex logic.
- No over-commenting obvious code.

## FluentValidation Integration
- Use `RuleFor(x => x.ScheduleField).MustBeValidAwsSchedule()` for format validation.
- Use `RuleFor(x => x.ScheduleField).MustBeValidAwsSchedule(TimeSpan.FromHours(1))` for min interval validation.
- Use `RuleFor(x => x.ScheduleField).MustBeValidAwsSchedule(null, TimeSpan.FromHours(2))` for max interval validation.
- Use `RuleFor(x => x.ScheduleField).MustBeValidAwsSchedule(TimeSpan.FromHours(1), TimeSpan.FromHours(24))` for both min and max.

## Testing
- Write unit tests for all public methods.
- Use xUnit Fact/Theory for test methods.
- Mock dependencies if needed.
- Test edge cases: empty strings, invalid formats, boundary intervals.

## Git Workflow
- Branch naming: feature/, bugfix/, hotfix/
- Commit messages: "feat: add CRON validation", "fix: handle invalid intervals"
- PR reviews required before merge.

## Deployment
- NuGet: Push package to NuGet.org after tagging release.
- GitHub Pages: Automatic via Actions on push to main.
- Versioning: Semantic (1.0.0, 1.1.0, etc.)

## Cursor Rules
(None found in .cursor/rules/ or .cursorrules)

## Copilot Instructions
(None found in .github/copilot-instructions.md)