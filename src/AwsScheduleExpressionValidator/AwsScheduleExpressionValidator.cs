using Quartz;
using FluentValidation;
using FluentValidation.Validators;

namespace AwsScheduleExpressionValidator;

/// <summary>
/// Validator for AWS EventBridge Scheduler schedule expressions (CRON and rate).
/// Supports format validation and minimum timespan checks between executions.
/// </summary>
public static class AwsScheduleExpressionValidator
{
    /// <summary>
    /// Validates if the schedule expression is a valid AWS EventBridge format (CRON, rate, or at).
    /// Should be one of the following formats:
    /// <list type="bullet">
    /// <item><description>cron({cron expression})</description></item>
    /// <item><description>{cron expression}</description></item>
    /// <item><description>rate({rate expression})</description></item>
    /// <item><description>at({at expression})</description></item>
    /// </list>
    /// </summary>
    /// <param name="expression">The schedule expression to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateFormat(string expression) =>
        expression switch
        {
            var x when string.IsNullOrWhiteSpace(x) => false,
            var x when x.StartsWith("rate(", StringComparison.OrdinalIgnoreCase) => ValidateRateFormat(x),
            var x when x.StartsWith("at(", StringComparison.OrdinalIgnoreCase) => ValidateAtFormat(x),
            var x when x.StartsWith("cron(", StringComparison.OrdinalIgnoreCase) => ValidateCronFormat(x),

            // Assume CRON without prefix for backward compatibility
            _ => ValidateCronFormat($"cron({expression})")
        };

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

