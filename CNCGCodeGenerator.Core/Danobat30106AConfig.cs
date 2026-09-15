namespace CNCGCodeGenerator.Core;

/// <summary>Fixed configuration for the one supported machine.</summary>
public static class Danobat30106AConfig
{
    public const string Manufacturer = "Danobat";
    public const string Model = "RTM-2500";
    public const string SerialNumber = "30106A";
    public const string Controller = "Fagor 8055M";
    public const int XRightSign = -1;

    // These directions are not confirmed by the available machine evidence.
    // Keep them in one small, visible configuration location.
    public static int YDownSign { get; set; } = -1;
    public static int ZInitialCrossSign { get; set; } = -1;
}

public static class OutputDefaults
{
    public const int DecimalPlaces = 3;
    public const string Extension = ".PIM";
    public const int MaximumBlocks = 100000;
    public const int MaximumBlockLength = 4096;
    public const int MaximumFileBytes = 10000000;
}
