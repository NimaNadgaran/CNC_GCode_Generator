using System.Text.Json;
using System.Text.Json.Serialization;

namespace CNCGCodeGenerator.Core;

public sealed record MachineRule
{
    public required string? Value { get; init; }
    public required string Unit { get; init; }
    public required Provenance Status { get; init; }
    public required string Source { get; init; }
    public required string? DateConfirmed { get; init; }
    public required string Notes { get; init; }
    public required string ProfileVersion { get; init; }
    public required bool OperatorApproved { get; init; }
}

public sealed record MachineProfile
{
    public required int SchemaVersion { get; init; }
    public required string ProfileVersion { get; init; }
    public required string Manufacturer { get; init; }
    public required string Model { get; init; }
    public required string SerialNumber { get; init; }
    public required string Controller { get; init; }
    public required Dictionary<string, MachineRule> Rules { get; init; }

    public static IReadOnlyDictionary<string, string> RuleUnits { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(CreateRuleUnits());
    private static Dictionary<string, string> CreateRuleUnits()
    {
        var result = new Dictionary<string, string>();
        foreach (var axis in Enum.GetValues<Axis>())
        {
            result[$"{axis}.Minimum"] = "mm";
            result[$"{axis}.Maximum"] = "mm";
            result[$"{axis}.WorkToMachineOffset"] = "mm";
            result[$"{axis}.MinimumFeed"] = "mm/min";
            result[$"{axis}.MaximumFeed"] = "mm/min";
            result[$"{axis}.ClearanceMinimum"] = "mm";
            result[$"{axis}.ClearanceMaximum"] = "mm";
        }
        result["X.RightSign"] = "+1 or -1";
        result["Y.DownSign"] = "+1 or -1";
        result["Z.InitialCrossSign"] = "+1 or -1";
        result["Z.InitialCrossDirection"] = "text";
        result["Spindle.MinimumRpm"] = "rpm";
        result["Spindle.MaximumRpm"] = "rpm";
        result["Output.DecimalPlaces"] = "digits";
        result["Output.MaximumBlocks"] = "blocks";
        result["Output.MaximumBlockLength"] = "characters";
        result["Output.MaximumFileBytes"] = "bytes";
        result["Output.Extension"] = "text";
        result["Output.Encoding"] = "ASCII";
        result["Output.LineEndings"] = "CRLF or LF";
        result["Output.MaximumNameLength"] = "characters";
        result["Output.BlockNumbersRequired"] = "0 or 1";
        result["Setup.ReferenceMethod"] = "text";
        result["Setup.ClearanceProcedure"] = "text";
        result["Setup.SpindleProcedure"] = "text";
        return result;
    }

    public static MachineProfile Unknown() => new()
    {
        SchemaVersion = 1,
        ProfileVersion = "1",
        Manufacturer = "Danobat",
        Model = "RTM-2500",
        SerialNumber = "30106A",
        Controller = "Fagor 8055M",
        Rules = RuleUnits.ToDictionary(pair => pair.Key, pair => new MachineRule
        {
            Value = null,
            Unit = pair.Value,
            Status = Provenance.Unknown,
            Source = "",
            DateConfirmed = null,
            Notes = pair.Key == "X.RightSign" ? "AGENTS.md reports rightward X negative; reconfirm during commissioning." : "",
            ProfileVersion = "1",
            OperatorApproved = false
        })
    };
}

public static class ProfilePersistence
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false) }
    };
    public static string Serialize(MachineProfile profile) => JsonSerializer.Serialize(profile, Options);
    public static MachineProfile Deserialize(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            RejectDuplicateProperties(document.RootElement);
            var profile = JsonSerializer.Deserialize<MachineProfile>(json, Options)
                ?? throw new JsonException("Empty profile.");
            CheckSchema(profile);
            return profile;
        }
        catch (JsonException ex)
        {
            throw new ValidationException("Profile", "File", $"Invalid profile; operator review required. {ex.Message}");
        }
    }
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property {property.Name}.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }
    internal static void CheckSchema(MachineProfile profile)
    {
        if (profile.SchemaVersion != 1 || profile.Manufacturer != "Danobat" || profile.Model != "RTM-2500" ||
            profile.SerialNumber != "30106A" || profile.Controller != "Fagor 8055M" ||
            string.IsNullOrWhiteSpace(profile.ProfileVersion) || profile.Rules is null ||
            profile.Rules.Count != MachineProfile.RuleUnits.Count ||
            MachineProfile.RuleUnits.Any(pair => !profile.Rules.TryGetValue(pair.Key, out var rule) || rule is null ||
                rule.Unit != pair.Value || rule.ProfileVersion != profile.ProfileVersion || !Enum.IsDefined(rule.Status)))
            throw new ValidationException("Profile", "Schema/identity/rules", "Unsupported, incomplete or inconsistent profile; no automatic safety-value migration is permitted.");
        TextValidation.Comment(profile.ProfileVersion, "ProfileVersion");
    }
}

