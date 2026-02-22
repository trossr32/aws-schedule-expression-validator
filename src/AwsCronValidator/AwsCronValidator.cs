using System;
using Quartz;
using FluentValidation;
using FluentValidation.Validators;

namespace AwsCronValidator;

/// <summary>
/// Validator for AWS EventBridge Scheduler schedule expressions (CRON and rate).
/// Supports format validation and minimum timespan checks between executions.
/// </summary>
public class AwsCronValidator
{
    /// <summary>
    /// Validates if the schedule expression is a valid AWS EventBridge format (CRON or rate).
    /// </summary>
    /// <param name="expression">The schedule expression to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateFormat(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        if (expression.StartsWith("rate(", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateRateFormat(expression);
        }
        else if (expression.StartsWith("cron(", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateCronFormat(expression);
        }
        else if (expression.StartsWith("at(", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateAtFormat(expression);
        }

        // Assume CRON without prefix for backward compatibility
        return ValidateCronFormat($"cron({expression})");
    }

    /// <summary>
    /// Validates a CRON expression.
    /// </summary>
    private static bool ValidateCronFormat(string cron)
    {
        if (string.IsNullOrWhiteSpace(cron) || !cron.StartsWith("cron(", StringComparison.OrdinalIgnoreCase) || !cron.EndsWith(")"))
            return false;

        string inner = cron.Substring(5, cron.Length - 6); // Remove "cron(" and ")"

        try
        {
            // AWS EventBridge uses 6-field CRON: minute hour day-of-month month day-of-week year
            // Quartz CronExpression supports this format, prepend 0 for seconds
            string quartzCron = "0 " + inner;
            new CronExpression(quartzCron);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a rate expression.
    /// </summary>
    private static bool ValidateRateFormat(string rate)
    {
        if (string.IsNullOrWhiteSpace(rate) || !rate.StartsWith("rate(", StringComparison.OrdinalIgnoreCase) || !rate.EndsWith(")"))
            return false;

        string inner = rate.Substring(5, rate.Length - 6); // Remove "rate(" and ")"

        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out int value) || value <= 0)
            return false;

        string unit = parts[1].ToLowerInvariant();
        if (unit == "minute" || unit == "minutes")
        {
            return value >= 1 && value <= 60;
        }
        else if (unit == "hour" || unit == "hours")
        {
            return value >= 1 && value <= 24;
        }
        else if (unit == "day" || unit == "days")
        {
            return value >= 1 && value <= 365;
        }

        return false;
    }

    /// <summary>
    /// Validates an at expression.
    /// </summary>
    private static bool ValidateAtFormat(string at)
    {
        if (string.IsNullOrWhiteSpace(at) || !at.StartsWith("at(", StringComparison.OrdinalIgnoreCase) || !at.EndsWith(")"))
            return false;

        string inner = at.Substring(3, at.Length - 4); // Remove "at(" and ")"

        // yyyy-mm-ddThh:mm:ss
        return DateTime.TryParse(inner, out _);
    }

    /// <summary>
    /// Validates schedule expression format and ensures timespan constraints between executions.
    /// For CRON, checks intervals between occurrences against min/max.
    /// For rate, checks rate interval against min/max.
    /// For at, validates only if no min/max constraints (one-time has no intervals).
    /// </summary>
    /// <param name="expression">The schedule expression.</param>
    /// <param name="minInterval">Minimum allowed timespan between executions (optional).</param>
    /// <param name="maxInterval">Maximum allowed timespan between executions (optional).</param>
    /// <returns>True if valid format and respects constraints, false otherwise.</returns>
    public static bool ValidateWithIntervals(string expression, TimeSpan? minInterval = null, TimeSpan? maxInterval = null)
    {
        if (!ValidateFormat(expression))
            return false;

        // Validate constraints
        if (minInterval.HasValue && minInterval.Value <= TimeSpan.Zero)
            return false;
        if (maxInterval.HasValue && maxInterval.Value <= TimeSpan.Zero)
            return false;
        if (minInterval.HasValue && maxInterval.HasValue && minInterval.Value > maxInterval.Value)
            return false;

        if (expression.StartsWith("rate(", StringComparison.OrdinalIgnoreCase))
        {
            // For rate, compute the interval and check against constraints
            var interval = GetRateInterval(expression);
            if (minInterval.HasValue && interval < minInterval.Value) return false;
            if (maxInterval.HasValue && interval > maxInterval.Value) return false;
            return true;
        }
        else if (expression.StartsWith("at(", StringComparison.OrdinalIgnoreCase))
        {
            // At is one-time, no intervals, so valid only if no constraints
            return !minInterval.HasValue && !maxInterval.HasValue;
        }
        else
        {
            // CRON
            try
            {
                string cron = expression.StartsWith("cron(", StringComparison.OrdinalIgnoreCase) ? expression : $"cron({expression})";
                string inner = cron.Substring(5, cron.Length - 6);
                string quartzCron = "0 " + inner;
                var expr = new CronExpression(quartzCron);
                var now = DateTimeOffset.UtcNow;

                // Get next 10 occurrences to check intervals
                var occurrences = new List<DateTimeOffset>();
                var current = now;
                for (int i = 0; i < 10; i++)
                {
                    var next = expr.GetNextValidTimeAfter(current);
                    if (next == null) break;
                    occurrences.Add(next.Value);
                    current = next.Value;
                }

                if (occurrences.Count < 2)
                    return !minInterval.HasValue && !maxInterval.HasValue; // If less than 2, valid only if no constraints

                for (int i = 1; i < occurrences.Count; i++)
                {
                    var interval = occurrences[i] - occurrences[i - 1];
                    if (minInterval.HasValue && interval < minInterval.Value) return false;
                    if (maxInterval.HasValue && interval > maxInterval.Value) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public static bool ValidateWithMinInterval(string expression, TimeSpan minInterval)
    {
        return ValidateWithIntervals(expression, minInterval, null);
    }

    /// <summary>
    /// Gets the interval for a rate expression.
    /// </summary>
    private static TimeSpan GetRateInterval(string rate)
    {
        string inner = rate.Substring(5, rate.Length - 6);
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int value = int.Parse(parts[0]);
        string unit = parts[1].ToLowerInvariant();

        return unit switch
        {
            "minute" or "minutes" => TimeSpan.FromMinutes(value),
            "hour" or "hours" => TimeSpan.FromHours(value),
            "day" or "days" => TimeSpan.FromDays(value),
            _ => TimeSpan.MaxValue
        };
    }
}

/// <summary>
/// FluentValidation validator for AWS EventBridge schedule expressions.
/// </summary>
public class AwsScheduleExpressionValidator<T> : PropertyValidator<T, string>
{
    private readonly TimeSpan? _minInterval;
    private readonly TimeSpan? _maxInterval;

    public AwsScheduleExpressionValidator(TimeSpan? minInterval = null, TimeSpan? maxInterval = null)
    {
        _minInterval = minInterval;
        _maxInterval = maxInterval;
    }

    public override string Name => "AwsScheduleExpressionValidator";

    public override bool IsValid(ValidationContext<T> context, string value)
    {
        return AwsCronValidator.ValidateWithIntervals(value, _minInterval, _maxInterval);
    }

    protected override string GetDefaultMessageTemplate(string errorCode)
    {
        if (_minInterval.HasValue && _maxInterval.HasValue)
            return "Invalid schedule expression or violates interval constraints (min: {MinInterval}, max: {MaxInterval}).";
        else if (_minInterval.HasValue)
            return "Invalid schedule expression or violates minimum interval of {MinInterval}.";
        else if (_maxInterval.HasValue)
            return "Invalid schedule expression or violates maximum interval of {MaxInterval}.";
        else
            return "Invalid schedule expression.";
    }
}

/// <summary>
/// Extension methods for FluentValidation.
/// </summary>
public static class AwsScheduleValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidAwsSchedule<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.SetValidator(new AwsScheduleExpressionValidator<T>());
    }

    public static IRuleBuilderOptions<T, string> MustBeValidAwsSchedule<T>(this IRuleBuilder<T, string> ruleBuilder, TimeSpan minInterval)
    {
        return ruleBuilder.SetValidator(new AwsScheduleExpressionValidator<T>(minInterval, null));
    }

    public static IRuleBuilderOptions<T, string> MustBeValidAwsSchedule<T>(this IRuleBuilder<T, string> ruleBuilder, TimeSpan? minInterval, TimeSpan? maxInterval)
    {
        return ruleBuilder.SetValidator(new AwsScheduleExpressionValidator<T>(minInterval, maxInterval));
    }
}
