using System.Globalization;
using System.Text.RegularExpressions;

namespace CNCGCodeGenerator.Core;

public enum Axis { X, Y, Z, V }
public enum CoordinateMode { Absolute }

public sealed record ValidationIssue(string Category, string Field, string Message)
{
    public override string ToString() => $"{Category}: {Field}: {Message}";
}

public sealed class ValidationException : Exception
{
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public ValidationException(IEnumerable<ValidationIssue> issues)
        : base(string.Join(Environment.NewLine, issues)) => Issues = issues.ToArray();
    public ValidationException(string category, string field, string message)
        : this([new(category, field, message)]) { }
}

public static class Numeric
{
    public static decimal Parse(string? text, string field)
    {
        if (text is null || !Regex.IsMatch(text, @"\A[+-]?[0-9]+(?:\.[0-9]+)?\z") ||
            !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
            throw new ValidationException("Input", field, $"'{text}' requires a complete decimal with ASCII digits and '.' (no commas or exponent).");
        // Decimal.TryParse can round excessive significant digits. Require exact value text.
        var canonical = text.TrimStart('+', '-').TrimStart('0');
        if (canonical.Contains('.')) canonical = canonical.TrimEnd('0').TrimEnd('.');
        if (canonical.StartsWith('.')) canonical = "0" + canonical;
        if (canonical.Length == 0) canonical = "0";
        if (Format(value).TrimStart('-') != canonical)
            throw new ValidationException("Input", field, "Value exceeds exact decimal precision.");
        return value;
    }

    public static string Format(decimal value) => value == 0 ? "0" : value.ToString("0.############################", CultureInfo.InvariantCulture);
    public static void RequirePrecision(decimal value, int places, string field)
    {
        if (decimal.Round(value, places, MidpointRounding.ToEven) != value)
            throw new ValidationException("Input", field, $"{Format(value)} is not exactly representable with {places} decimal places; explicit operator adjustment required.");
    }
}

public sealed record AxisPosition(decimal X, decimal Y, decimal Z, decimal V = 0)
{
    public decimal this[Axis axis] => axis switch { Axis.X => X, Axis.Y => Y, Axis.Z => Z, Axis.V => V, _ => throw new ArgumentOutOfRangeException(nameof(axis)) };
    public AxisPosition With(Axis axis, decimal value) => axis switch
    {
        Axis.X => this with { X = value },
        Axis.Y => this with { Y = value },
        Axis.Z => this with { Z = value },
        Axis.V => this with { V = value },
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };
}

public sealed record GrindingRequest
{
    public AxisPosition? StartPositionMm { get; init; }
    public decimal TableTravelXMm { get; init; }
    public decimal CrossTravelZMm { get; init; }
    public decimal CrossStepZMm { get; init; }
    public decimal VerticalDownfeedYPerRoughSweepMm { get; init; }
    public decimal XGrindingFeedMmPerMinute { get; init; }
    public decimal YDownfeedMmPerMinute { get; init; }
    public decimal ZCrossFeedMmPerMinute { get; init; }
    public decimal SpindleRpm { get; init; }
    public int RoughingSweeps { get; init; }
    public int FinishingSweeps { get; init; }
    public int SparkOutRoundTrips { get; init; }
    public CoordinateMode CoordinateMode { get; init; } = CoordinateMode.Absolute;
    public bool CoolantEnabled { get; init; }
    public bool DressingEnabled { get; init; }
    public string ProgramName { get; init; } = "";
    public string ProgramComment { get; init; } = "";
}

public sealed record Motion(Axis Axis, AxisPosition TargetMm, decimal FeedMmPerMinute);
public sealed record MotionPlan(AxisPosition StartMm, IReadOnlyList<Motion> Motions);

public static class TextValidation
{
    public static void Comment(string text, string field)
    {
        if (text is null || text.Length > 200 || text.Any(c => c < 32 || c > 126 || c is ';' or '%' or '(' or ')' || c is '\r' or '\n'))
            throw new ValidationException("Input", field, "Use at most 200 printable ASCII characters without executable delimiters.");
    }
    public static void ProgramName(string name)
    {
        if (name is null || !Regex.IsMatch(name, @"\A[0-9]{6}\z"))
            throw new ValidationException("Input", "ProgramName", "Require a six-digit numeric Fagor program number.");
    }
}
