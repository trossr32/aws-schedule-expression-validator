using System;
using AwsCronValidator;

namespace AwsCronValidator.Tests;

public class AwsCronValidatorTests
{
    [Theory]
    [InlineData("cron(0 10 * * ? *)")] // Valid CRON: 10 AM daily
    [InlineData("cron(*/5 * * * ? *)")] // Valid: Every 5 minutes
    [InlineData("cron(0 0 1 1 ? *)")] // Valid: Jan 1 midnight
    [InlineData("cron(0 12 ? * MON *)")] // Valid: Mondays at noon
    [InlineData("0 10 * * ? *")] // Valid without prefix (backward compat)
    public void ValidateFormat_ValidCrons_ReturnsTrue(string expression)
    {
        Assert.True(AwsCronValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("rate(5 minutes)")]
    [InlineData("rate(1 hour)")]
    [InlineData("rate(10 days)")]
    [InlineData("rate(30 minutes)")]
    public void ValidateFormat_ValidRates_ReturnsTrue(string expression)
    {
        Assert.True(AwsCronValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("at(2023-01-01T12:00:00)")]
    [InlineData("at(2024-12-31T23:59:59)")]
    public void ValidateFormat_ValidAts_ReturnsTrue(string expression)
    {
        Assert.True(AwsCronValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("invalid")] // Invalid format
    [InlineData("cron(0 10 * *)")] // Too few fields
    [InlineData("rate(5)")] // Missing unit
    [InlineData("rate(5 invalid)")] // Invalid unit
    [InlineData("rate(0 minutes)")] // Zero value
    [InlineData("rate(61 minutes)")] // Too high for minutes
    [InlineData("at(invalid)")] // Invalid date
    public void ValidateFormat_InvalidExpressions_ReturnsFalse(string expression)
    {
        Assert.False(AwsCronValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("cron(0 10 * * ? *)", 1.0, -1.0)] // 1 hour min, daily at 10 AM (24h > 1h)
    [InlineData("cron(0 */2 * * ? *)", 1.0, -1.0)] // 2 hour intervals, 1 hour min
    [InlineData("rate(2 hours)", 1.0, -1.0)] // Rate 2h >= 1h
    [InlineData("rate(30 minutes)", 0.5, -1.0)] // 30 min >= 30 min
    [InlineData("cron(0 10 * * ? *)", -1.0, 25.0)] // Daily, max 25h ok
    [InlineData("rate(1 hour)", -1.0, 2.0)] // Rate 1h <= 2h
    [InlineData("cron(*/30 * * * ? *)", 0.5, 1.0)] // 30 min intervals, min 30m, max 1h
    public void ValidateWithIntervals_Valid_ReturnsTrue(string expression, double minHours, double maxHours)
    {
        var minInterval = minHours >= 0 ? TimeSpan.FromHours(minHours) : (TimeSpan?)null;
        var maxInterval = maxHours >= 0 ? TimeSpan.FromHours(maxHours) : (TimeSpan?)null;
        Assert.True(AwsCronValidator.ValidateWithIntervals(expression, minInterval, maxInterval));
    }

    [Theory]
    [InlineData("cron(*/5 * * * ? *)", 1.0, -1.0)] // 5 min intervals, 1 hour min required
    [InlineData("cron(0 10 * * ? *)", 25.0, -1.0)] // Daily, but 25 hours min (impossible)
    [InlineData("rate(5 minutes)", 0.1, -1.0)] // 5 min < 6 min
    [InlineData("rate(1 day)", 25.0, -1.0)] // 24h < 25h
    [InlineData("cron(0 */2 * * ? *)", -1.0, 1.0)] // 2h intervals > 1h max
    [InlineData("rate(2 hours)", -1.0, 1.0)] // 2h > 1h max
    [InlineData("at(2023-01-01T12:00:00)", 1.0, -1.0)] // At with min interval invalid
    public void ValidateWithIntervals_Invalid_ReturnsFalse(string expression, double minHours, double maxHours)
    {
        var minInterval = minHours >= 0 ? TimeSpan.FromHours(minHours) : (TimeSpan?)null;
        var maxInterval = maxHours >= 0 ? TimeSpan.FromHours(maxHours) : (TimeSpan?)null;
        Assert.False(AwsCronValidator.ValidateWithIntervals(expression, minInterval, maxInterval));
    }

    [Fact]
    public void ValidateWithIntervals_ZeroInterval_ReturnsFalse()
    {
        Assert.False(AwsCronValidator.ValidateWithIntervals("cron(0 10 * * ? *)", TimeSpan.Zero, null));
        Assert.False(AwsCronValidator.ValidateWithIntervals("cron(0 10 * * ? *)", null, TimeSpan.Zero));
    }

    // Backward compatibility test
    [Fact]
    public void ValidateWithMinInterval_BackwardCompat()
    {
        Assert.True(AwsCronValidator.ValidateWithMinInterval("rate(2 hours)", TimeSpan.FromHours(1)));
        Assert.False(AwsCronValidator.ValidateWithMinInterval("rate(30 minutes)", TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData("cron(*/5 * * * ? *)", 1)] // 5 min intervals, 1 hour min required
    [InlineData("cron(0 10 * * ? *)", 25)] // Daily, but 25 hours min (impossible)
    [InlineData("rate(5 minutes)", 0.1)] // 5 min < 6 min
    [InlineData("rate(1 day)", 25)] // 24h < 25h
    public void ValidateWithMinInterval_Invalid_ReturnsFalse(string expression, double minHours)
    {
        var minInterval = TimeSpan.FromHours(minHours);
        Assert.False(AwsCronValidator.ValidateWithMinInterval(expression, minInterval));
    }


}