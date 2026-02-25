namespace AwsScheduleExpressionValidator.Tests;

public class AwsScheduleExpressionValidatorTests
{
    [Theory]
    [InlineData("0 10 * * ? *")] // Valid CRON: 10 AM daily
    [InlineData("*/5 * * * ? *")] // Valid: Every 5 minutes
    [InlineData("0 0 1 1 ? *")] // Valid: Jan 1 midnight
    [InlineData("0 12 ? * MON *")] // Valid: Mondays at noon
    [InlineData("15 10 ? * 6L 2022-2023")] // Valid: AWS doc example
    [InlineData("0 9 ? * MON-FRI *")] // Valid: weekday range
    [InlineData("0 9 ? * MON,WED,FRI *")] // Valid: day-of-week list
    [InlineData("0 9 1W * ? *")] // Valid: nearest weekday
    [InlineData("0 9 LW * ? *")] // Valid: last weekday of month
    [InlineData("0 9 15W * ? 2025")] // Valid: weekday with year
    [InlineData("0 9 ? * MON#2 *")] // Valid: second Monday
    [InlineData("0 9 ? JAN,MAR,DEC MON#1 2024")] // Valid: month names list
    [InlineData("0/15 8-18 ? * TUE,THU *")] // Valid: increments + range
    [InlineData("0 9 ? * 5L *")] // Valid: last Thursday using numeric day
    [InlineData("0 9 L * ? *")] // Valid: last day of month
    public void ValidateFormat_ValidCron_ReturnsTrue(string expression)
    {
        Assert.True(AwsScheduleExpressionValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("rate(5 minutes)")]
    [InlineData("rate(1 hour)")]
    [InlineData("rate(10 days)")]
    [InlineData("rate(30 minutes)")]
    public void ValidateFormat_ValidRates_ReturnsTrue(string expression)
    {
        Assert.True(AwsScheduleExpressionValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("at(2023-01-01T12:00:00)")]
    [InlineData("at(2024-12-31T23:59:59)")]
    public void ValidateFormat_ValidAts_ReturnsTrue(string expression)
    {
        Assert.True(AwsScheduleExpressionValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("invalid")] // Invalid format
    [InlineData("cron(0 10 * *)")] // Too few fields
    [InlineData("cron(0 10 * * ? * 2024)")] // Too many fields
    [InlineData("60 10 * * ? *")] // Minute out of range
    [InlineData("0 24 * * ? *")] // Hour out of range
    [InlineData("0 10 32 * ? *")] // Day-of-month out of range
    [InlineData("0 10 ? 13 * *")] // Month out of range
    [InlineData("0 10 ? * 8 *")] // Day-of-week out of range
    [InlineData("0 10 ? * MON#2,FRI#1 *")] // Multiple # not allowed
    [InlineData("0 10 ? * MON#6 *")] // Invalid # instance
    [InlineData("0 10 ? * MON#0 *")] // Invalid # instance
    [InlineData("0 10 ? * MON#1,MON#2 *")] // Multiple # entries not allowed
    [InlineData("0 10 ? * MON#1-2 *")] // Invalid # format
    [InlineData("0 10 ? * MON,L *")] // Invalid mixed L list
    [InlineData("0 10 LW * * *")] // Missing ? in day-of-week
    [InlineData("0 10 L * * *")] // Missing ? in day-of-week
    [InlineData("0 10 * * * *")] // * used in both day fields
    [InlineData("0 10 ? * ? *")] // ? used in both day fields
    [InlineData("0 10 5W * MON *")] // W with day-of-week set
    [InlineData("0 10 5W,6W * ? *")] // W with list not allowed
    [InlineData("0 10 ? * MON#1,FRI *")] // # with list not allowed
    [InlineData("0 10 1-5W * ? *")] // W with range not allowed
    [InlineData("0 10 1L * ? *")] // L with value not allowed in day-of-month
    [InlineData("0 10 ? * MONL,FRI *")] // L with list not allowed
    [InlineData("0 10 ? * MONL-THU *")] // L with range not allowed
    [InlineData("0 10 ? * MON#1/2 *")] // # with increment not allowed
    [InlineData("rate(5)")] // Missing unit
    [InlineData("rate(5 invalid)")] // Invalid unit
    [InlineData("rate(0 minutes)")] // Zero value
    [InlineData("rate(61 minutes)")] // Too high for minutes
    [InlineData("at(invalid)")] // Invalid date
    public void ValidateFormat_InvalidExpressions_ReturnsFalse(string expression)
    {
        Assert.False(AwsScheduleExpressionValidator.ValidateFormat(expression));
    }

    [Theory]
    [InlineData("0 10 * * ? *", 1.0, -1.0)] // 1 hour min, daily at 10 AM (24h > 1h)
    [InlineData("0 */2 * * ? *", 1.0, -1.0)] // 2 hour intervals, 1 hour min
    [InlineData("rate(2 hours)", 1.0, -1.0)] // Rate 2h >= 1h
    [InlineData("rate(30 minutes)", 0.5, -1.0)] // 30 min >= 30 min
    [InlineData("0 10 * * ? *", -1.0, 25.0)] // Daily, max 25h OK
    [InlineData("rate(1 hour)", -1.0, 2.0)] // Rate 1h <= 2h
    [InlineData("*/30 * * * ? *", 0.5, 1.0)] // 30 min intervals, min 30m, max 1h
    public void ValidateWithIntervals_Valid_ReturnsTrue(string expression, double minHours, double maxHours)
    {
        var minInterval = minHours >= 0 ? TimeSpan.FromHours(minHours) : (TimeSpan?)null;
        var maxInterval = maxHours >= 0 ? TimeSpan.FromHours(maxHours) : (TimeSpan?)null;
        Assert.True(AwsScheduleExpressionValidator.ValidateWithIntervals(expression, minInterval, maxInterval));
    }

    [Theory]
    [InlineData("*/5 * * * ? *", 1.0, -1.0)] // 5 min intervals, 1 hour min required
    [InlineData("0 10 * * ? *", 25.0, -1.0)] // Daily, but 25 hours min (impossible)
    [InlineData("rate(5 minutes)", 0.1, -1.0)] // 5 min < 6 min
    [InlineData("rate(1 day)", 25.0, -1.0)] // 24h < 25h
    [InlineData("0 */2 * * ? *", -1.0, 1.0)] // 2h intervals > 1h max
    [InlineData("rate(2 hours)", -1.0, 1.0)] // 2h > 1h max
    [InlineData("at(2023-01-01T12:00:00)", 1.0, -1.0)] // At with min interval invalid
    public void ValidateWithIntervals_Invalid_ReturnsFalse(string expression, double minHours, double maxHours)
    {
        var minInterval = minHours >= 0 ? TimeSpan.FromHours(minHours) : (TimeSpan?)null;
        var maxInterval = maxHours >= 0 ? TimeSpan.FromHours(maxHours) : (TimeSpan?)null;
        Assert.False(AwsScheduleExpressionValidator.ValidateWithIntervals(expression, minInterval, maxInterval));
    }

    [Fact]
    public void ValidateWithIntervals_ZeroInterval_ReturnsFalse()
    {
        Assert.False(AwsScheduleExpressionValidator.ValidateWithIntervals("0 10 * * ? *", TimeSpan.Zero, null));
        Assert.False(AwsScheduleExpressionValidator.ValidateWithIntervals("0 10 * * ? *", null, TimeSpan.Zero));
    }
}