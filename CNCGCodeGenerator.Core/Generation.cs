using System.Security.Cryptography;
using System.Text;

namespace CNCGCodeGenerator.Core;

internal static class Planner
{
    public static MotionPlan Plan(GrindingRequest request, ValidatedProfile profile)
    {
        var start = request.StartPositionMm ?? throw new ValidationException("Input", "StartPositionMm", "Known work-coordinate X, Y, Z and V positions are required.");
        if (!Enum.IsDefined(request.CoordinateMode) || !Enum.IsDefined(request.CrossStepTiming) || !Enum.IsDefined(request.FirstXDirection))
            throw new ValidationException("Input", "Strategy", "Unknown coordinate mode or direction/step strategy.");
        if (request.CoolantEnabled || request.DressingEnabled)
            throw new ValidationException("Unknown machine data", "Coolant/dressing", "Generation is disabled until machine-specific codes and complete sequences are confirmed and implemented. Dresser axis is V.");
        TextValidation.ProgramName(request.ProgramName, (int)profile["Output.MaximumNameLength"]);
        TextValidation.Comment(request.OperatorNotes, "OperatorNotes");
        if (string.IsNullOrWhiteSpace(request.SetupReview))
            throw new ValidationException("Input", "SetupReview", "Record operator review of start/reference, fixture and wheel clearances across the complete envelope, and spindle procedure.");
        if (request.TableTravelXMm <= 0 || request.CrossTravelZMm < 0 || request.CrossStepZMm <= 0 ||
            request.VerticalDownfeedYPerRoughSweepMm < 0 || request.RoughingSweeps < 0 || request.FinishingSweeps < 0 ||
            (long)request.RoughingSweeps + request.FinishingSweeps <= 0 || request.SparkOutRoundTrips < 0 ||
            request.RoughingSweeps == 0 && request.VerticalDownfeedYPerRoughSweepMm != 0)
            throw new ValidationException("Input", "Geometry/pass counts", "Require positive X travel/step, nonnegative Z coverage/Y downfeed/counts, at least one sweep, and no downfeed without roughing sweeps.");
        foreach (var (value, field) in new[] { (request.TableTravelXMm, "X travel mm"), (request.CrossTravelZMm, "Z coverage mm"), (request.CrossStepZMm, "Z step mm"), (request.VerticalDownfeedYPerRoughSweepMm, "Y downfeed mm"), (request.SpindleRpm, "Spindle rpm") })
            Numeric.RequirePrecision(value, profile.DecimalPlaces, field);
        if (request.SpindleRpm <= 0 || request.SpindleRpm < profile["Spindle.MinimumRpm"] || request.SpindleRpm > profile["Spindle.MaximumRpm"])
            throw new ValidationException("Input", "SpindleRpm", $"{Numeric.Format(request.SpindleRpm)} requires the confirmed installed-wheel RPM range.");
        profile.CheckFeed(Axis.X, request.XGrindingFeedMmPerMinute);
        if (request.CrossTravelZMm > 0) profile.CheckFeed(Axis.Z, request.ZCrossFeedMmPerMinute);
        if (request.VerticalDownfeedYPerRoughSweepMm > 0) profile.CheckFeed(Axis.Y, request.YDownfeedMmPerMinute);
        profile.CheckPosition(start);
        var (fullSteps, remainder) = ExactDecimal.DivideIntoSteps(request.CrossTravelZMm, request.CrossStepZMm);
        var steps = fullSteps + (remainder > 0 ? 1 : 0);
        var strokesPerRow = request.CrossStepTiming == CrossStepTiming.AfterRoundTrip ? 2 : 1;
        var sweeps = (decimal)request.RoughingSweeps + request.FinishingSweeps;
        var count = checked(sweeps * ((steps + 1) * strokesPerRow + steps) +
            (request.VerticalDownfeedYPerRoughSweepMm > 0 ? request.RoughingSweeps : 0) + 2m * request.SparkOutRoundTrips);
        if (count > profile["Output.MaximumBlocks"] - 16)
            throw new ValidationException("Input", "Motion count", $"{Numeric.Format(count)} moves exceed the configured block budget.");

        var motions = new List<Motion>();
        var current = start;
        var xOther = ExactDecimal.Add(start.X, request.TableTravelXMm * profile["X.RightSign"] * (request.FirstXDirection == TableDirection.Right ? 1 : -1));
        var zSign = request.CrossTravelZMm > 0 ? profile["Z.InitialCrossSign"] : 0;
        void Move(Axis axis, decimal target, decimal feed)
        {
            if (current[axis] == target) return;
            var next = current.With(axis, target);
            profile.CheckPosition(next); // Axis-aligned segment lies inside the checked rectangular limits.
            Numeric.RequirePrecision(ExactDecimal.Subtract(target, current[axis]), profile.DecimalPlaces, $"{axis} increment");
            motions.Add(new(axis, next, feed));
            current = next;
        }
        void Stroke() => Move(Axis.X, current.X == start.X ? xOther : start.X, request.XGrindingFeedMmPerMinute);
        for (var sweep = 0; sweep < (int)sweeps; sweep++)
        {
            if (sweep < request.RoughingSweeps && request.VerticalDownfeedYPerRoughSweepMm > 0)
                Move(Axis.Y, ExactDecimal.Add(current.Y, request.VerticalDownfeedYPerRoughSweepMm * profile["Y.DownSign"]), request.YDownfeedMmPerMinute);
            var zStart = current.Z;
            var sweepSign = sweep % 2 == 0 ? zSign : -zSign;
            for (var row = 0; row <= (int)steps; row++)
            {
                for (var stroke = 0; stroke < strokesPerRow; stroke++) Stroke();
                if (row == (int)steps) break;
                var coverage = row < fullSteps ? ExactDecimal.Multiply(row + 1m, request.CrossStepZMm) : request.CrossTravelZMm;
                Move(Axis.Z, ExactDecimal.Add(zStart, sweepSign * coverage), request.ZCrossFeedMmPerMinute);
            }
        }
        for (var cycle = 0; cycle < request.SparkOutRoundTrips; cycle++) { Stroke(); Stroke(); }
        return new(start, motions.AsReadOnly());
    }
}

