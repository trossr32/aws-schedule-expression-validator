namespace AwsScheduleExpressionValidator.Tests;

public class AwsScheduleExpressionValidationTests
{
    [Fact]
    public void Evaluate_ValidExpression_ReturnsSuccess()
    {
        var result = "rate(5 minutes)"
            .ValidateAwsScheduleExpression()
            .WithMinInterval(TimeSpan.FromMinutes(1))
            .WithMaxInterval(TimeSpan.FromMinutes(10))
            .Evaluate();

        Assert.True(result.IsValid);
        Assert.Equal(AwsScheduleValidationError.None, result.Error);
    }

    [Fact]
    public void Evaluate_InvalidFormat_ReturnsError()
    {
        var result = "invalid"
            .ValidateAwsScheduleExpression()
            .Evaluate();

        Assert.False(result.IsValid);
        Assert.Equal(AwsScheduleValidationError.InvalidFormat, result.Error);
    }

    [Fact]
    public void Evaluate_InvalidIntervalConfiguration_ReturnsError()
    {
        var result = "rate(5 minutes)"
            .ValidateAwsScheduleExpression()
            .WithMinInterval(TimeSpan.FromMinutes(2))
            .WithMaxInterval(TimeSpan.FromMinutes(1))
            .Evaluate();

        Assert.False(result.IsValid);
        Assert.Equal(AwsScheduleValidationError.InvalidIntervalConfiguration, result.Error);
    }

    [Fact]
    public void Evaluate_IntervalViolation_ReturnsError()
    {
        var result = "rate(5 minutes)"
            .ValidateAwsScheduleExpression()
            .WithMinInterval(TimeSpan.FromMinutes(10))
            .Evaluate();

        Assert.False(result.IsValid);
        Assert.Equal(AwsScheduleValidationError.IntervalConstraintViolation, result.Error);
    }

    [Fact]
    public void Evaluate_ThrowsOnFailure_WhenConfigured()
    {
        Assert.Throws<AwsScheduleExpressionFormatException>(() =>
            "invalid"
                .ValidateAwsScheduleExpression()
                .ThrowExceptionOnFailure()
                .Evaluate());
    }

    [Fact]
    public void IsValid_ReturnsTrue_ForValidExpression()
    {
        var isValid = "rate(5 minutes)"
            .ValidateAwsScheduleExpression()
            .WithMinInterval(TimeSpan.FromMinutes(1))
            .IsValid();

        Assert.True(isValid);
    }

    [Fact]
    public void GetNextScheduleExpressionOccurrences_ReturnsExpectedCount()
    {
        var occurrences = "rate(1 minutes)"
            .ValidateAwsScheduleExpression()
            .WithOccurrenceCount(3)
            .GetNextScheduleExpressionOccurrences();

        Assert.Equal(3, occurrences.Count);
    }

    [Fact]
    public void GetNextScheduleExpressionOccurrences_UsesOccurrenceStart()
    {
        var start = DateTimeOffset.UtcNow.AddHours(1);

        var occurrences = "rate(1 minutes)"
            .ValidateAwsScheduleExpression()
            .WithOccurrenceCount(1)
            .WithOccurrenceStart(start)
            .GetNextScheduleExpressionOccurrences();

        Assert.Single(occurrences);
        Assert.True(occurrences[0] >= start);
    }

    [Fact]
    public void GetNextScheduleExpressionOccurrences_ThrowsOnInvalidFormat()
    {
        Assert.Throws<AwsScheduleExpressionFormatException>(() =>
            "invalid"
                .ValidateAwsScheduleExpression()
                .ThrowExceptionOnFailure()
                .GetNextScheduleExpressionOccurrences());
    }
}
