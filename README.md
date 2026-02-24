# AWS Schedule Expression Validator

[![CI](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/ci.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/ci.yml)
[![Publish Package](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-package.yml/badge.svg)](https://github.com/trossr32/aws-schedule-expression-validator/actions/workflows/publish-package.yml)
[![NuGet](https://img.shields.io/nuget/v/AwsScheduleExpressionValidator.svg)](https://www.nuget.org/packages/AwsScheduleExpressionValidator)
[![GitHub Packages](https://img.shields.io/badge/GitHub%20Packages-AwsScheduleExpressionValidator-24292f)](https://github.com/trossr32?tab=packages)

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
- Packages published to NuGet.org and GitHub Packages

## Packages

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
        RuleFor(x => x.Expression)
            .MustBeValidAwsSchedule();

        RuleFor(x => x.Expression)
            .MustBeValidAwsSchedule(TimeSpan.FromMinutes(5));

        RuleFor(x => x.Expression)
            .MustBeValidAwsSchedule(TimeSpan.FromMinutes(5), TimeSpan.FromHours(24));
    }
}
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
