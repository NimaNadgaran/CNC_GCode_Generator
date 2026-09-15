using CNCGCodeGenerator.Core;

namespace CNCGCodeGenerator;

public sealed class OperationSettings
{
    public static IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>
    {
        ["StartX"] = "Start X coordinate (mm)",
        ["StartY"] = "Start Y coordinate (mm)",
        ["StartZ"] = "Start Z coordinate (mm)",
        ["XFeed"] = "X grinding feed (mm/min)",
        ["YFeed"] = "Y downfeed feed (mm/min)",
        ["ZFeed"] = "Z cross-feed (mm/min)",
        ["ZStep"] = "Z cross-step (mm)",
        ["Rpm"] = "Spindle speed (rpm)",
        ["Roughing"] = "Roughing sweeps (count)",
        ["Finishing"] = "Finishing sweeps (count)",
        ["SparkOut"] = "Extra final-Z X round trips (count)",
        ["Name"] = "Program number (six digits)",
        ["Comment"] = "Program comment (ASCII)"
    };
    public Dictionary<string, string> Values { get; init; } = Labels.ToDictionary(p => p.Key, _ => "");
    public OperationSettings Copy() => new() { Values = new(Values) };
    public GrindingRequest CreateRequest(string xTravel, string yDownfeed, string zCoverage)
    {
        decimal Read(string key) => Numeric.Parse(Values[key], Labels[key]);
        int Count(string key) { var value = Read(key); if (value < 0 || value > 100000 || value != decimal.Truncate(value)) throw new ValidationException("Input", Labels[key], "Require a whole count between 0 and 100000."); return (int)value; }
        var roughing = Count("Roughing");
        var y = roughing == 0 && string.IsNullOrEmpty(yDownfeed) ? 0 : Numeric.Parse(yDownfeed, "Y downfeed per roughing sweep (mm)"); var z = Numeric.Parse(zCoverage, "Z total coverage (mm)");
        return new() { StartPositionMm = new(Read("StartX"), Read("StartY"), Read("StartZ")), TableTravelXMm = Numeric.Parse(xTravel, "X stroke travel (mm)"), VerticalDownfeedYPerRoughSweepMm = y, CrossTravelZMm = z, CrossStepZMm = Read("ZStep"), XGrindingFeedMmPerMinute = Read("XFeed"), YDownfeedMmPerMinute = Read("YFeed"), ZCrossFeedMmPerMinute = Read("ZFeed"), SpindleRpm = Read("Rpm"), RoughingSweeps = roughing, FinishingSweeps = Count("Finishing"), SparkOutRoundTrips = Count("SparkOut"), ProgramName = Values["Name"], ProgramComment = Values["Comment"] };
    }
}
