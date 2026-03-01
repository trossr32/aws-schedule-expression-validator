# AWS Schedule Expression Validator

[![NuGet](https://img.shields.io/nuget/v/AwsScheduleExpressionValidator.svg)](https://www.nuget.org/packages/AwsScheduleExpressionValidator) <br />
[![GitHub Packages](https://img.shields.io/badge/GitHub%20Packages-AwsScheduleExpressionValidator-1082c3)](https://github.com/trossr32?tab=packages) <br />
[![CI](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/ci.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/ci.yml)
[![Publish NuGet Package](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-package.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-package.yml)
[![Publish DotNet Tool](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-tool.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-tool.yml)
[![Publish GitHub Package](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-github.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-github.yml)
[![Publish Powershell Module](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-powershell.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-powershell.yml)
[![Deploy GitHub Pages](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/deploy-pages.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/deploy-pages.yml)

Validate AWS EventBridge Scheduler expressions (`cron`, `rate`, and `at`) and optionally enforce minimum/maximum intervals between runs. Includes FluentValidation integrations and a Blazor WebAssembly demo.

- Demo: https://trossr32.github.io/aws-schedule-expression-validator/
- Repository: https://github.com/trossr32/aws-schedule-expression-validator

## Features

- Validate `cron(...)`, `rate(...)`, and `at(...)` expressions
- Accepts bare CRON fields for backward compatibility
- Validate interval constraints (min/max) between occurrences
- Retrieve upcoming execution times
- Fluent API
- FluentValidation rule extensions
- Blazor WebAssembly demo deployed to GitHub Pages
- Packages/Tools published to NuGet.org and GitHub Packages
- PowerShell module published to PowerShell Gallery

## NuGet Package

```bash
dotnet add package AwsScheduleExpressionValidator
```

## Usage

### Fluent validation API

```csharp
using AwsScheduleExpressionValidator;

var result = "rate(5 minutes)"
    .ValidateAwsScheduleExpression()
    .WithMinInterval(TimeSpan.FromMinutes(1))
    .WithMaxInterval(TimeSpan.FromMinutes(10))
    .Evaluate();

var isValid = "cron(0 10 * * ? *)"
    .ValidateAwsScheduleExpression()
    .IsValid();

var occurrences = "rate(1 minutes)"
    .ValidateAwsScheduleExpression()
    .WithOccurrenceCount(3)
    .WithOccurrenceStart(DateTimeOffset.UtcNow)
    .GetNextScheduleExpressionOccurrences();

"invalid"
    .ValidateAwsScheduleExpression()
    .ThrowExceptionOnFailure()
    .Evaluate();
```

#### Fluent API reference

- `ValidateAwsScheduleExpression()`
  - Starts validation for a schedule expression string.
- `WithMinInterval(TimeSpan)`
  - Sets a minimum interval constraint.
- `WithMaxInterval(TimeSpan)`
  - Sets a maximum interval constraint.
- `WithOccurrenceCount(int)`
  - Sets the number of occurrences to return for `GetNextScheduleExpressionOccurrences()`.
- `WithOccurrenceStart(DateTimeOffset)`
  - Sets the starting point for occurrence calculation.
- `ThrowExceptionOnFailure()`
  - Throws `AwsScheduleExpressionFormatException`, `AwsScheduleIntervalConfigurationException`, or `AwsScheduleIntervalViolationException` when validation fails.
- `IsValid()`
  - Returns `true`/`false` for the current configuration. Use this or `Evaluate()` to get validation results.
- `Evaluate()`
  - Returns `AwsScheduleExpressionValidationResult` with `IsValid`, `Error`, and `Message`. Use this or `IsValid()` to get validation results.
- `GetNextScheduleExpressionOccurrences()`
  - Returns upcoming execution times using the configured occurrence settings.

### Format validation

```csharp
using AwsScheduleExpressionValidator;

// Valid CRON expression
var isValid = AwsScheduleExpressionValidator.ValidateFormat("cron(0 10 * * ? *)");
var isValid = AwsScheduleExpressionValidator.ValidateFormat("0 10 * * ? *");

// Valid rate expression
var isValid = AwsScheduleExpressionValidator.ValidateFormat("rate(5 minutes)");

// Valid at expression
var isValid = AwsScheduleExpressionValidator.ValidateFormat("at(2024-12-31T23:59:00)");
```

### Validate with interval constraints

```csharp
using AwsScheduleExpressionValidator;

var minInterval = TimeSpan.FromHours(1);
var maxInterval = TimeSpan.FromDays(1);

var isValid = AwsScheduleExpressionValidator.ValidateWithIntervals("rate(5 minutes)", minInterval, maxInterval);
```

### Get upcoming executions

```csharp
using AwsScheduleExpressionValidator;

var nextRuns = AwsScheduleExpressionValidator.GetNextOccurrences("cron(0 10 * * ? *)", count: 5);

foreach (var run in nextRuns)
{
    Console.WriteLine(run.ToLocalTime().ToString("f"));
}
```

### FluentValidation integration

```csharp
using AwsScheduleExpressionValidator;
using FluentValidation;

public class ScheduleRequest
{
    public string Expression { get; set; } = string.Empty;
}

public class ScheduleRequestValidator : AbstractValidator<ScheduleRequest>
{
    public ScheduleRequestValidator()
    {
        // Basic format validation
        RuleFor(x => x.Expression)
            .MustBeValidAwsSchedule();

        // Validate with a minimum interval of 5 minutes
        RuleFor(x => x.Expression)
            .MustBeValidAwsSchedule(TimeSpan.FromMinutes(5));
            
        // Validate with a maximum interval of 24 hours
        RuleFor(x => x.Expression)
            .MustBeValidAwsSchedule(null, TimeSpan.FromHours(24));
            
        // Validate with a minimum interval of 5 minutes and a maximum interval of 24 hours
        RuleFor(x => x.Expression)
            .MustBeValidAwsSchedule(TimeSpan.FromMinutes(5), TimeSpan.FromHours(24));
    }
}
```

## CLI Tool

Install the .NET tool:

```bash
dotnet tool install -g AwsScheduleExpressionValidator.Tool
```

Examples:

```bash
aws-schedule-expression-validator format "rate(5 minutes)"
aws-schedule-expression-validator validate "cron(0 10 * * ? *)" --min 00:05:00 --max 01:00:00
aws-schedule-expression-validator occurrences "rate(5 minutes)" --count 3
```

Commands:

- `format <expression>`: Validates the expression format only.
- `validate <expression> [--min <timespan>] [--max <timespan>]`: Validates format and interval constraints.
- `occurrences <expression> [--count <number>] [--start <iso-8601>]`: Lists upcoming occurrences.

Options:

- `--min`: Minimum interval (TimeSpan, e.g. `00:05:00`).
- `--max`: Maximum interval (TimeSpan, e.g. `01:00:00`).
- `--count`: Number of occurrences to return (default 5).
- `--start`: Start date/time (ISO 8601).

## PowerShell module commands

Install the PowerShell module from the PowerShell Gallery and use the cmdlets to validate expressions or list occurrences.

```powershell
Install-Module AwsScheduleExpressionValidator.PsModule -Scope CurrentUser
Import-Module AwsScheduleExpressionValidator.PsModule
```

### Test-AwsScheduleExpressionFormat

Tests whether a schedule expression has a valid AWS format.

```powershell
Test-AwsScheduleExpressionFormat -Expression 'rate(5 minutes)'
```

### Test-AwsScheduleExpression

Validates a schedule expression and optionally enforces minimum and maximum interval constraints.

```powershell
Test-AwsScheduleExpression -Expression 'rate(5 minutes)' -MinInterval '00:01:00' -MaxInterval '01:00:00'
```

### Get-AwsScheduleExpressionOccurrence

Returns upcoming occurrences for a schedule expression, optionally starting at a specified time.

```powershell
Get-AwsScheduleExpressionOccurrence -Expression 'rate(5 minutes)' -Count 3 -Start '2024-01-01T00:00:00Z'
```

## Tests

Run the unit tests from the solution root:

```bash
dotnet test src/AwsScheduleExpressionValidator.Tests/AwsScheduleExpressionValidator.Tests.csproj
```

## Development

The solution includes a Blazor WebAssembly demo (`src/AwsScheduleExpressionValidatorDemo`) deployed to GitHub Pages. To run locally:

```bash
dotnet run --project src/AwsScheduleExpressionValidatorDemo/AwsScheduleExpressionValidatorDemo.csproj
```

## Notes

- For `at(...)` expressions, interval constraints are not applicable (one-time schedules).
- When no `cron(...)` prefix is provided, the validator assumes the input is a CRON expression.

