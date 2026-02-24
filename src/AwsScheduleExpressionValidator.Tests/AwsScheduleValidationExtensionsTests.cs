using FluentValidation;

namespace AwsScheduleExpressionValidator.Tests;

public class AwsScheduleValidationExtensionsTests
{
    private sealed class ScheduleModel
    {
        public string ScheduleExpression { get; set; } = string.Empty;
    }

    private sealed class ScheduleValidator : AbstractValidator<ScheduleModel>
    {
        public ScheduleValidator()
        {
            RuleFor(model => model.ScheduleExpression).MustBeValidAwsSchedule();
        }
    }

    private sealed class ScheduleMinValidator : AbstractValidator<ScheduleModel>
    {
        public ScheduleMinValidator(TimeSpan minInterval)
        {
            RuleFor(model => model.ScheduleExpression).MustBeValidAwsSchedule(minInterval);
        }
    }

    private sealed class ScheduleMinMaxValidator : AbstractValidator<ScheduleModel>
    {
        public ScheduleMinMaxValidator(TimeSpan? minInterval, TimeSpan? maxInterval)
        {
            RuleFor(model => model.ScheduleExpression).MustBeValidAwsSchedule(minInterval, maxInterval);
        }
    }

    [Theory]
    [InlineData("0 10 * * ? *")]
    [InlineData("cron(0 10 * * ? *)")]
    [InlineData("rate(5 minutes)")]
    [InlineData("at(2024-01-01T12:00:00)")]
    public void MustBeValidAwsSchedule_ValidExpressions_Pass(string expression)
    {
        var validator = new ScheduleValidator();

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = expression });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("0 10 * *")]
    public void MustBeValidAwsSchedule_InvalidExpressions_Fail(string expression)
    {
        var validator = new ScheduleValidator();

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = expression });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("rate(2 hours)")]
    [InlineData("0 */2 * * ? *")]
    [InlineData("cron(0 */2 * * ? *)")]
    public void MustBeValidAwsSchedule_MinIntervalValid_Pass(string expression)
    {
        var validator = new ScheduleMinValidator(TimeSpan.FromHours(1));

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = expression });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("rate(30 minutes)")]
    [InlineData("*/5 * * * ? *")]
    [InlineData("cron(*/5 * * * ? *)")]
    public void MustBeValidAwsSchedule_MinIntervalInvalid_Fail(string expression)
    {
        var validator = new ScheduleMinValidator(TimeSpan.FromHours(1));

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = expression });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MustBeValidAwsSchedule_MaxIntervalValid_Pass()
    {
        var validator = new ScheduleMinMaxValidator(null, TimeSpan.FromHours(1));

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = "*/30 * * * ? *" });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("rate(2 hours)", 1.0)]
    [InlineData("at(2024-01-01T12:00:00)", 1.0)]
    public void MustBeValidAwsSchedule_MaxIntervalInvalid_Fail(string expression, double maxHours)
    {
        var validator = new ScheduleMinMaxValidator(null, TimeSpan.FromHours(maxHours));

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = expression });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MustBeValidAwsSchedule_MinMaxIntervalValid_Pass()
    {
        var validator = new ScheduleMinMaxValidator(TimeSpan.FromHours(0.5), TimeSpan.FromHours(1));

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = "*/30 * * * ? *" });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("rate(2 hours)", 0.5, 1.0)]
    [InlineData("at(2024-01-01T12:00:00)", 0.5, 1.0)]
    public void MustBeValidAwsSchedule_MinMaxIntervalInvalid_Fail(string expression, double minHours, double maxHours)
    {
        var validator = new ScheduleMinMaxValidator(TimeSpan.FromHours(minHours), TimeSpan.FromHours(maxHours));

        var result = validator.Validate(new ScheduleModel { ScheduleExpression = expression });

        Assert.False(result.IsValid);
    }
}
