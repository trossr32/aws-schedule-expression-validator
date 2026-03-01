# AWS Schedule Expression Validator CLI

Validate AWS EventBridge Scheduler expressions from the command line.

## Install

```bash
dotnet tool install -g AwsScheduleExpressionValidator.Tool
```

## Usage

```bash
aws-schedule-expression-validator format "rate(5 minutes)"
aws-schedule-expression-validator validate "cron(0 10 * * ? *)" --min 00:05:00 --max 01:00:00
aws-schedule-expression-validator occurrences "rate(5 minutes)" --count 3
```

## Commands

- `format <expression>`: Validates the expression format only.
- `validate <expression> [--min <timespan>] [--max <timespan>]`: Validates format and interval constraints.
- `occurrences <expression> [--count <number>] [--start <iso-8601>]`: Lists upcoming occurrences.

## Options

- `--min`: Minimum interval (TimeSpan, e.g. `00:05:00`).
- `--max`: Maximum interval (TimeSpan, e.g. `01:00:00`).
- `--count`: Number of occurrences to return (default 5).
- `--start`: Start date/time (ISO 8601).