internal sealed class ValidatedProfile
{
    private readonly MachineProfile profile;
    private readonly Dictionary<string, decimal> numbers = [];
    public string Version => profile.ProfileVersion;
    public decimal this[string key] => numbers[key];
    public int DecimalPlaces => (int)this["Output.DecimalPlaces"];
    public string Text(string key) => profile.Rules[key].Value!;
    public string NewLine => Text("Output.LineEndings") == "CRLF" ? "\r\n" : "\n";
    public ValidatedProfile(MachineProfile profile, GrindingRequest request)
    {
        ProfilePersistence.CheckSchema(profile);
        this.profile = profile;
        var issues = new List<ValidationIssue>();
        foreach (var (key, rule) in profile.Rules)
        {
            // Inactive axes still require limits and offsets to report/check the full start state.
            var inactive = key.StartsWith("V.") && key.EndsWith("Feed") ||
                request.VerticalDownfeedYPerRoughSweepMm == 0 && (key == "Y.DownSign" || key.StartsWith("Y.") && key.EndsWith("Feed")) ||
                request.CrossTravelZMm == 0 && (key is "Z.InitialCrossSign" or "Z.InitialCrossDirection" || key.StartsWith("Z.") && key.EndsWith("Feed"));
            if (inactive && rule.Value is null) continue;
            var trusted = rule.Status is Provenance.ManufacturerConfirmed or Provenance.ControllerManualConfirmed or
                Provenance.ExistingProgramConfirmed or Provenance.OperatorObserved ||
                rule.Status == Provenance.OperatorEntered && rule.OperatorApproved;
            if (rule.Value is null || !trusted || string.IsNullOrWhiteSpace(rule.Source) ||
                !DateOnly.TryParseExact(rule.DateConfirmed, "yyyy-MM-dd", out _))
            {
                issues.Add(new("Unknown machine data", key, "Requires a value, confirmed provenance (or explicit operator approval), source and confirmation date."));
                continue;
            }
            if (rule.Unit is "text" or "ASCII" or "CRLF or LF") continue;
            try { numbers[key] = Numeric.Parse(rule.Value, key); }
            catch (ValidationException ex) { issues.AddRange(ex.Issues); }
        }
        if (issues.Count != 0) throw new ValidationException(issues);
        foreach (var axis in Enum.GetValues<Axis>())
        {
            Require(this[$"{axis}.Minimum"] < this[$"{axis}.Maximum"], $"{axis}.limits", "Minimum must be less than maximum.");
            Require(this[$"{axis}.ClearanceMinimum"] <= this[$"{axis}.ClearanceMaximum"] &&
                this[$"{axis}.ClearanceMinimum"] >= this[$"{axis}.Minimum"] &&
                this[$"{axis}.ClearanceMaximum"] <= this[$"{axis}.Maximum"], $"{axis}.clearance envelope", "Require an ordered operator-approved machine-coordinate clearance envelope inside the soft limits.");
            if (numbers.ContainsKey($"{axis}.MinimumFeed") || numbers.ContainsKey($"{axis}.MaximumFeed"))
                Require(numbers.ContainsKey($"{axis}.MinimumFeed") && numbers.ContainsKey($"{axis}.MaximumFeed") &&
                    this[$"{axis}.MinimumFeed"] > 0 && this[$"{axis}.MaximumFeed"] >= this[$"{axis}.MinimumFeed"], $"{axis}.feed", "Require positive ordered feed range.");
        }
        foreach (var key in new[] { "X.RightSign", "Y.DownSign", "Z.InitialCrossSign" })
            if (numbers.ContainsKey(key)) Require(this[key] is -1 or 1, key, "Direction sign must be -1 or +1.");
        Require(this["Spindle.MinimumRpm"] > 0 && this["Spindle.MaximumRpm"] >= this["Spindle.MinimumRpm"], "Spindle", "Require positive ordered RPM range for installed wheel.");
        IntegerRange("Output.DecimalPlaces", 0, 12);
        IntegerRange("Output.MaximumBlocks", 10, 100000);
        IntegerRange("Output.MaximumBlockLength", 16, 4096);
        IntegerRange("Output.MaximumFileBytes", 1, 10000000);
        IntegerRange("Output.MaximumNameLength", 1, 100);
        IntegerRange("Output.BlockNumbersRequired", 0, 0);
        Require(Text("Output.Encoding") == "ASCII", "Output.Encoding", "Only explicitly confirmed ASCII is implemented.");
        Require(Text("Output.LineEndings") is "CRLF" or "LF", "Output.LineEndings", "Require CRLF or LF.");
        Require(System.Text.RegularExpressions.Regex.IsMatch(Text("Output.Extension"), @"\A\.[A-Za-z0-9]{1,8}\z"), "Output.Extension", "Require a dot and 1–8 ASCII alphanumeric characters.");
        foreach (var key in new[] { "Setup.ReferenceMethod", "Setup.ClearanceProcedure", "Setup.SpindleProcedure" })
            Require(!string.IsNullOrWhiteSpace(Text(key)), key, "Confirmed setup procedure required.");
        if (request.CrossTravelZMm > 0)
            Require(!string.IsNullOrWhiteSpace(Text("Z.InitialCrossDirection")), "Z.InitialCrossDirection", "Describe the physical first cross-travel direction independently from its numerical sign.");
    }
    private void IntegerRange(string key, int min, int max) => Require(this[key] == decimal.Truncate(this[key]) && this[key] >= min && this[key] <= max, key, $"Implemented range is integer {min} through {max}; other controller requirements block export.");
    private static void Require(bool condition, string field, string message)
    {
        if (!condition) throw new ValidationException("Profile", field, message);
    }
    public void CheckPosition(AxisPosition position)
    {
        foreach (var axis in Enum.GetValues<Axis>())
        {
            Numeric.RequirePrecision(position[axis], DecimalPlaces, $"{axis} coordinate");
            var machineMm = ExactDecimal.Add(position[axis], this[$"{axis}.WorkToMachineOffset"]);
            Require(machineMm >= this[$"{axis}.Minimum"] && machineMm <= this[$"{axis}.Maximum"], $"{axis} trajectory", $"Machine coordinate {Numeric.Format(machineMm)} mm must be within confirmed limits.");
            Require(machineMm >= this[$"{axis}.ClearanceMinimum"] && machineMm <= this[$"{axis}.ClearanceMaximum"], $"{axis} clearance", $"Machine coordinate {Numeric.Format(machineMm)} mm must be inside the reviewed clearance envelope for this setup.");
        }
    }
    public void CheckFeed(Axis axis, decimal feed)
    {
        Numeric.RequirePrecision(feed, DecimalPlaces, $"{axis} feed mm/min");
        Require(feed > 0 && feed >= this[$"{axis}.MinimumFeed"] && feed <= this[$"{axis}.MaximumFeed"], $"{axis} feed", $"{Numeric.Format(feed)} mm/min outside confirmed range.");
    }
}

public static class TextValidation
{
    public static void Comment(string text, string field)
    {
        if (text is null || text.Length > 200 || text.Any(c => c < 32 || c > 126 || c is ';' or '%' or '(' or ')'))
            throw new ValidationException("Input", field, "Use at most 200 printable ASCII characters, excluding semicolons, delimiters and parentheses.");
    }
    public static void ProgramName(string name, int maxLength)
    {
        if (name is null || name.Length > maxLength || !System.Text.RegularExpressions.Regex.IsMatch(name, @"\A[A-Za-z0-9][A-Za-z0-9_-]*\z") ||
            System.Text.RegularExpressions.Regex.IsMatch(name, @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new ValidationException("Input", "ProgramName", "Require an allowed non-device Windows name using ASCII letters, digits, underscore or hyphen within confirmed length.");
    }
}
