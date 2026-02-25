namespace AwsScheduleExpressionValidator;

/// <summary>
/// Defines the error types that can occur when validating AWS schedule expressions.
/// </summary>
/// <remarks>This enumeration is used to represent specific validation errors encountered during the evaluation of
/// AWS schedule configurations. Each value corresponds to a distinct error condition that can be checked
/// programmatically to determine the nature of a validation failure.</remarks>
public enum AwsScheduleValidationError
{
    /// <summary>
    /// Indicates that no validation errors were encountered, and the AWS schedule expression is considered valid.
    /// </summary>
    None,

    /// <summary>
    /// Represents an error that occurs when the format of the AWS schedule expression is invalid. This error is typically
    /// encountered when the expression does not conform to the expected syntax or structure.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// Represents an error that occurs when an interval configuration is invalid.
    /// </summary>
    /// <remarks>This exception is typically thrown when the specified interval does not meet the required
    /// constraints, such as being negative or exceeding maximum limits. Ensure that the interval values are validated
    /// before configuration to prevent this exception.</remarks>
    InvalidIntervalConfiguration,

    /// <summary>
    /// Represents a violation of an interval constraint, indicating that a specified condition related to time
    /// intervals has not been met.
    /// </summary>
    /// <remarks>This class is typically used in scenarios where constraints on time intervals are enforced,
    /// such as scheduling or timing operations. It may contain properties that provide details about the specific
    /// violation, including the expected and actual interval values.</remarks>
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
    /// <summary>
    /// Gets a predefined validation result indicating that the AWS schedule expression is valid and contains no errors.
    /// </summary>
    /// <remarks>Use this property to represent a successful validation outcome when no issues are detected in
    /// the schedule expression. This instance can be used as a standard result for valid expressions.</remarks>
    public static AwsScheduleExpressionValidationResult Success { get; } = new(true, AwsScheduleValidationError.None, string.Empty);
}

/// <summary>
/// Represents an exception that is thrown when an AWS schedule expression fails validation.
/// </summary>
/// <remarks>This exception is typically thrown when a schedule expression provided to AWS services does not meet
/// the required validation criteria. Use this exception to identify and handle invalid schedule expressions in your
/// application logic.</remarks>
/// <param name="message">The error message that describes the reason for the validation failure.</param>
public class AwsScheduleValidationException(string message) : Exception(message);

/// <summary>
/// Represents an error that occurs when the format of an AWS schedule expression is invalid.
/// </summary>
/// <remarks>This exception is thrown when the provided schedule expression does not conform to the expected
/// format for AWS scheduling.</remarks>
/// <param name="message">The error message that describes the reason for the exception.</param>
public sealed class AwsScheduleExpressionFormatException(string message) : AwsScheduleValidationException(message);

/// <summary>
/// Represents an exception that is thrown when a configuration error occurs related to the AWS schedule interval.
/// </summary>
/// <param name="message">The error message that describes the reason for the exception.</param>
public sealed class AwsScheduleIntervalConfigurationException(string message) : AwsScheduleValidationException(message);

/// <summary>
/// Represents an exception that is thrown when a schedule interval violation occurs in AWS schedule validation.
/// </summary>
/// <remarks>This exception is a specific type of AwsScheduleValidationException, indicating that the provided
/// schedule does not adhere to the expected interval constraints.</remarks>
/// <param name="message">The error message that describes the reason for the exception.</param>
public sealed class AwsScheduleIntervalViolationException(string message) : AwsScheduleValidationException(message);

/// <summary>
/// Provides configuration and validation for AWS schedule expressions, including interval constraints, occurrence
/// count, and start time. Enables fluent setup of validation parameters and supports exception handling for validation
/// failures.
/// </summary>
/// <remarks>Use this class to configure validation rules for AWS schedule expressions, such as minimum and
/// maximum intervals between scheduled events, the number of occurrences to validate, and the start time for
/// scheduling. The class offers methods for fluent configuration and allows callers to choose whether validation
/// failures should throw exceptions or return detailed error results. Validation methods ensure that only schedule
/// expressions meeting the specified constraints are considered valid. For a simple validity check, use the IsValid
/// method; for detailed validation results, use Evaluate.</remarks>
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

    /// <summary>
    /// Gets the next scheduled occurrences based on the current schedule expression, occurrence count, and start time.
    /// </summary>
    /// <remarks>The method validates the schedule expression format and occurrence count before retrieving
    /// occurrences. If validation fails, the method returns an empty list and handles the failure internally.</remarks>
    /// <returns>A read-only list of <see cref="DateTimeOffset"/> values representing the next scheduled occurrences. The list is
    /// empty if the schedule expression format is invalid or the occurrence count is less than or equal to zero.</returns>
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

/// <summary>
/// Provides extension methods for validating AWS schedule expressions.
/// </summary>
/// <remarks>This class contains methods that enable a fluent interface for validating AWS schedule
/// expressions.</remarks>
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