            return (!minInterval.HasValue || interval >= minInterval.Value) && (!maxInterval.HasValue || interval <= maxInterval.Value);
        }

        if (expression.StartsWith("at(", StringComparison.OrdinalIgnoreCase))
            // At is one-time, no intervals, so valid only if no constraints
            return !minInterval.HasValue && !maxInterval.HasValue;

        // CRON
        if (!minInterval.HasValue && !maxInterval.HasValue)
            return true;

        try
        {
            if (!TryCreateQuartzExpression(expression, out var expr))
                return false;

            var now = DateTimeOffset.UtcNow;

            // Get next 10 occurrences to check intervals
            var occurrences = new List<DateTimeOffset>();
            var current = now;

            for (var i = 0; i < 10; i++)
            {
                var next = expr.GetNextValidTimeAfter(current);

                if (next == null)
                    break;

                occurrences.Add(next.Value);
                current = next.Value;
            }

            if (occurrences.Count < 2)
                return !minInterval.HasValue && !maxInterval.HasValue; // If less than 2, valid only if no constraints

            for (var i = 1; i < occurrences.Count; i++)
            {
                var interval = occurrences[i] - occurrences[i - 1];

                if (minInterval.HasValue && interval < minInterval.Value)
                    return false;

                if (maxInterval.HasValue && interval > maxInterval.Value)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the next expected execution times for a schedule expression.
    /// </summary>
    /// <param name="expression">The schedule expression.</param>
    /// <param name="count">Maximum number of occurrences to return.</param>
    /// <param name="start">Optional start time to calculate from.</param>
    /// <returns>List of upcoming execution times (UTC).</returns>
    public static IReadOnlyList<DateTimeOffset> GetNextOccurrences(string expression, int count = 5, DateTimeOffset? start = null)
    {
        if (count <= 0 || !ValidateFormat(expression))
            return [];

        var from = start ?? DateTimeOffset.UtcNow;

        if (expression.StartsWith("rate(", StringComparison.OrdinalIgnoreCase))
        {
            var interval = GetRateInterval(expression);
            var occurrences = new List<DateTimeOffset>(count);
            var current = from;

            for (var i = 0; i < count; i++)
            {
                current = current.Add(interval);
                occurrences.Add(current);
            }

            return occurrences;
        }

        if (expression.StartsWith("at(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = expression.Substring(3, expression.Length - 4);

            if (!DateTimeOffset.TryParse(inner, out var atTime))
                return [];

            return atTime >= from
                ? new[] { atTime }
                : Array.Empty<DateTimeOffset>();
        }

        if (!TryCreateQuartzExpression(expression, out var expr))
            return [];

        var results = new List<DateTimeOffset>(count);
        var cursor = from;

        for (var i = 0; i < count; i++)
        {
            var next = expr.GetNextValidTimeAfter(cursor);

            if (next == null)
                break;

            results.Add(next.Value);
            cursor = next.Value;
        }

        return results;
    }

    /// <summary>
    /// Validates a CRON expression.
    /// </summary>
    private static bool ValidateCronFormat(string cron)
    {
        if (string.IsNullOrWhiteSpace(cron) || !cron.StartsWith("cron(", StringComparison.OrdinalIgnoreCase) || !cron.EndsWith(')'))
            return false;

        var inner = cron.Substring(5, cron.Length - 6); // Remove "cron(" and ")"

        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 5 or > 6)
            return false;

        var minutes = parts[0];
        var hours = parts[1];
        var dayOfMonth = parts[2];
        var month = parts[3];
        var dayOfWeek = parts[4];
        var year = parts.Length == 6 ? parts[5] : "*";

        if (!ValidateSimpleField(minutes, 0, 59))
            return false;

        if (!ValidateSimpleField(hours, 0, 23))
            return false;

        if (!ValidateDayOfMonthField(dayOfMonth, out var dayOfMonthIsQuestion, out var dayOfMonthHasAsterisk))
            return false;

        if (!ValidateMonthField(month))
            return false;

        if (!ValidateDayOfWeekField(dayOfWeek, out var dayOfWeekIsQuestion, out var dayOfWeekHasAsterisk))
            return false;

        if (!ValidateSimpleField(year, 1970, 2199))
            return false;

        if (dayOfMonthIsQuestion && dayOfWeekIsQuestion)
            return false;

        if (!dayOfMonthIsQuestion && !dayOfWeekIsQuestion)
            return false;

        if (dayOfMonthHasAsterisk && !dayOfWeekIsQuestion)
            return false;

        if (dayOfWeekHasAsterisk && !dayOfMonthIsQuestion)
            return false;

        return true;
    }

    private static readonly IReadOnlyDictionary<string, int> MonthNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["JAN"] = 1,
        ["FEB"] = 2,
        ["MAR"] = 3,
        ["APR"] = 4,
        ["MAY"] = 5,
        ["JUN"] = 6,
        ["JUL"] = 7,
        ["AUG"] = 8,
        ["SEP"] = 9,
        ["OCT"] = 10,
        ["NOV"] = 11,
        ["DEC"] = 12
    };

    private static readonly IReadOnlyDictionary<string, int> DayOfWeekNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["SUN"] = 1,
        ["MON"] = 2,
        ["TUE"] = 3,
        ["WED"] = 4,
        ["THU"] = 5,
        ["FRI"] = 6,
        ["SAT"] = 7
    };

    private static bool ValidateMonthField(string field) =>
        ValidateFieldSegments(field, 1, 12, MonthNames, allowQuestion: false, allowL: false, allowW: false, allowHash: false, out _, out _);

    private static bool ValidateDayOfWeekField(string field, out bool isQuestion, out bool hasAsterisk) =>
        ValidateFieldSegments(field, 1, 7, DayOfWeekNames, allowQuestion: true, allowL: true, allowW: false, allowHash: true, out isQuestion, out hasAsterisk);

    private static bool ValidateDayOfMonthField(string field, out bool isQuestion, out bool hasAsterisk) =>
        ValidateFieldSegments(field, 1, 31, null, allowQuestion: true, allowL: true, allowW: true, allowHash: false, out isQuestion, out hasAsterisk);

    private static bool ValidateSimpleField(string field, int min, int max) =>
        ValidateFieldSegments(field, min, max, null, allowQuestion: false, allowL: false, allowW: false, allowHash: false, out _, out _);

    private static bool ValidateFieldSegments(
        string field,
        int min,
        int max,
        IReadOnlyDictionary<string, int>? nameMap,
        bool allowQuestion,
        bool allowL,
        bool allowW,
        bool allowHash,
        out bool isQuestion,
        out bool hasAsterisk)
    {
        isQuestion = false;
        hasAsterisk = false;

        if (string.IsNullOrWhiteSpace(field))
            return false;

        var segments = field.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment.Equals("*", StringComparison.Ordinal))
            {
                hasAsterisk = true;
                continue;
            }

            if (segment.Equals("?", StringComparison.Ordinal))
            {
                if (!allowQuestion || segments.Length > 1)
                    return false;

                isQuestion = true;
                continue;
            }

            if (segment.Contains('#'))
            {
                if (!allowHash || segments.Length > 1)
                    return false;

                if (!ValidateHashSegment(segment, min, max, nameMap))
                    return false;

                continue;
            }

            if (allowL && IsLWildcardSegment(segment, min, max, nameMap, allowW))
            {
                if (segments.Length > 1)
                    return false;

                continue;
            }

            if (allowW && IsWWildcardSegment(segment, min, max))
            {
                if (segments.Length > 1)
                    return false;

                continue;
            }

            if (segment.Contains('/'))
            {
                if (!ValidateIncrementSegment(segment, min, max, nameMap))
                    return false;

                continue;
            }

            if (segment.Contains('-'))
            {
                if (!ValidateRangeSegment(segment, min, max, nameMap))
                    return false;

                continue;
            }

            if (!TryParseValue(segment, min, max, nameMap, out _))
                return false;
        }

        return true;
    }

    private static bool ValidateHashSegment(string segment, int min, int max, IReadOnlyDictionary<string, int>? nameMap)
    {
        var parts = segment.Split('#', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
            return false;

        if (!TryParseValue(parts[0], min, max, nameMap, out _))
            return false;

        return int.TryParse(parts[1], out var instance) && instance is >= 1 and <= 5;
    }

    private static bool IsLWildcardSegment(string segment, int min, int max, IReadOnlyDictionary<string, int>? nameMap, bool allowW)
    {
        if (segment.Equals("L", StringComparison.OrdinalIgnoreCase))
            return true;

        if (allowW && segment.Equals("LW", StringComparison.OrdinalIgnoreCase))
            return true;

        if (nameMap is null || !segment.EndsWith('L')) 
            return false;

        var valuePart = segment[..^1];

        return TryParseValue(valuePart, min, max, nameMap, out _);
    }

    private static bool IsWWildcardSegment(string segment, int min, int max)
    {
        if (!segment.EndsWith('W'))
            return false;

        var valuePart = segment[..^1];

        return int.TryParse(valuePart, out var value) && value >= min && value <= max;
    }
    
    private static bool ValidateIncrementSegment(string segment, int min, int max, IReadOnlyDictionary<string, int>? nameMap)
    {
        var parts = segment.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
            return false;

        var start = parts[0];

        if (!(start.Equals("*", StringComparison.Ordinal) || ValidateRangeOrValue(start, min, max, nameMap)))
            return false;

        return int.TryParse(parts[1], out var increment) && increment >= 1 && increment <= max;
    }

    private static bool ValidateRangeSegment(string segment, int min, int max, IReadOnlyDictionary<string, int>? nameMap)
    {
        var parts = segment.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
            return false;

        if (!TryParseValue(parts[0], min, max, nameMap, out var start))
            return false;

        if (!TryParseValue(parts[1], min, max, nameMap, out var end))
            return false;

        return start <= end;
    }

    private static bool ValidateRangeOrValue(string segment, int min, int max, IReadOnlyDictionary<string, int>? nameMap) =>
        segment.Contains('-')
            ? ValidateRangeSegment(segment, min, max, nameMap)
            : TryParseValue(segment, min, max, nameMap, out _);

    private static bool TryParseValue(string segment, int min, int max, IReadOnlyDictionary<string, int>? nameMap, out int value) => 
        (int.TryParse(segment, out value) || nameMap != null && nameMap.TryGetValue(segment, out value)) && value >= min && value <= max;

    /// <summary>
    /// Validates a rate expression.
    /// </summary>
    private static bool ValidateRateFormat(string rate)
    {
        if (string.IsNullOrWhiteSpace(rate) || !rate.StartsWith("rate(", StringComparison.OrdinalIgnoreCase) || !rate.EndsWith(')'))
            return false;

        var inner = rate.Substring(5, rate.Length - 6); // Remove "rate(" and ")"

        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out var value) || value <= 0)
            return false;

        var unit = parts[1].ToLowerInvariant();

        return unit switch
        {
            "minute" or "minutes" => value is >= 1 and <= 60,
            "hour" or "hours" => value is >= 1 and <= 24,
            "day" or "days" => value is >= 1 and <= 365,
            _ => false
        };
    }

    /// <summary>
    /// Validates an at expression.
    /// </summary>
    private static bool ValidateAtFormat(string at)
    {
        if (string.IsNullOrWhiteSpace(at) || !at.StartsWith("at(", StringComparison.OrdinalIgnoreCase) || !at.EndsWith(')'))
            return false;

        var inner = at.Substring(3, at.Length - 4); // Remove "at(" and ")"

        // yyyy-mm-ddThh:mm:ss
        return DateTime.TryParse(inner, out _);
    }

    /// <summary>
    /// Attempts to parse a string representation of a cron schedule and create a corresponding Quartz CronExpression object.
    /// </summary>
    /// <param name="expression">The string containing the cron expression to be parsed. Must be in a valid cron format, either prefixed with
    /// 'cron(' or as a standard cron expression.</param>
    /// <param name="expr">When the method returns, contains the created CronExpression object if parsing succeeds; otherwise, contains
    /// null.</param>
    /// <returns>true if the cron expression was successfully parsed and a CronExpression object was created; otherwise, false.</returns>
    private static bool TryCreateQuartzExpression(string expression, out CronExpression expr)
    {
        expr = null!;
        var cron = expression.StartsWith("cron(", StringComparison.OrdinalIgnoreCase)
            ? expression
            : $"cron({expression})";

        var inner = cron.Substring(5, cron.Length - 6);
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is < 5 or > 6)
            return false;

        var year = parts.Length == 6 ? parts[5] : "*";
        var quartzCron = string.Join(' ', "0", parts[0], parts[1], parts[2], parts[3], parts[4], year);

        try
        {
            expr = new CronExpression(quartzCron);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the interval for a rate expression.
    /// </summary>
    private static TimeSpan GetRateInterval(string rate)
    {
        var inner = rate.Substring(5, rate.Length - 6);
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var value = int.Parse(parts[0]);
        var unit = parts[1].ToLowerInvariant();

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
public class AwsScheduleExpressionValidator<T>(TimeSpan? minInterval = null, TimeSpan? maxInterval = null) : PropertyValidator<T, string>
{
    /// <summary>
    /// AwsScheduleExpressionValidator
    /// </summary>
    public override string Name => "AwsScheduleExpressionValidator";

    /// <summary>
    /// Validates the specified cron expression against the defined minimum and maximum interval constraints.
    /// </summary>
    /// <remarks>This method uses the AwsScheduleExpressionValidator to ensure that the cron expression adheres to the configured interval limits.</remarks>
    /// <param name="context">The validation context that provides additional information about the validation operation.</param>
    /// <param name="value">The cron expression to validate. Must be formatted according to AWS standards.</param>
    /// <returns>true if the cron expression is valid according to the specified interval constraints; otherwise, false.</returns>
    public override bool IsValid(ValidationContext<T> context, string value) => 
        AwsScheduleExpressionValidator.ValidateWithIntervals(value, minInterval, maxInterval);

    /// <summary>
    /// Generates a default error message template that describes the error condition based on defined interval constraints.
    /// </summary>
    /// <remarks>The returned message template varies depending on whether minimum or maximum interval
    /// constraints are defined. If both are present, the message includes both constraints; if only one is defined, the
    /// message reflects that specific constraint; if neither is defined, a generic error message is returned.</remarks>
    /// <param name="errorCode">The error code that identifies the specific error condition being reported.</param>
    /// <returns>A string containing the default message template for the error condition, reflecting the presence of minimum
    /// and/or maximum interval constraints.</returns>
    protected override string GetDefaultMessageTemplate(string errorCode) =>
        minInterval.HasValue && maxInterval.HasValue 
            ? "Invalid schedule expression or violates interval constraints (min: {MinInterval}, max: {MaxInterval})." 
            : minInterval.HasValue 
                ? "Invalid schedule expression or violates minimum interval of {MinInterval}." 
                : maxInterval.HasValue 
                    ? "Invalid schedule expression or violates maximum interval of {MaxInterval}." 
                    : "Invalid schedule expression.";
}

/// <summary>
/// Extension methods for FluentValidation.
/// </summary>
public static class AwsScheduleValidationExtensions
{
    /// <summary>
    /// Adds AWS schedule expression validation to a FluentValidation rule builder.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="ruleBuilder"></param>
    extension<T>(IRuleBuilder<T, string> ruleBuilder)
    {
        /// <summary>
        /// Adds a validator to ensure that the provided AWS schedule expression conforms to AWS scheduling standards.
        /// </summary>
        /// <remarks>This method uses the <see cref="AwsScheduleExpressionValidator{T}"/> to validate the
        /// format and correctness of AWS schedule expressions. Use this method when you need to enforce AWS schedule
        /// expression validity within a FluentValidation rule set.</remarks>
        /// <returns>An instance of <see cref="IRuleBuilderOptions{T, String}"/> that enables further rule configuration.</returns>
        public IRuleBuilderOptions<T, string> MustBeValidAwsSchedule() => 
            ruleBuilder.SetValidator(new AwsScheduleExpressionValidator<T>());

        /// <summary>
        /// Adds a validator to ensure that the provided schedule expression conforms to AWS scheduling rules and meets
        /// the specified minimum interval between executions.
        /// </summary>
        /// <remarks>Use this method to enforce AWS scheduling constraints when validating schedule
        /// expressions. The validator checks both the syntactic validity and the minimum interval requirement, helping
        /// prevent misconfiguration of scheduled tasks.</remarks>
        /// <param name="minInterval">The minimum allowed time interval between scheduled executions. Must be a positive TimeSpan value.</param>
        /// <returns>An IRuleBuilderOptions instance that enables further configuration of validation rules for the schedule expression.</returns>
        public IRuleBuilderOptions<T, string> MustBeValidAwsSchedule(TimeSpan minInterval) => 
            ruleBuilder.SetValidator(new AwsScheduleExpressionValidator<T>(minInterval));

        /// <summary>
        /// Adds a validator that ensures the provided schedule expression conforms to AWS scheduling requirements,
        /// optionally enforcing minimum and maximum interval constraints between scheduled events.
        /// </summary>
        /// <remarks>Use this method to validate AWS schedule expressions within a FluentValidation rule,
        /// ensuring they meet specified interval requirements. The validator checks that the schedule expression is
        /// valid according to AWS rules and that the intervals between scheduled events comply with the provided
        /// constraints.</remarks>
        /// <param name="minInterval">The minimum allowed interval between scheduled events. Specify <see langword="null"/> to indicate no minimum
        /// constraint. Must be a positive <see cref="TimeSpan"/> if provided.</param>
        /// <param name="maxInterval">The maximum allowed interval between scheduled events. Specify <see langword="null"/> to indicate no maximum
        /// constraint. Must be a positive <see cref="TimeSpan"/> if provided.</param>
        /// <returns>An <see cref="IRuleBuilderOptions{T, String}"/> instance that enables further configuration of validation rules.</returns>
        public IRuleBuilderOptions<T, string> MustBeValidAwsSchedule(TimeSpan? minInterval, TimeSpan? maxInterval) => 
            ruleBuilder.SetValidator(new AwsScheduleExpressionValidator<T>(minInterval, maxInterval));
    }
}