internal static class FagorPostProcessor
{
    public static string Format(MotionPlan plan, GrindingRequest request, ValidatedProfile profile)
    {
        var lines = new List<string>
        {
            $"; PROGRAM {request.ProgramName}", $"; GENERATOR {GenerationService.Version}",
            "; MACHINE DANOBAT RTM-2500 SERIAL 30106A FAGOR 8055M", $"; PROFILE {profile.Version}",
            $"; NOTES {request.OperatorNotes}", "; SETUP", "G71", "G94", "G90",
            $"S{Numeric.Format(request.SpindleRpm)} M03", "; GRINDING"
        };
        if (request.CoordinateMode == CoordinateMode.Incremental) lines.Add("G91");
        var current = plan.StartMm;
        foreach (var motion in plan.Motions)
        {
            var target = motion.TargetMm[motion.Axis];
            var coordinate = request.CoordinateMode == CoordinateMode.Absolute ? target : ExactDecimal.Subtract(target, current[motion.Axis]);
            lines.Add($"G01 {motion.Axis}{Numeric.Format(coordinate)} F{Numeric.Format(motion.FeedMmPerMinute)}");
            current = motion.TargetMm;
        }
        lines.AddRange(["; SHUTDOWN", "M05", "G90", "M30"]);
        if (lines.Count > profile["Output.MaximumBlocks"] || lines.Any(line => line.Length > profile["Output.MaximumBlockLength"]))
            throw new ValidationException("Calculation", "Output blocks", "Generated block count or block length exceeds confirmed controller limits.");
        var text = string.Join(profile.NewLine, lines) + profile.NewLine;
        if (text.Length > profile["Output.MaximumFileBytes"])
            throw new ValidationException("Calculation", "Output size", "Generated file exceeds confirmed byte limit.");
        return text;
    }
}

public sealed record SimulationResult(IReadOnlyList<Motion> Motions, AxisPosition FinalMm, CoordinateMode FinalMode, bool SpindleRequested, decimal RequestedRpm);

