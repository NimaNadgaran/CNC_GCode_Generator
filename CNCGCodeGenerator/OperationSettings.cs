using CNCGCodeGenerator.Core;

namespace CNCGCodeGenerator;

public sealed class OperationSettings
{
    public static IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>
    {
        ["StartX"] = "Current/start X work coordinate (mm)",
        ["StartY"] = "Current/start Y work coordinate (mm)",
        ["StartZ"] = "Current/start Z work coordinate (mm)",
        ["StartV"] = "Current/start V work coordinate (mm)",
        ["XFeed"] = "X grinding feed (mm/min)",
        ["YFeed"] = "Y downfeed (mm/min; required when moving Y)",
        ["ZFeed"] = "Z cross-feed (mm/min; required when moving Z)",
        ["ZStep"] = "Z cross-step (mm)",
        ["Rpm"] = "Clockwise spindle request (rpm)",
        ["Roughing"] = "Roughing sweeps (count)",
        ["Finishing"] = "Finishing sweeps without downfeed (count)",
        ["SparkOut"] = "Extra X round trips at final Z (count)",
        ["Name"] = "Program name (ASCII letters/digits/_/-)",
        ["Notes"] = "Program comment (ASCII)",
        ["Review"] = "Operator and setup review reference (required)"
    };
    public Dictionary<string, string> Values { get; init; } = Labels.ToDictionary(p => p.Key, _ => "");
    public CoordinateMode CoordinateMode { get; set; } = CoordinateMode.Absolute;
    public CrossStepTiming CrossStepTiming { get; set; } = CrossStepTiming.AfterRoundTrip;
    public TableDirection FirstXDirection { get; set; } = TableDirection.Right;
    public OperationSettings Copy() => new()
    {
        Values = new(Values),
        CoordinateMode = CoordinateMode,
        CrossStepTiming = CrossStepTiming,
        FirstXDirection = FirstXDirection
    };
    public GrindingRequest CreateRequest(string xTravel, string yDownfeed, string zCoverage)
    {
        decimal Read(string key) => Numeric.Parse(Values[key], Labels[key]);
        int Count(string key)
        {
            var value = Read(key);
            if (value < 0 || value > 100000 || value != decimal.Truncate(value))
                throw new ValidationException("Input", Labels[key], "Require a whole count between 0 and 100000.");
            return (int)value;
        }
        var y = Numeric.Parse(yDownfeed, "Y downfeed mm");
        var z = Numeric.Parse(zCoverage, "Z cross travel mm");
        return new()
        {
            StartPositionMm = new(Read("StartX"), Read("StartY"), Read("StartZ"), Read("StartV")),
            TableTravelXMm = Numeric.Parse(xTravel, "X table travel mm"),
            VerticalDownfeedYPerRoughSweepMm = y,
            CrossTravelZMm = z,
            CrossStepZMm = Read("ZStep"),
            XGrindingFeedMmPerMinute = Read("XFeed"),
            YDownfeedMmPerMinute = y == 0 && Values["YFeed"] == "" ? 0 : Read("YFeed"),
            ZCrossFeedMmPerMinute = z == 0 && Values["ZFeed"] == "" ? 0 : Read("ZFeed"),
            SpindleRpm = Read("Rpm"),
            RoughingSweeps = Count("Roughing"),
            FinishingSweeps = Count("Finishing"),
            SparkOutRoundTrips = Count("SparkOut"),
            ProgramName = Values["Name"],
            OperatorNotes = Values["Notes"],
            SetupReview = Values["Review"],
            CoordinateMode = CoordinateMode,
            CrossStepTiming = CrossStepTiming,
            FirstXDirection = FirstXDirection
        };
    }
}
