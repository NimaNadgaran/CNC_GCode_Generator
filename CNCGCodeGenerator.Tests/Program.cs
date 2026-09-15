using CNCGCodeGenerator.Core;
using System.Globalization;
using System.IO;
using System.Text;

namespace CNCGCodeGenerator.Tests;

internal static class Program
{
    private static int assertions;
    [STAThread]
    private static int Main()
    {
        var tests = new Action[] { Numbers, Paths, FinishingRegression, Validation, Parser, Export, Locale, Persistence };
        var failed = 0; foreach (var test in tests) try { test(); Console.WriteLine("PASS " + test.Method.Name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + test.Method.Name + ": " + ex.Message); }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} groups; {assertions} assertions."); return failed == 0 ? 0 : 1;
    }
    private static void Check(bool value, string message = "Assertion failed") { assertions++; if (!value) throw new InvalidOperationException(message); }
    private static void Reject(Action action) { assertions++; try { action(); } catch (ValidationException) { return; } throw new InvalidOperationException("Expected validation failure."); }
    private static GrindingRequest Request() => new() { StartPositionMm = new(10, 20, 30), TableTravelXMm = 6, CrossTravelZMm = 5, CrossStepZMm = 2, VerticalDownfeedYPerRoughSweepMm = .02m, XGrindingFeedMmPerMinute = 100, YDownfeedMmPerMinute = 5, ZCrossFeedMmPerMinute = 20, SpindleRpm = 500, RoughingSweeps = 1, FinishingSweeps = 0, SparkOutRoundTrips = 0, ProgramName = "000101", ProgramComment = "offline test" };
    private static ValidatedProgram Generate(GrindingRequest? request = null) => GenerationService.Generate(request ?? Request());
    private static void Numbers() { foreach (var value in new[] { "", "1,2", "1e3", "NaN", "Infinity", "١.٢" }) Reject(() => Numeric.Parse(value, "test")); Check(Numeric.Parse("-0.000", "test") == 0); Check(Numeric.Format(-0m) == "0"); Check(Numeric.Parse("1.25", "test") == 1.25m); }
    private static void Paths()
    {
        var result = Generate(); Check(result.Plan.Motions[1].Axis == Axis.X && result.Plan.Motions[1].TargetMm.X == 4, "Physical RIGHT must command X-"); Check(result.Plan.Motions[2].TargetMm.X == 10, "X return");
        Check(result.Plan.Motions.Count(m => m.Axis == Axis.Y) == 1, "rough Y"); Check(result.Plan.Motions.Count(m => m.Axis == Axis.Z) == 3, "Z count"); Check(result.Plan.Motions[^1].TargetMm.Z == 25, "final Z"); Check(result.FileName == "000101.PIM", "extension"); Check(result.GCode.EndsWith("M05\nG90\nM30\n"), "shutdown");
        var finish = Generate(Request() with { RoughingSweeps = 0, FinishingSweeps = 1, VerticalDownfeedYPerRoughSweepMm = 0 }); Check(finish.Plan.Motions.All(m => m.Axis != Axis.Y));
        var zero = Generate(Request() with { CrossTravelZMm = 0 }); Check(zero.Plan.Motions.All(m => m.Axis != Axis.Z));
        var extra = Generate(Request() with { CrossTravelZMm = 0, SparkOutRoundTrips = 2 }); Check(extra.Plan.Motions.Count(m => m.Axis == Axis.X) == 6, "extra rounds");
        var partial = Generate(Request() with { CrossTravelZMm = 210, CrossStepZMm = 20 }); Check(partial.Plan.Motions.Max(m => m.Axis == Axis.Z ? Math.Abs(m.TargetMm.Z - 30) : 0) == 210); Check(partial.Plan.Motions.All(m => m.Axis != Axis.Z || Math.Abs(m.TargetMm.Z - 30) <= 210));
    }
    private static void Validation() { Reject(() => Generate(Request() with { TableTravelXMm = 0 })); Reject(() => Generate(Request() with { SpindleRpm = 0 })); Reject(() => Generate(Request() with { CrossTravelZMm = 1, CrossStepZMm = 0 })); Reject(() => Generate(Request() with { ProgramName = "TEST" })); Reject(() => Generate(Request() with { RoughingSweeps = 0, FinishingSweeps = 0, VerticalDownfeedYPerRoughSweepMm = 0 })); }
    private static void FinishingRegression()
    {
        var request = Request() with { StartPositionMm = new(1200, 250, 500), TableTravelXMm = 400, CrossTravelZMm = 81, CrossStepZMm = 40, XGrindingFeedMmPerMinute = 1000, YDownfeedMmPerMinute = 50, ZCrossFeedMmPerMinute = 100, RoughingSweeps = 0, FinishingSweeps = 2, SparkOutRoundTrips = 1, VerticalDownfeedYPerRoughSweepMm = 0, ProgramName = "000110" };
        var result = Generate(request); Check(result.Plan.Motions.All(m => m.Axis != Axis.Y)); Check(result.Plan.Motions.Count(m => m.Axis == Axis.X) == 18); Check(result.Plan.Motions.Where(m => m.Axis == Axis.Z).Select(m => m.TargetMm.Z).SequenceEqual(new[] { 460m, 420m, 419m, 459m, 499m, 500m })); Check(result.GCode.Contains("M05\nG90\nM30\n"));
        Reject(() => Generate(request with { VerticalDownfeedYPerRoughSweepMm = .001m }));
        Reject(() => Generate(request with { RoughingSweeps = 1, VerticalDownfeedYPerRoughSweepMm = 0 }));
        Reject(() => Generate(request with { RoughingSweeps = 1, VerticalDownfeedYPerRoughSweepMm = -1 }));
        Reject(() => Generate(request with { RoughingSweeps = 1, VerticalDownfeedYPerRoughSweepMm = decimal.MinValue }));
        var settings = new CNCGCodeGenerator.OperationSettings(); foreach (var key in settings.Values.Keys) settings.Values[key] = key switch { "StartX" => "1200", "StartY" => "250", "StartZ" => "500", "XFeed" => "1000", "YFeed" => "50", "ZFeed" => "100", "ZStep" => "40", "Rpm" => "500", "Roughing" => "0", "Finishing" => "2", "SparkOut" => "1", "Name" => "000110", "Comment" => "", _ => "" }; var normalized = settings.CreateRequest("400", "", "81"); Check(normalized.VerticalDownfeedYPerRoughSweepMm == 0); Check(Generate(normalized).Plan.Motions.All(m => m.Axis != Axis.Y));
    }
    private static void Parser() { var result = Generate(); var parsed = FagorParserSimulator.Parse(result.GCode, result.Plan.StartMm); Check(parsed.Motions.SequenceEqual(result.Plan.Motions)); Check(parsed.FinalMode == CoordinateMode.Absolute); Check(parsed.FinalMm == result.Plan.Motions[^1].TargetMm); Reject(() => FagorParserSimulator.Parse(result.GCode.Replace("G71", "G21"), result.Plan.StartMm)); }
    private static void Export()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CncGenerator-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); var path = Path.Combine(dir, "000101.PIM"); try { var session = new PreviewSession(); session.Generate(Request()); Reject(() => session.Export(path, false)); session.Export(path, false, true); Check(File.ReadAllText(path) == Generate().GCode); } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    private static void Locale() { var old = CultureInfo.CurrentCulture; try { var expected = Generate().GCode; CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fa-IR"); Check(expected == Generate().GCode); } finally { CultureInfo.CurrentCulture = old; } }
    private static void Persistence()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CncGenerator-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); var path = Path.Combine(dir, "settings.json");
        try
        {
            var operation = new CNCGCodeGenerator.OperationSettings();
            foreach (var key in operation.Values.Keys) operation.Values[key] = key switch
            {
                "StartX" => "123", "StartY" => "456", "StartZ" => "789", "XFeed" => "1000", "YFeed" => "5", "ZFeed" => "60", "ZStep" => "20", "Rpm" => "500", "Roughing" => "1", "Finishing" => "2", "SparkOut" => "0", "Name" => "000101", "Comment" => "saved", _ => ""
            };
            UserSettingsStore.Save(path, operation, "600", "0.020", "210", "fa", 1, -1);
            var loaded = UserSettingsStore.Load(path);
            Check(loaded.XTravel == "600" && loaded.YDownfeed == "0.020" && loaded.ZCoverage == "210");
            Check(loaded.Language == "fa" && loaded.YDownSign == 1 && loaded.ZInitialCrossSign == -1);
            Check(loaded.Operation.Values["Comment"] == "saved");
            Check(loaded.Operation.Values["StartX"] == "" && loaded.Operation.Values["StartY"] == "" && loaded.Operation.Values["StartZ"] == "");
            File.WriteAllText(path, "{\"SchemaVersion\":999}"); Reject(() => UserSettingsStore.Load(path));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