// Deliberately independent text interpreter: no postprocessor helpers or planner arithmetic.
public static class FagorParserSimulator
{
    public static SimulationResult Parse(string text, AxisPosition start)
    {
        if (text.Length > 10000000 || text.Any(c => c > 126 || c < 32 && c is not '\r' and not '\n'))
            throw new ValidationException("Parser", "Program", "Unsupported characters or excessive size.");
        var current = start;
        var motions = new List<Motion>();
        var mode = CoordinateMode.Absolute;
        bool metric = false, feedMode = false, initialAbsolute = false, spindle = false, stopped = false, ended = false;
        decimal rpm = 0;
        var block = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            block++;
            if (raw.Contains('\r')) Fail("Invalid line ending.");
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (ended) Fail("Executable block after M30.");
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (words[0])
            {
                case "G71" when words.Length == 1: metric = true; break;
                case "G94" when words.Length == 1: feedMode = true; break;
                case "G90" when words.Length == 1: mode = CoordinateMode.Absolute; initialAbsolute = true; break;
                case "G91" when words.Length == 1:
                    if (!initialAbsolute) Fail("Initial G90 missing.");
                    mode = CoordinateMode.Incremental;
                    break;
                case "M05" when words.Length == 1: spindle = false; stopped = true; break;
                case "M30" when words.Length == 1:
                    if (!metric || !feedMode || !initialAbsolute || mode != CoordinateMode.Absolute || spindle || !stopped) Fail("Incomplete setup or shutdown state.");
                    ended = true;
                    break;
                case "G01":
                    if (!metric || !feedMode || !initialAbsolute || !spindle || stopped || words.Length != 3 ||
                        words[1].Length < 2 || words[2].Length < 2 || words[2][0] != 'F' || !"XYZV".Contains(words[1][0])) Fail("Invalid motion or missing setup/feed/spindle request.");
                    var axis = Enum.Parse<Axis>(words[1][0].ToString());
                    var coordinate = Number(words[1][1..]);
                    var feed = Number(words[2][1..]);
                    if (feed <= 0) Fail("Nonpositive feed.");
                    var target = mode == CoordinateMode.Absolute ? coordinate : checked(current[axis] + coordinate);
                    if (mode == CoordinateMode.Incremental && ParserScaled(target) != ParserScaled(current[axis]) + ParserScaled(coordinate))
                        Fail("Incremental accumulation would lose exact coordinate precision.");
                    if (target == current[axis]) Fail("Zero-length motion.");
                    current = current.With(axis, target);
                    motions.Add(new(axis, current, feed));
                    break;
                default:
                    if (words.Length != 2 || !words[0].StartsWith('S') || words[1] != "M03" || spindle || stopped || !metric || !feedMode || !initialAbsolute)
                        Fail("Unsupported executable block.");
                    rpm = Number(words[0][1..]);
                    if (rpm <= 0) Fail("Invalid RPM.");
                    spindle = true;
                    break;
            }
        }
        if (!ended || motions.Count == 0) Fail("Missing M30 or motion.");
        return new(motions.AsReadOnly(), current, mode, spindle, rpm);

        decimal Number(string token)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"\A-?[0-9]+(?:\.[0-9]+)?\z") ||
                !decimal.TryParse(token, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture, out var result))
            { Fail("Invalid numeric word."); return 0; }
            if (result.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture) != token)
                Fail("Numeric word is not canonical or would round during parsing.");
            return result;
        }
        // Independent representation for checking the interpreter's arithmetic;
        // do not use the planner's ExactDecimal implementation here.
        System.Numerics.BigInteger ParserScaled(decimal value)
        {
            var parts = value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture).Split('.');
            var fraction = parts.Length == 2 ? parts[1] : "";
            var digits = parts[0].TrimStart('-') + fraction.PadRight(28, '0');
            var integer = System.Numerics.BigInteger.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
            return value < 0 ? -integer : integer;
        }
        void Fail(string message) => throw new ValidationException("Parser", $"Block {block}", message);
    }
}

public sealed class ValidatedProgram
{
    public string GCode { get; }
    public string Report { get; }
    public string Sha256 { get; }
    public string FileName { get; }
    public GrindingRequest Request { get; }
    public string ProfileJson { get; }
    public MotionPlan Plan { get; }
    internal ValidatedProgram(string code, string report, string fileName, GrindingRequest request, string profileJson, MotionPlan plan)
    {
        GCode = code; Report = report; FileName = fileName; Request = request; ProfileJson = profileJson; Plan = plan;
        Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(code)));
    }
}

