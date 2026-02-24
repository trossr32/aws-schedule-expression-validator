namespace AwsScheduleExpressionValidator;

public enum AwsScheduleValidationError
{
    None,
    InvalidFormat,
    InvalidIntervalConfiguration,
    IntervalConstraintViolation
}

/// <summary>
/// Represents the result of validating an AWS schedule expression, indicating whether the expression is valid and
/// providing error details if validation fails.
/// </summary>
/// <remarks>Use the static <see cref="Success"/> property to represent a successful validation result with no
/// errors.</remarks>
/// <param name="IsValid">Indicates whether the AWS schedule expression is valid. A value of <see langword="true"/> signifies a valid
/// expression; otherwise, <see langword="false"/>.</param>
/// <param name="Error">Specifies the type of validation error encountered, if any. This value identifies the nature of the validation
/// failure.</param>
/// <param name="Message">Provides a descriptive message related to the validation result. The message may include guidance or details about
/// the validation outcome.</param>
public sealed record AwsScheduleExpressionValidationResult(bool IsValid, AwsScheduleValidationError Error, string Message)
{
    public static AwsScheduleExpressionValidationResult Success { get; } = new(true, AwsScheduleValidationError.None, string.Empty);
}

public class AwsScheduleValidationException(string message) : Exception(message);

public sealed class AwsScheduleExpressionFormatException(string message) : AwsScheduleValidationException(message);

public sealed class AwsScheduleIntervalConfigurationException(string message) : AwsScheduleValidationException(message);

public sealed class AwsScheduleIntervalViolationException(string message) : AwsScheduleValidationException(message);

public sealed class AwsScheduleExpressionValidation
{
    private readonly string _expression;
    private TimeSpan? _minInterval;
    private TimeSpan? _maxInterval;
    private bool _throwOnFailure;
    private int _occurrenceCount = 5;
    private DateTimeOffset? _occurrenceStart;

    internal AwsScheduleExpressionValidation(string expression) => _expression = expression;
    
    /// <summary>
    /// Sets the minimum interval that must be observed between scheduled events for validation purposes.
    /// </summary>
    /// <param name="minInterval">The minimum interval, as a <see cref="System.TimeSpan"/>, that scheduled expressions must adhere to. Must be a
    /// positive duration.</param>
    /// <returns>The current <see cref="AwsScheduleExpressionValidation"/> instance, enabling method chaining.</returns>
    public AwsScheduleExpressionValidation WithMinInterval(TimeSpan minInterval)
    {
        _minInterval = minInterval;
        return this;
    }

    /// <summary>
    /// Sets the maximum interval allowed for schedule expression validation.
    /// </summary>
    /// <remarks>Use this method to enforce an upper limit on the interval when validating schedule
    /// expressions. This helps ensure that only intervals within the specified range are considered valid.</remarks>
    /// <param name="maxInterval">The maximum time span permitted for the interval. Must be a positive value.</param>
    /// <returns>The current instance of the AwsScheduleExpressionValidation for method chaining.</returns>
    public AwsScheduleExpressionValidation WithMaxInterval(TimeSpan maxInterval)
    {
        _maxInterval = maxInterval;
        return this;
    }

    /// <summary>
    /// Enables exception throwing when validation fails, allowing callers to handle errors through exception handling.
    /// </summary>
    /// <returns>The current instance of the AwsScheduleExpressionValidation class, enabling method chaining.</returns>
    /// <exception cref="AwsScheduleExpressionFormatException">Thrown when the schedule expression format is invalid.</exception>
    /// <exception cref="AwsScheduleIntervalConfigurationException">Thrown when the interval configuration is invalid (e.g., negative intervals, min greater than max).</exception>
    /// <exception cref="AwsScheduleIntervalViolationException">Thrown when the schedule expression violates specified interval constraints.</exception>
    public AwsScheduleExpressionValidation ThrowExceptionOnFailure()
    {
        _throwOnFailure = true;
        return this;
    }

    /// <summary>
    /// Sets the occurrence count used for schedule expression validation and returns the current instance for method chaining.
    /// </summary>
    /// <param name="count">The number of occurrences to use for validation. Must be a non-negative integer.</param>
    /// <returns>The current instance of <see cref="AwsScheduleExpressionValidation"/> with the updated occurrence count.</returns>
    public AwsScheduleExpressionValidation WithOccurrenceCount(int count)
    {
        _occurrenceCount = count;
        return this;
    }

    /// <summary>
    /// Sets the start date and time for the occurrence schedule.
    /// </summary>
    /// <remarks>This method allows for configuring the start time of the occurrence, which is essential for
    /// scheduling tasks accurately.</remarks>
    /// <param name="start">The date and time when the occurrence should start. This value cannot be in the past.</param>
    /// <returns>Returns the current instance of the AwsScheduleExpressionValidation for method chaining.</returns>
    public AwsScheduleExpressionValidation WithOccurrenceStart(DateTimeOffset start)
    {
        _occurrenceStart = start;
        return this;
    }

