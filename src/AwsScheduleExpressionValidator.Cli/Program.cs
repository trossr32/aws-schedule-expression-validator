using System.Globalization;
using AwsScheduleExpressionValidator;
using Validator = AwsScheduleExpressionValidator.AwsScheduleExpressionValidator;

return Run(args);

// The main entry point for the CLI application.
// Parses the command and delegates to the appropriate handler.
static int Run(string[] args)
{
    if (args.Length == 0 || IsHelp(args[0]))
    {
        PrintHelp();
        return 0;
    }

    var command = args[0].ToLowerInvariant();

    return command switch
    {
        "format" => RunFormat(args),
        "validate" => RunValidate(args),
        "occurrences" => RunOccurrences(args),
        _ => ShowError($"Unknown command '{args[0]}'.")
    };
}

// Validates the format of a schedule expression and prints "Valid" or "Invalid" to the console.
static int RunFormat(string[] args)
{
    if (args.Length < 2)
        return ShowError("Missing schedule expression for format validation.");

    var expression = args[1];
    var isValid = Validator.ValidateFormat(expression);
    Console.WriteLine(isValid ? "Valid" : "Invalid");

    return isValid ? 0 : 1;
}

// Validates a schedule expression and optionally checks the minimum and maximum intervals between occurrences.
static int RunValidate(string[] args)
{
    if (args.Length < 2)
        return ShowError("Missing schedule expression for validation.");

    var expression = args[1];

    if (!TryParseMinMax(args[2..], out var minInterval, out var maxInterval, out var error))
        return ShowError(error);

    var validation = expression.ValidateAwsScheduleExpression();

    if (minInterval.HasValue)
        validation = validation.WithMinInterval(minInterval.Value);

    if (maxInterval.HasValue)
        validation = validation.WithMaxInterval(maxInterval.Value);

    var result = validation.Evaluate();

    if (result.IsValid)
    {
        Console.WriteLine("Valid");
        return 0;
    }

    Console.WriteLine($"Invalid: {result.Message}");

    return 1;
}

// Generates the next occurrences of a schedule expression based on the provided options.
// Supports --count for the number of occurrences to generate and --start for the starting date/time.
// Returns 0 if occurrences are generated successfully; otherwise, returns 1 with an error message.
static int RunOccurrences(string[] args)
{
    if (args.Length < 2)
        return ShowError("Missing schedule expression for occurrences.");

    var expression = args[1];

    if (!TryParseOccurrencesOptions(args[2..], out var count, out var start, out var error))
        return ShowError(error);

    if (!Validator.ValidateFormat(expression))
        return ShowError("Schedule expression format is invalid.");

    var occurrences = Validator.GetNextOccurrences(expression, count, start);

    if (occurrences.Count == 0)
    {
        Console.WriteLine("No occurrences found.");
        return 1;
    }

    foreach (var occurrence in occurrences)
        Console.WriteLine(occurrence.ToString("o", CultureInfo.InvariantCulture));

    return 0;
}

// Parses the options for the validate command.
// Supports --min and --max for minimum and maximum intervals between occurrences.
static bool TryParseMinMax(string[] args, out TimeSpan? minInterval, out TimeSpan? maxInterval, out string error)
{
    minInterval = null;
    maxInterval = null;
    error = string.Empty;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];

        switch (arg)
        {
            case "--min":
                if (!TryReadTimeSpan(args, ref i, out var minValue, out error))
                    return false;

                minInterval = minValue;
                break;
            case "--max":
                if (!TryReadTimeSpan(args, ref i, out var maxValue, out error))
                    return false;

                maxInterval = maxValue;
                break;
            default:
                error = $"Unknown option '{arg}'.";
                return false;
        }
    }

    return true;
}

// Parses the options for the occurrences command.
// Supports --count for the number of occurrences to generate and --start for the starting date/time.
// Returns true if parsing is successful; otherwise, sets an error message and returns false.
static bool TryParseOccurrencesOptions(string[] args, out int count, out DateTimeOffset? start, out string error)
{
    count = 5;
    start = null;
    error = string.Empty;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];

        switch (arg)
        {
            case "--count":
                if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var parsedCount) || parsedCount <= 0)
                {
                    error = "Invalid value for --count. Provide a positive integer.";
                    return false;
                }

                count = parsedCount;
                break;

            case "--start":
                if (i + 1 >= args.Length || !DateTimeOffset.TryParse(args[i + 1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedStart))
                {
                    error = "Invalid value for --start. Use an ISO 8601 date/time, e.g., 2023-01-01T00:00:00Z.";
                    return false;
                }

                start = parsedStart;
                break;
            
            default:
                error = $"Unknown option '{arg}'.";
                return false;
        }

        i++;
    }

    return true;
}

// Reads a TimeSpan value from the arguments at the specified index.
// If successful, advances the index and returns true; otherwise, sets an error message and returns false.
static bool TryReadTimeSpan(string[] args, ref int index, out TimeSpan value, out string error)
{
    value = TimeSpan.Zero;
    error = string.Empty;

    if (index + 1 >= args.Length || !TimeSpan.TryParse(args[index + 1], CultureInfo.InvariantCulture, out value))
    {
        error = $"Invalid value for {args[index]}. Use a TimeSpan format like 00:05:00.";
        return false;
    }

    index++;
    return true;
}

// Displays an error message to the console and prints the help information.
static int ShowError(string message)
{
    Console.Error.WriteLine(message);
    PrintHelp();
    return 1;
}

// Checks if the provided argument is a help command.
static bool IsHelp(string arg) => arg is "-h" or "--help" or "help";

// Prints the help information for the CLI application, including usage instructions and examples.
static void PrintHelp()
{
    Console.WriteLine("AWS Schedule Expression Validator CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  aws-schedule-expression-validator format <expression>");
    Console.WriteLine("  aws-schedule-expression-validator validate <expression> [--min <timespan>] [--max <timespan>]");
    Console.WriteLine("  aws-schedule-expression-validator occurrences <expression> [--count <number>] [--start <iso-8601>]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  aws-schedule-expression-validator format \"rate(5 minutes)\"");
    Console.WriteLine("  aws-schedule-expression-validator validate \"cron(0 10 * * ? *)\" --min 00:05:00 --max 01:00:00");
    Console.WriteLine("  aws-schedule-expression-validator occurrences \"rate(5 minutes)\" --count 3");
}
