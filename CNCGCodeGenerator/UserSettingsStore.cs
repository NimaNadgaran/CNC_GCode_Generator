using System.Text.Json;
using System.IO;
using CNCGCodeGenerator.Core;

namespace CNCGCodeGenerator;

public sealed record PersistedUserSettings(OperationSettings Operation, string XTravel, string YDownfeed, string ZCoverage, string Language, int YDownSign, int ZInitialCrossSign);

/// <summary>Stores reusable UI settings without persisting machine-start safety data.</summary>
public static class UserSettingsStore
{
    private const int CurrentSchemaVersion = 1;
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CNCGCodeGenerator", "settings.json");

    public static PersistedUserSettings Load(string path)
    {
        if (!File.Exists(path)) return Defaults();
        var file = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(path)) ?? throw new InvalidDataException("The settings file is empty.");
        if (file.SchemaVersion != CurrentSchemaVersion || file.OperationValues is null || file.XTravel is null || file.YDownfeed is null || file.ZCoverage is null || file.Language is null || file.Language is not ("en" or "fa") || file.YDownSign is not (-1 or 1) || file.ZInitialCrossSign is not (-1 or 1)) throw new InvalidDataException("The settings file has an unsupported or invalid schema.");
        if (file.OperationValues.Keys.Except(OperationSettings.Labels.Keys).Any() || OperationSettings.Labels.Keys.Any(key => !file.OperationValues.ContainsKey(key)) || file.OperationValues.Values.Any(value => value is null)) throw new InvalidDataException("The settings file does not contain the expected operation fields.");
        var operation = new OperationSettings { Values = new(file.OperationValues) };
        operation.Values["StartX"] = "";
        operation.Values["StartY"] = "";
        operation.Values["StartZ"] = "";
        return new(operation, file.XTravel, file.YDownfeed, file.ZCoverage, file.Language, file.YDownSign, file.ZInitialCrossSign);
    }

    public static void Save(string path, OperationSettings operation, string xTravel, string yDownfeed, string zCoverage, string language, int yDownSign, int zInitialCrossSign)
    {
        if (language is not ("en" or "fa") || yDownSign is not (-1 or 1) || zInitialCrossSign is not (-1 or 1)) throw new ArgumentException("Invalid user-settings value.");
        var values = new Dictionary<string, string>(operation.Values);
        values["StartX"] = "";
        values["StartY"] = "";
        values["StartZ"] = "";
        var file = new SettingsFile { SchemaVersion = CurrentSchemaVersion, OperationValues = values, XTravel = xTravel, YDownfeed = yDownfeed, ZCoverage = zCoverage, Language = language, YDownSign = yDownSign, ZInitialCrossSign = zInitialCrossSign };
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static PersistedUserSettings Defaults() => new(new OperationSettings(), "", "", "", "en", -1, -1);

    public sealed class SettingsFile
    {
        public int SchemaVersion { get; set; }
        public Dictionary<string, string>? OperationValues { get; set; }
        public string? XTravel { get; set; }
        public string? YDownfeed { get; set; }
        public string? ZCoverage { get; set; }
        public string? Language { get; set; }
        public int YDownSign { get; set; }
        public int ZInitialCrossSign { get; set; }
    }
}
