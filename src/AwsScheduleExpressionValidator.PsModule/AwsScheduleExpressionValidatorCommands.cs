using System.Management.Automation;

namespace AwsScheduleExpressionValidator.PsModule;

/// <summary>
/// <para type="synopsis">
/// Tests the validity of an AWS schedule expression format.
/// </para>
/// <para type="description">
/// Provides a cmdlet for testing the validity of an AWS schedule expression format.
/// </para>
/// <example>
/// <para>Example usage:</para>
/// <code>Test-AwsScheduleExpressionFormat -Expression 'rate(5 minutes)'</code>
/// <remarks>This example tests whether the string "rate(5 minutes)" is a valid AWS schedule expression format. The cmdlet will return $true if the format is valid, and $false otherwise.</remarks>
/// </example>
/// <para type="link" uri="https://github.com/trossr32/aws-schedule-expression-validator">GitHub Repository</para>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "AwsScheduleExpressionFormat")]
[OutputType(typeof(bool))]
public sealed class TestAwsScheduleExpressionFormatCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Expression { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        var isValid = AwsScheduleExpressionValidator.ValidateFormat(Expression);
        WriteObject(isValid);
    }
}

/// <summary>
/// <para type="synopsis">
/// Tests the validity of an AWS schedule expression and optional interval constraints.
/// </para>
/// <para type="description">
/// Provides a cmdlet for testing the validity of an AWS schedule expression, with optional minimum and maximum interval constraints.
/// </para>
/// <example>
/// <para>Example usage:</para>
/// <code>Test-AwsScheduleExpression -Expression 'rate(5 minutes)' -MinInterval '00:01:00' -MaxInterval '01:00:00'</code>
/// <remarks>
/// This example tests whether the string "rate(5 minutes)" is a valid AWS schedule expression and falls between 1 minute and 1 hour.
/// The cmdlet will return $true if the expression is valid and within the interval constraints, and $false otherwise.
/// </remarks>
/// </example>
/// <para type="link" uri="https://github.com/trossr32/aws-schedule-expression-validator">GitHub Repository</para>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "AwsScheduleExpression")]
[OutputType(typeof(bool))]
public sealed class TestAwsScheduleExpressionCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Expression { get; set; } = string.Empty;

    [Parameter]
    public TimeSpan? MinInterval { get; set; }

    [Parameter]
    public TimeSpan? MaxInterval { get; set; }

    protected override void ProcessRecord()
    {
        var validation = Expression.ValidateAwsScheduleExpression();

        if (MinInterval.HasValue)
            validation = validation.WithMinInterval(MinInterval.Value);

        if (MaxInterval.HasValue)
            validation = validation.WithMaxInterval(MaxInterval.Value);

        var result = validation.Evaluate();
        WriteObject(result.IsValid);

        if (!result.IsValid)
            WriteWarning(result.Message);
    }
}

/// <summary>
/// <para type="synopsis">
/// Gets upcoming occurrences for an AWS schedule expression.
/// </para>
/// <para type="description">
/// Provides a cmdlet for retrieving upcoming occurrences for a valid AWS schedule expression, optionally starting at a specific time.
/// </para>
/// <example>
/// <para>Example usage:</para>
/// <code>Get-AwsScheduleExpressionOccurrence -Expression 'rate(5 minutes)' -Count 3 -Start '2024-01-01T00:00:00Z'</code>
/// <remarks>This example returns the next three occurrences for the schedule expression starting at January 1, 2024 UTC.</remarks>
/// </example>
/// <para type="link" uri="https://github.com/trossr32/aws-schedule-expression-validator">GitHub Repository</para>
/// </summary>
[Cmdlet(VerbsCommon.Get, "AwsScheduleExpressionOccurrence")]
[OutputType(typeof(DateTimeOffset))]
public sealed class GetAwsScheduleExpressionOccurrenceCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Expression { get; set; } = string.Empty;

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Count { get; set; } = 5;

    [Parameter]
    public DateTimeOffset? Start { get; set; }

    protected override void ProcessRecord()
    {
        if (!AwsScheduleExpressionValidator.ValidateFormat(Expression))
        {
            WriteError(new ErrorRecord(
                new ArgumentException("Schedule expression format is invalid.", nameof(Expression)),
                "InvalidFormat",
                ErrorCategory.InvalidArgument,
                Expression));
            return;
        }

        var occurrences = AwsScheduleExpressionValidator.GetNextOccurrences(Expression, Count, Start);

        foreach (var occurrence in occurrences)
            WriteObject(occurrence);
    }
}
