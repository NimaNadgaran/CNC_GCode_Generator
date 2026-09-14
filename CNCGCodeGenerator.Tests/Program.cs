using CNCGCodeGenerator.Core;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

// Dependency-free regression runner. All machine values below are synthetic test fixtures.
namespace CNCGCodeGenerator.Tests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Strict invariant decimal parsing and exact precision", Numbers),
            ("Small motion cannot disappear at extreme coordinates", ExtremeArithmetic),
            ("Extreme step quotient must not round up and overshoot", ExtremeSteps),
            ("Axis mapping, alternation and independent feeds", AxesAndFeeds),
            ("Cross steps: exact, remainder, oversize, zero coverage", CrossSteps),
            ("Both strategies; single stroke and single cycle", Strategies),
            ("Rough/finish sweeps and final spark-out", Sweeps),
            ("Input rejection and bounded arithmetic", InvalidInputs),
            ("Machine limits, offsets and clearance boundaries", Limits),
            ("Profile evidence, corruption and migration fail closed", Profiles),
            ("Unknown optional functions cannot generate commands", OptionalFunctions),
            ("Independent parser rejects malformed/modal/spindle errors", Parser),
            ("ASCII comments and filename injection prevention", Injection),
            ("Determinism across English, Persian and German cultures", Locales),
            ("Generated property cases: modes, signs, coverage, limits", Properties),
            ("Reviewed golden output", Goldens),
            ("Snapshot invalidation and atomic export integration", Export),
            ("WPF inputs, blocked preview, successful preview and invalidation", WpfIntegration),
            ("Settings input adapter rejects incomplete and fractional values", InputAdapter)
        };
        var failures = 0;
        foreach (var (name, run) in tests)
        {
            try { run(); Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + "\n" + ex); }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} test groups passed; {assertions} assertions.");
        return failures == 0 ? 0 : 1;
    }

    private static int assertions;
    private static void Check(bool value, string message = "Assertion failed")
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Reject(Action action, string? contains = null)
    {
        assertions++;
        try { action(); }
        catch (ValidationException ex)
        {
            if (contains is not null && !ex.Message.Contains(contains, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Expected '{contains}', got '{ex.Message}'");
            return;
        }
        throw new InvalidOperationException("Expected blocking validation error.");
    }
    private static MachineProfile Fixture()
    {
        var profile = MachineProfile.Unknown();
        foreach (var (key, unit) in MachineProfile.RuleUnits)
        {
            var value = key switch
            {
                "X.RightSign" or "Y.DownSign" or "Z.InitialCrossSign" => "-1",
                "Spindle.MinimumRpm" => "1",
                "Spindle.MaximumRpm" => "2000",
                "Output.DecimalPlaces" => "3",
                "Output.MaximumBlocks" => "10000",
                "Output.MaximumBlockLength" => "200",
                "Output.MaximumFileBytes" => "1000000",
                "Output.MaximumNameLength" => "32",
                "Output.BlockNumbersRequired" => "0",
                "Output.Encoding" => "ASCII",
                "Output.LineEndings" => "LF",
                "Output.Extension" => ".test",
                _ when unit == "text" => "Synthetic offline test procedure only",
                _ when key.EndsWith("MinimumFeed") => "0.001",
                _ when key.EndsWith("MaximumFeed") => "2000",
                _ when key.EndsWith("WorkToMachineOffset") => "0",
                _ when key.EndsWith("Minimum") => "-1000",
                _ when key.EndsWith("Maximum") => "1000",
                _ => throw new InvalidOperationException(key)
            };
            profile.Rules[key] = new()
            {
                Value = value,
                Unit = unit,
                Status = Provenance.OperatorEntered,
                Source = "Synthetic test fixture; NOT machine evidence",
                DateConfirmed = "2026-01-01",
                Notes = "OFFLINE ONLY",
                ProfileVersion = "1",
                OperatorApproved = true
            };
        }
        return profile;
    }
    private static MachineProfile Rule(MachineProfile profile, string key, string? value)
    {
        var copy = ProfilePersistence.Deserialize(ProfilePersistence.Serialize(profile));
        copy.Rules[key] = copy.Rules[key] with { Value = value };
        return copy;
    }
    private static GrindingRequest Request() => new()
    {
        StartPositionMm = new(10, 20, 30, 40),
        TableTravelXMm = 6,
        CrossTravelZMm = 5,
        CrossStepZMm = 2,
        XGrindingFeedMmPerMinute = 100,
        YDownfeedMmPerMinute = 3,
        ZCrossFeedMmPerMinute = 20,
        SpindleRpm = 500,
        RoughingSweeps = 1,
        FinishingSweeps = 0,
        SparkOutRoundTrips = 0,
        CoordinateMode = CoordinateMode.Absolute,
        CrossStepTiming = CrossStepTiming.AfterRoundTrip,
        FirstXDirection = TableDirection.Right,
        ProgramName = "TEST",
        SetupReview = "Synthetic offline setup review"
    };
    private static ValidatedProgram Generate(GrindingRequest? request = null, MachineProfile? profile = null) =>
        GenerationService.Generate(request ?? Request(), profile ?? Fixture());

    private static void Numbers()
    {
        foreach (var text in new[] { "", " ", "1,2", "1,000", "1e3", "NaN", "Infinity", "-Infinity", "۱٫۲", "١.٢", "1x", "1\n", ".1", "1.", "1 2", "79228162514264337593543950336", "0.12345678901234567890123456789" })
            Reject(() => Numeric.Parse(text, "test"));
        Check(Numeric.Parse("+0001.2300", "test") == 1.23m);
        Check(Numeric.Parse("-0.000", "test") == 0);
        Check(Numeric.Format(-0.000m) == "0");
        Check(Numeric.Parse("-79228162514264337593543950335", "test") == decimal.MinValue);
        Check(Numeric.Parse("0.0000000000000000000000000001", "test") == 0.0000000000000000000000000001m);
        Reject(() => Generate(Request() with { TableTravelXMm = 0.0001m }), "decimal places");
        Check(Generate(Request() with { TableTravelXMm = 0.001m }).Plan.Motions[0].TargetMm.X == 9.999m);
    }
    private static void AxesAndFeeds()
    {
        var result = Generate(Request() with { VerticalDownfeedYPerRoughSweepMm = 0.05m });
        Check(result.Plan.Motions[0] == new Motion(Axis.Y, new(10, 19.95m, 30, 40), 3));
        var x = result.Plan.Motions.Where(m => m.Axis == Axis.X).ToArray();
        for (var i = 0; i < x.Length; i++) Check(x[i].TargetMm.X == (i % 2 == 0 ? 4 : 10));
        Check(result.Plan.Motions.All(m => m.TargetMm.V == 40));
        Check(result.GCode.Contains("G01 Y19.95 F3"));
        Check(result.GCode.Contains("G01 Z28 F20"));
        Check(result.GCode.Contains("G01 X4 F100"));
        Check(!result.GCode.Contains("G21") && !result.GCode.Contains("G00") && !result.GCode.Contains('%'));
        Check(result.GCode.EndsWith("M05\nG90\nM30\n"));
        Check(Generate(Request() with { FirstXDirection = TableDirection.Left }).Plan.Motions[0].TargetMm.X == 16);
        Check(Generate(profile: Rule(Fixture(), "X.RightSign", "1")).Plan.Motions[0].TargetMm.X == 16);
    }
    private static void ExtremeArithmetic()
    {
        var profile = Fixture();
        profile = Rule(profile, "Y.Minimum", "-79228162514264337593543950335");
        profile = Rule(profile, "Y.Maximum", "79228162514264337593543950335");
        profile = Rule(profile, "Y.ClearanceMinimum", "-79228162514264337593543950335");
        profile = Rule(profile, "Y.ClearanceMaximum", "79228162514264337593543950335");
        Reject(() => Generate(Request() with
        {
            StartPositionMm = new(10, 10000000000000000000000000000m, 30, 40),
            VerticalDownfeedYPerRoughSweepMm = 0.001m
        }, profile), "exact");
    }
    private static void CrossSteps()
    {
        foreach (var (coverage, step, expected) in new[] { (6m, 2m, new[] { 28m, 26m, 24m }), (5m, 2m, new[] { 28m, 26m, 25m }), (1m, 9m, new[] { 29m }), (0m, 2m, Array.Empty<decimal>()) })
        {
            var result = Generate(Request() with { CrossTravelZMm = coverage, CrossStepZMm = step });
            Check(result.Plan.Motions.Where(m => m.Axis == Axis.Z).Select(m => m.TargetMm.Z).SequenceEqual(expected));
            Check(result.Plan.Motions[^1].Axis == Axis.X);
            Check(result.Plan.Motions.Count(m => m.Axis == Axis.X) == 2 * (expected.Length + 1));
        }
    }
    private static void ExtremeSteps()
    {
        var profile = Fixture();
        foreach (var key in new[] { "Minimum", "ClearanceMinimum" })
            profile = Rule(profile, "Z." + key, "-79228162514264337593543950335");
        foreach (var key in new[] { "Maximum", "ClearanceMaximum" })
            profile = Rule(profile, "Z." + key, "79228162514264337593543950335");
        var coverage = 69999999999999999999999999999m;
        var program = Generate(Request() with
        {
            StartPositionMm = new(10, 20, 0, 40),
            CrossTravelZMm = coverage,
            CrossStepZMm = 70000000000000000000000000000m
        }, profile);
        Check(program.Plan.Motions.Where(m => m.Axis == Axis.Z).All(m => m.TargetMm.Z >= -coverage));
        Check(program.Plan.Motions.Count(m => m.Axis == Axis.Z) == 1);
    }
    private static void Strategies()
    {
        var stroke = Generate(Request() with { CrossTravelZMm = 0, CrossStepTiming = CrossStepTiming.AfterStroke });
        Check(stroke.Plan.Motions.Count == 1 && stroke.Plan.Motions[^1].TargetMm.X == 4);
        var cycle = Generate(Request() with { CrossTravelZMm = 0 });
        Check(cycle.Plan.Motions.Count == 2 && cycle.Plan.Motions[^1].TargetMm.X == 10);
        var afterStroke = Generate(Request() with { CrossStepTiming = CrossStepTiming.AfterStroke });
        Check(string.Concat(afterStroke.Plan.Motions.Select(m => m.Axis)) == "XZXZXZX");
        Check(string.Concat(Generate().Plan.Motions.Select(m => m.Axis)) == "XXZXXZXXZXX");
    }
    private static void Sweeps()
    {
        var result = Generate(Request() with { RoughingSweeps = 2, FinishingSweeps = 1, SparkOutRoundTrips = 2, VerticalDownfeedYPerRoughSweepMm = 0.1m });
        Check(result.Plan.Motions.Count(m => m.Axis == Axis.Y) == 2);
        Check(result.Plan.Motions[^1].TargetMm == new AxisPosition(10, 19.8m, 25, 40));
        Check(result.Plan.Motions.Count(m => m.Axis == Axis.X) == 28);
        Check(result.Plan.Motions.TakeLast(4).All(m => m.Axis == Axis.X && m.TargetMm.Z == 25));
        var z = result.Plan.Motions.Where(m => m.Axis == Axis.Z).Select(m => m.TargetMm.Z).ToArray();
        Check(z.SequenceEqual(new[] { 28m, 26m, 25m, 27m, 29m, 30m, 28m, 26m, 25m }));
        var finishingOnly = Generate(Request() with { RoughingSweeps = 0, FinishingSweeps = 1 });
        Check(finishingOnly.Plan.Motions.All(m => m.Axis != Axis.Y));
    }
    private static void InvalidInputs()
    {
        var request = Request();
        foreach (var invalid in new[]
        {
            request with { TableTravelXMm = 0 }, request with { TableTravelXMm = -1 },
            request with { CrossTravelZMm = -1 }, request with { CrossStepZMm = 0 }, request with { CrossStepZMm = -1 },
            request with { VerticalDownfeedYPerRoughSweepMm = -1 }, request with { XGrindingFeedMmPerMinute = 0 },
            request with { XGrindingFeedMmPerMinute = -1 }, request with { ZCrossFeedMmPerMinute = 0 },
            request with { VerticalDownfeedYPerRoughSweepMm = 1, YDownfeedMmPerMinute = 0 },
            request with { SpindleRpm = 0 }, request with { SpindleRpm = -1 }, request with { SpindleRpm = 2001 },
            request with { RoughingSweeps = -1 }, request with { RoughingSweeps = 0 }, request with { SparkOutRoundTrips = -1 },
            request with { FinishingSweeps = -1 }, request with { RoughingSweeps = int.MaxValue },
            request with { SparkOutRoundTrips = int.MaxValue }, request with { CrossStepZMm = 0.001m, CrossTravelZMm = 1000 },
            request with { StartPositionMm = null }, request with { SetupReview = "" },
            request with { CoordinateMode = (CoordinateMode)99 }, request with { FirstXDirection = (TableDirection)99 },
            request with { CrossStepTiming = (CrossStepTiming)99 },
            request with { TableTravelXMm = decimal.MaxValue, FirstXDirection = TableDirection.Left },
            request with { CrossTravelZMm = decimal.MaxValue, CrossStepZMm = 0.001m }
        }) Reject(() => Generate(invalid));
    }
    private static void Limits()
    {
        var request = Request() with { StartPositionMm = new(0, 0, 0, 0), TableTravelXMm = 1000 };
        Check(Generate(request).Plan.Motions[0].TargetMm.X == -1000);
        Reject(() => Generate(request with { TableTravelXMm = 1000.001m }), "trajectory");
        Reject(() => Generate(request with { StartPositionMm = new(1001, 0, 0, 0) }), "trajectory");
        Reject(() => Generate(profile: Rule(Fixture(), "V.Maximum", null)), "V.Maximum");
        Reject(() => Generate(profile: Rule(Fixture(), "X.WorkToMachineOffset", "2000")), "trajectory");
        Check(Generate(request, Rule(Fixture(), "X.WorkToMachineOffset", "1000")).Plan.Motions[0].TargetMm.X == -1000);
        Reject(() => Generate(profile: Rule(Fixture(), "X.ClearanceMinimum", "5")), "clearance");
        Check(Generate(profile: Rule(Fixture(), "X.ClearanceMinimum", "4")).Plan.Motions[0].TargetMm.X == 4);
        Reject(() => Generate(profile: Rule(Fixture(), "X.ClearanceMinimum", "4.001")), "clearance");
        Reject(() => Generate(profile: Rule(Fixture(), "X.Minimum", "1000")), "limits");
        Reject(() => Generate(profile: Rule(Fixture(), "X.RightSign", "0")), "sign");
        Reject(() => Generate(profile: Rule(Fixture(), "Output.DecimalPlaces", "1.5")));
        Reject(() => Generate(profile: Rule(Fixture(), "Output.MaximumBlocks", "10")));
        Reject(() => Generate(profile: Rule(Fixture(), "Output.MaximumBlockLength", "16")), "block");
        Reject(() => Generate(profile: Rule(Fixture(), "Output.MaximumFileBytes", "1")), "byte");
        Reject(() => Generate(profile: Rule(Fixture(), "Output.BlockNumbersRequired", "1")));
        Reject(() => Generate(profile: Rule(Fixture(), "Spindle.MinimumRpm", "3000")));
        Reject(() => Generate(profile: Rule(Fixture(), "Y.MaximumFeed", "0")));
    }
    private static void Profiles()
    {
        Reject(() => Generate(profile: MachineProfile.Unknown()), "Unknown machine data");
        var profile = Fixture();
        var json = ProfilePersistence.Serialize(profile);
        Check(Generate(profile: ProfilePersistence.Deserialize(json)).GCode == Generate(profile: profile).GCode);
        foreach (var invalid in new[] { "{", "null", "{}", json.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 0"), json.Replace("30106A", "OTHER"), json.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 1, \"SchemaVersion\": 1"), json.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 1, \"Extra\": true") })
            Reject(() => ProfilePersistence.Deserialize(invalid));
        foreach (var status in new[] { Provenance.Unknown, Provenance.Assumed, Provenance.OperatorEntered })
        {
            var copy = Fixture();
            copy.Rules["X.RightSign"] = copy.Rules["X.RightSign"] with { Status = status, OperatorApproved = false };
            Reject(() => Generate(profile: copy), "X.RightSign");
        }
        var missing = Fixture(); missing.Rules.Remove("X.Minimum");
        Reject(() => ProfilePersistence.Deserialize(ProfilePersistence.Serialize(missing)));
        var invalidDate = Fixture(); invalidDate.Rules["X.Minimum"] = invalidDate.Rules["X.Minimum"] with { DateConfirmed = "yesterday" };
        Reject(() => Generate(profile: invalidDate));
        Reject(() => Generate(profile: Rule(Fixture(), "Output.Encoding", "UTF-8")));
        Reject(() => Generate(profile: Rule(Fixture(), "Output.Extension", "../nc")));
        Reject(() => Generate(profile: Rule(Fixture(), "Output.LineEndings", "CR")));
    }
    private static void OptionalFunctions()
    {
        Reject(() => Generate(Request() with { DressingEnabled = true }), "Dresser axis is V");
        Reject(() => Generate(Request() with { CoolantEnabled = true }), "disabled");
        var code = Generate().GCode;
        Check(!code.Contains("M08") && !code.Contains("M09") && !code.Contains("G01 V") && !code.Contains("G01 Y"));
        var profile = Rule(Rule(Fixture(), "Y.DownSign", null), "Z.InitialCrossSign", null);
        foreach (var axis in new[] { "Y", "Z", "V" })
            foreach (var limit in new[] { "MinimumFeed", "MaximumFeed" }) profile = Rule(profile, axis + "." + limit, null);
        Check(Generate(Request() with { CrossTravelZMm = 0 }, profile).Plan.Motions.Count == 2);
    }
    private static void Parser()
    {
        var good = Generate().GCode;
        var start = Request().StartPositionMm!;
        foreach (var broken in new[]
        {
            good.Replace("G71", "G21"), good.Replace("G94\n", ""), good.Replace("M05\n", ""),
            good.Replace("G90\nM30", "G91\nM30"), good.Replace("M30\n", ""), good + "G01 X0 F1\n",
            good.Replace(" F100", ""), good.Replace("F100", "F0"), good.Replace("F100", "FNaN"),
            good.Replace("S500 M03", "S500 M3"), good.Replace("S500 M03", "O02"),
            good.Replace("G01 X4", "G01 X4 Y8"), good.Replace("X4", "X4,1"),
            good.Replace("S500 M03\n", ""), good.Replace("G71", "G71\nM08"),
            good.Replace("G90", "G91"), good.Replace("G01 X4", "G01 X10"),
            good.Replace("X4", "X0.12345678901234567890123456789")
        }) Reject(() => FagorParserSimulator.Parse(broken, start));
        var changed = FagorParserSimulator.Parse(good.Replace("F100", "F101"), start);
        Check(!changed.Motions.SequenceEqual(Generate().Plan.Motions), "Semantic comparison must detect changed feed.");
        var v = FagorParserSimulator.Parse("G71\nG94\nG90\nS500 M03\nG01 V39 F2\nM05\nG90\nM30\n", start);
        Check(v.Motions[0].Axis == Axis.V && v.Motions[0].FeedMmPerMinute == 2 && v.FinalMm.Y == 20);
    }
    private static void Injection()
    {
        foreach (var name in new[] { "../evil", "CON", "COM1", "A\nM03", "A;M03", "نام", "%", "A.txt", "A B", "" })
            Reject(() => Generate(Request() with { ProgramName = name }));
        foreach (var notes in new[] { "a\nM03", "a\rM03", "a;M03", "%", "(test)", "\t", "فارسی", "\0" })
            Reject(() => Generate(Request() with { OperatorNotes = notes }));
        Check(Generate(Request() with { OperatorNotes = "M03 is only a request" }).GCode.Contains("; NOTES M03 is only a request"));
    }
    private static void Locales()
    {
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            var expected = Generate(Request() with { VerticalDownfeedYPerRoughSweepMm = 0.025m }).GCode;
            foreach (var name in new[] { "en-US", "fa-IR", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                Check(Generate(Request() with { VerticalDownfeedYPerRoughSweepMm = 0.025m }).GCode == expected);
                Check(Numeric.Parse("1.25", "test") == 1.25m);
                Reject(() => Numeric.Parse("1,25", "test"));
            }
        }
        finally { CultureInfo.CurrentCulture = original; CultureInfo.CurrentUICulture = originalUi; }
    }
    private static void Properties()
    {
        var random = new Random(30106);
        for (var i = 0; i < 300; i++)
        {
            var request = Request() with
            {
                TableTravelXMm = random.Next(1, 1000) / 10m,
                CrossTravelZMm = random.Next(0, 100) / 10m,
                CrossStepZMm = random.Next(1, 100) / 10m,
                CrossStepTiming = (CrossStepTiming)random.Next(2),
                FirstXDirection = (TableDirection)random.Next(2),
                RoughingSweeps = random.Next(1, 4),
                FinishingSweeps = random.Next(0, 3),
                VerticalDownfeedYPerRoughSweepMm = random.Next(0, 10) / 100m,
                SparkOutRoundTrips = random.Next(0, 4)
            };
            var profile = Rule(Fixture(), "Z.InitialCrossSign", random.Next(2) == 0 ? "1" : "-1");
            var absolute = Generate(request, profile);
            var incremental = Generate(request with { CoordinateMode = CoordinateMode.Incremental }, profile);
            Check(absolute.Plan.Motions.SequenceEqual(incremental.Plan.Motions));
            Check(absolute.GCode == Generate(request, profile).GCode);
            Check(incremental.GCode.EndsWith("M05\nG90\nM30\n"));
            var parsed = FagorParserSimulator.Parse(incremental.GCode, request.StartPositionMm!);
            Check(parsed.Motions.SequenceEqual(absolute.Plan.Motions));
            Check(parsed.FinalMode == CoordinateMode.Absolute && !parsed.SpindleRequested);
            Check(absolute.Plan.Motions.All(m => Math.Abs(m.TargetMm.Z - request.StartPositionMm!.Z) <= request.CrossTravelZMm));
            foreach (var axis in Enum.GetValues<Axis>()) Check(parsed.Motions.All(m => m.TargetMm[axis] >= -1000 && m.TargetMm[axis] <= 1000));
            Check(!absolute.GCode.Contains(',') && !absolute.GCode.Contains("NaN") && !absolute.GCode.Contains("Infinity"));
        }
    }
    private static void Goldens()
    {
        var cases = new Dictionary<string, GrindingRequest>
        {
            ["absolute"] = Request(),
            ["incremental"] = Request() with { CoordinateMode = CoordinateMode.Incremental },
            ["simple"] = Request() with { CrossTravelZMm = 0 },
            ["multi-sweep"] = Request() with { CrossTravelZMm = 2, RoughingSweeps = 2, VerticalDownfeedYPerRoughSweepMm = 0.05m },
            ["finish"] = Request() with { CrossTravelZMm = 0, RoughingSweeps = 1, FinishingSweeps = 1, SparkOutRoundTrips = 1, VerticalDownfeedYPerRoughSweepMm = 0.05m }
        };
        foreach (var (name, request) in cases)
        {
            var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", name + ".txt")).Replace("\r\n", "\n");
            Check(Generate(request).GCode == expected, "Golden mismatch: " + name);
        }
    }
    private static void Export()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CncGeneratorTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "TEST.test");
        try
        {
            var session = new PreviewSession();
            Reject(() => session.Export(path, false));
            var profile = Fixture();
            var result = session.Generate(Request(), profile);
            Check(!File.Exists(path), "Cancelled export must create no file.");
            // The validated snapshot contains immutable request/plan/text, not references to mutable profile rules.
            profile.Rules["X.RightSign"] = profile.Rules["X.RightSign"] with { Value = "1" };
            Check(result.ProfileJson.Contains("\"-1\""));
            session.Export(path, false);
            Check(File.ReadAllBytes(path).SequenceEqual(Encoding.ASCII.GetBytes(result.GCode)));
            Check(session.LastExport?.Success == true && session.LastExport.ProgramSha256 == result.Sha256);
            Reject(() => session.Export(path, false), "confirmation");
            Check(session.LastExport?.Success == false);
            File.WriteAllText(path, "old content");
            session.Export(path, true);
            Check(Directory.GetFiles(directory, "*.bak").Length == 1);
            Check(File.ReadAllText(Directory.GetFiles(directory, "*.bak")[0]) == "old content");
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0);
            Reject(() => session.Export(Path.Combine(directory, "renamed.test"), false), "filename");
            session.Invalidate();
            Reject(() => session.Export(path, true));
            session.Generate(Request(), Fixture());
            Reject(() => session.Generate(Request() with { SpindleRpm = 0 }, Fixture()));
            Check(session.Current is null);
            Reject(() => session.Export(path, true));
            var profilePath = Path.Combine(directory, "profile.json");
            AtomicFile.Write(profilePath, Encoding.UTF8.GetBytes(ProfilePersistence.Serialize(Fixture())), false);
            Check(Generate(profile: ProfilePersistence.Deserialize(File.ReadAllText(profilePath))).GCode == result.GCode);
            var crlf = Generate(profile: Rule(Fixture(), "Output.LineEndings", "CRLF"));
            Check(crlf.GCode.Contains("\r\n") && crlf.GCode.Replace("\r\n", "").IndexOf('\n') < 0);
            try { AtomicFile.Write(Path.Combine(directory, "missing", "TEST.test"), [1, 2], false); throw new InvalidOperationException("Expected IO failure"); }
            catch (DirectoryNotFoundException) { Check(!File.Exists(Path.Combine(directory, "missing", "TEST.test"))); }
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private static OperationSettings Settings()
    {
        var settings = new OperationSettings();
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["StartX"] = "10",
            ["StartY"] = "20",
            ["StartZ"] = "30",
            ["StartV"] = "40",
            ["XFeed"] = "100",
            ["YFeed"] = "3",
            ["ZFeed"] = "20",
            ["ZStep"] = "2",
            ["Rpm"] = "500",
            ["Roughing"] = "1",
            ["Finishing"] = "0",
            ["SparkOut"] = "0",
            ["Name"] = "TEST",
            ["Notes"] = "",
            ["Review"] = "Synthetic offline setup review"
        }) settings.Values[key] = value;
        return settings;
    }
    private static void WpfIntegration()
    {
        var window = new MainWindow();
        try
        {
            var x = (TextBox)window.FindName("XInput"); var y = (TextBox)window.FindName("YInput"); var z = (TextBox)window.FindName("ZInput");
            var save = (Button)window.FindName("SaveButton"); var output = (TextBox)window.FindName("GCodeOutput");
            Check(!save.IsEnabled && x.Text == "" && y.Text == "" && z.Text == "");
            Set(window, "profileLoadError", null); Set(window, "operation", Settings()); Set(window, "profile", Fixture());
            x.Text = "6"; y.Text = "0"; z.Text = "5";
            Invoke(window, "GenerateButton_Click", window, new RoutedEventArgs());
            Check(save.IsEnabled && output.Text.Contains("G71") && output.Text.Contains("final 25"));
            x.Text = "7";
            Check(!save.IsEnabled && output.Text == "");
            x.Text = "6"; Invoke(window, "GenerateButton_Click", window, new RoutedEventArgs());
            Set(window, "profile", MachineProfile.Unknown()); Invoke(window, "InvalidatePreview");
            Check(!save.IsEnabled && output.Text == "");
            Invoke(window, "GenerateButton_Click", window, new RoutedEventArgs());
            Check(!save.IsEnabled && output.Text.Contains("EXPORT BLOCKED"));
            Set(window, "profile", Fixture()); x.Text = "NaN";
            Invoke(window, "GenerateButton_Click", window, new RoutedEventArgs());
            Check(!save.IsEnabled && output.Text.Contains("EXPORT BLOCKED"));
            var settings = new SettingsWindow(Settings(), Fixture(), "fa");
            Check(settings.SelectedLanguage == "fa"); settings.Close();
        }
        finally { window.Close(); }
    }
    private static void Set(object instance, string field, object? value) => instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);
    private static void Invoke(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, args);
    private static void InputAdapter()
    {
        Check(Settings().CreateRequest("6", "0", "5") == Request());
        var settings = Settings(); settings.Values["Roughing"] = "1.5";
        Reject(() => settings.CreateRequest("6", "0", "5"), "whole count");
        settings = Settings(); settings.Values["StartV"] = "";
        Reject(() => settings.CreateRequest("6", "0", "5"), "V");
        Reject(() => new OperationSettings().CreateRequest("6", "0", "5"));
    }
}