    /// <summary>
    /// Validates the AWS schedule expression and its associated interval constraints.
    /// </summary>
    /// <remarks>This method checks the format of the schedule expression and ensures that any specified
    /// minimum and maximum intervals are valid and logically consistent.
    /// To return a more declarative response, use the <see cref="Evaluate"/> method instead.</remarks>
    /// <returns>
    /// <see langword="true"/> if valid, else <see langword="false"/>
    /// </returns>
    public bool IsValid() => Evaluate().IsValid;

    /// <summary>
    /// Validates the AWS schedule expression and its associated interval constraints. 
    /// </summary>
    /// <remarks>
    /// This method checks the format of the schedule expression and ensures that any specified
    /// minimum and maximum intervals are valid and logically consistent. It returns a success result if all validations
    /// pass, or a failure result with detailed error information if any validation fails.
    /// To return a simple boolean result, use the <see cref="IsValid"/> method instead.
    /// </remarks>
    /// <returns>An instance of AwsScheduleExpressionValidationResult indicating whether the schedule expression and interval
    /// configuration are valid. Returns a failure result with an appropriate error if validation does not succeed.</returns>
    public AwsScheduleExpressionValidationResult Evaluate()
    {
        if (!AwsScheduleExpressionValidator.ValidateFormat(_expression))
            return HandleFailure(AwsScheduleValidationError.InvalidFormat, new AwsScheduleExpressionFormatException("Schedule expression format is invalid."));

        if (_minInterval.HasValue && _minInterval.Value <= TimeSpan.Zero)
            return HandleFailure(AwsScheduleValidationError.InvalidIntervalConfiguration, new AwsScheduleIntervalConfigurationException("Minimum interval must be greater than zero."));

        if (_maxInterval.HasValue && _maxInterval.Value <= TimeSpan.Zero)
            return HandleFailure(AwsScheduleValidationError.InvalidIntervalConfiguration, new AwsScheduleIntervalConfigurationException("Maximum interval must be greater than zero."));

        if (_minInterval.HasValue && _maxInterval.HasValue && _minInterval.Value > _maxInterval.Value)
            return HandleFailure(AwsScheduleValidationError.InvalidIntervalConfiguration, new AwsScheduleIntervalConfigurationException("Minimum interval must not exceed maximum interval."));

        if (AwsScheduleExpressionValidator.ValidateWithIntervals(_expression, _minInterval, _maxInterval))
            return AwsScheduleExpressionValidationResult.Success;

        var constraintMessage = BuildConstraintMessage();

        return HandleFailure(AwsScheduleValidationError.IntervalConstraintViolation, new AwsScheduleIntervalViolationException(constraintMessage));
    }

    public IReadOnlyList<DateTimeOffset> GetNextScheduleExpressionOccurrences()
    {
        if (!AwsScheduleExpressionValidator.ValidateFormat(_expression))
        {
            HandleFailure(AwsScheduleValidationError.InvalidFormat, new AwsScheduleExpressionFormatException("Schedule expression format is invalid."));

            return [];
        }

        if (_occurrenceCount <= 0)
        {
            HandleFailure(AwsScheduleValidationError.InvalidIntervalConfiguration, new AwsScheduleIntervalConfigurationException("Occurrence count must be greater than zero."));

            return [];
        }

        return AwsScheduleExpressionValidator.GetNextOccurrences(_expression, _occurrenceCount, _occurrenceStart);
    }

    private AwsScheduleExpressionValidationResult HandleFailure(AwsScheduleValidationError error, AwsScheduleValidationException exception) =>
        _throwOnFailure 
            ? throw exception 
            : new AwsScheduleExpressionValidationResult(false, error, exception.Message);

    private string BuildConstraintMessage()
    {
        if (!_minInterval.HasValue && !_maxInterval.HasValue)
            return "Schedule expression violates interval constraints.";

        if (_minInterval.HasValue && _maxInterval.HasValue)
            return $"Schedule expression violates interval constraints (min: {_minInterval}, max: {_maxInterval}).";

        if (_minInterval.HasValue)
            return $"Schedule expression violates minimum interval constraint ({_minInterval}).";

        return $"Schedule expression violates maximum interval constraint ({_maxInterval}).";
    }
}

public static class AwsScheduleExpressionValidationExtensions
{
    /// <summary>
    /// Validates the specified AWS schedule expression and returns the result of the validation.
    /// </summary>
    /// <remarks>This method is an extension method for the string class, enabling a fluent interface for
    /// validating AWS schedule expressions.</remarks>
    /// <param name="expression">The AWS schedule expression to validate. This string must conform to the AWS schedule expression format.</param>
    /// <returns>An instance of AwsScheduleExpressionValidation that indicates whether the expression is valid and provides
    /// details about any validation errors.</returns>
    public static AwsScheduleExpressionValidation ValidateAwsScheduleExpression(this string expression) =>
        new(expression);
}