public static class GenerationService
{
    public const string Version = "2.0.0";
    public static ValidatedProgram Generate(GrindingRequest request, MachineProfile profile)
    {
        try
        {
            var json = ProfilePersistence.Serialize(profile);
            var snapshot = ProfilePersistence.Deserialize(json);
            var confirmed = new ValidatedProfile(snapshot, request);
            var plan = Planner.Plan(request, confirmed);
            var code = FagorPostProcessor.Format(plan, request, confirmed);
            var parsed = FagorParserSimulator.Parse(code, plan.StartMm);
            if (!parsed.Motions.SequenceEqual(plan.Motions) || parsed.FinalMm != plan.Motions[^1].TargetMm ||
                parsed.FinalMode != CoordinateMode.Absolute || parsed.SpindleRequested || parsed.RequestedRpm != request.SpindleRpm)
                throw new ValidationException("Calculation", "Round-trip", "Formatted program differs from planned trajectory/feed/modal/spindle state.");
            foreach (var motion in parsed.Motions) { confirmed.CheckPosition(motion.TargetMm); confirmed.CheckFeed(motion.Axis, motion.FeedMmPerMinute); }
            return new(code, Report(plan, request, confirmed), request.ProgramName + confirmed.Text("Output.Extension"), request, json, plan);
        }
        catch (OverflowException)
        {
            throw new ValidationException("Calculation", "Numeric range", "Arithmetic exceeded exact decimal capacity; export blocked.");
        }
    }
    private static string Report(MotionPlan plan, GrindingRequest request, ValidatedProfile profile)
    {
        var output = new StringBuilder();
        output.AppendLine($"Validation passed for configured limits. Operator review required.");
        output.AppendLine("Machine interlocks and physical setup are outside software simulation.");
        output.AppendLine($"Danobat RTM-2500 / 30106A / Fagor 8055M / Profile {profile.Version}");
        output.AppendLine($"Metric mm; feeds mm/min; {request.CoordinateMode}; final mode G90.");
        output.AppendLine($"Move table right: X{(profile["X.RightSign"] == -1 ? "-" : "+")}; first stroke {request.FirstXDirection}.");
        if (request.CrossTravelZMm > 0)
            output.AppendLine($"First Z cross travel: {profile.Text("Z.InitialCrossDirection")}: Z{(profile["Z.InitialCrossSign"] == -1 ? "-" : "+")}.");
        if (request.VerticalDownfeedYPerRoughSweepMm > 0)
            output.AppendLine($"Vertical downfeed: Y{(profile["Y.DownSign"] == -1 ? "-" : "+")}.");
        output.AppendLine(request.CrossStepTiming == CrossStepTiming.AfterRoundTrip ? "Z step after a complete X out-and-back cycle." : "Z step after every X stroke.");
        output.AppendLine("Successive sweeps reverse Z coverage; Y downfeed occurs before each roughing sweep. Finishing sweeps have no downfeed.");
        var positions = new[] { plan.StartMm }.Concat(plan.Motions.Select(m => m.TargetMm)).ToArray();
        foreach (var axis in Enum.GetValues<Axis>())
            output.AppendLine($"{axis} work mm: start {Numeric.Format(plan.StartMm[axis])}; min {Numeric.Format(positions.Min(p => p[axis]))}; max {Numeric.Format(positions.Max(p => p[axis]))}; final {Numeric.Format(positions[^1][axis])}; machine offset {Numeric.Format(profile[$"{axis}.WorkToMachineOffset"])}.");
        decimal distance = 0;
        var previous = plan.StartMm;
        foreach (var move in plan.Motions) { distance = ExactDecimal.Add(distance, Math.Abs(ExactDecimal.Subtract(move.TargetMm[move.Axis], previous[move.Axis]))); previous = move.TargetMm; }
        output.AppendLine($"X strokes: {plan.Motions.Count(m => m.Axis == Axis.X)}; Z steps: {plan.Motions.Count(m => m.Axis == Axis.Z)}; total Y downfeed: {Numeric.Format(ExactDecimal.Multiply(request.VerticalDownfeedYPerRoughSweepMm, request.RoughingSweeps))} mm; distance: {Numeric.Format(distance)} mm.");
        output.AppendLine($"Ends at {(positions[^1].X == plan.StartMm.X ? "starting" : "opposite")} X side. Other axes are not automatically returned.");
        output.AppendLine($"Spindle: S{Numeric.Format(request.SpindleRpm)} M03 request only, followed by M05; rotation/interlocks not verified.");
        output.AppendLine("Coolant commands: disabled. Dresser: disabled. Overrides: none. No automatic retract/home.");
        output.AppendLine($"Reference procedure: {profile.Text("Setup.ReferenceMethod")}");
        output.AppendLine($"Clearance procedure: {profile.Text("Setup.ClearanceProcedure")}");
        output.AppendLine($"Spindle procedure: {profile.Text("Setup.SpindleProcedure")}");
        output.AppendLine($"Operator setup review: {request.SetupReview}");
        return output.ToString();
    }
}
