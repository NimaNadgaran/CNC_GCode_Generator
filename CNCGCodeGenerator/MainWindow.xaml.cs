using CNCGCodeGenerator.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CNCGCodeGenerator;

public partial class MainWindow : Window
{
    private readonly PreviewSession session = new();
    private OperationSettings operation = new();
    private MachineProfile profile = MachineProfile.Unknown();
    private string language = "en";
    private string? profileLoadError;
    internal static string ProfilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CNCGCodeGenerator", "machine-profile.json");

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            if (File.Exists(ProfilePath)) profile = ProfilePersistence.Deserialize(File.ReadAllText(ProfilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ValidationException)
        {
            profileLoadError = ex.Message;
        }
        XInput.TextChanged += InputChanged;
        YInput.TextChanged += InputChanged;
        ZInput.TextChanged += InputChanged;
        UpdateLanguage();
        InvalidatePreview();
    }

    private void InputChanged(object sender, TextChangedEventArgs e) => InvalidatePreview();
    private void InvalidatePreview()
    {
        session.Invalidate();
        GCodeOutput.Clear();
        SaveButton.IsEnabled = false;
        StatusText.Text = profileLoadError is null
            ? "Operator review required. Machine interlocks and physical setup are not simulated."
            : "Blocking profile-load error: " + profileLoadError;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        InvalidatePreview();
        var window = new SettingsWindow(operation, profile, language) { Owner = this };
        if (window.ShowDialog() != true) return;
        operation = window.Operation;
        profile = window.Profile;
        language = window.SelectedLanguage;
        profileLoadError = null;
        UpdateLanguage();
        InvalidatePreview();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        InvalidatePreview();
        try
        {
            if (profileLoadError is not null) throw new ValidationException("Profile", "Load", profileLoadError);
            var request = operation.CreateRequest(XInput.Text, YInput.Text, ZInput.Text);
            var result = session.Generate(request, profile);
            GCodeOutput.Text = result.Report + Environment.NewLine + "SHA-256: " + result.Sha256 +
                Environment.NewLine + "--- G-CODE ---" + Environment.NewLine + result.GCode;
            SaveButton.IsEnabled = true;
            StatusText.Text = "Validation passed for configured limits. Review the full report and code before export.";
        }
        catch (ValidationException ex)
        {
            GCodeOutput.Text = "EXPORT BLOCKED" + Environment.NewLine + ex.Message;
            StatusText.Text = "Blocking error / unconfirmed machine data. Export unavailable.";
        }
        catch (Exception ex)
        {
            InvalidatePreview();
            System.Diagnostics.Trace.TraceError(ex.ToString());
            GCodeOutput.Text = "Unexpected validation failure. Export blocked. " + ex.Message;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (session.Current is not { } snapshot) return;
        var dialog = new SaveFileDialog
        {
            Title = "Export reviewed Fagor program",
            FileName = snapshot.FileName,
            Filter = "Confirmed program format|*" + Path.GetExtension(snapshot.FileName),
            DefaultExt = Path.GetExtension(snapshot.FileName),
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true) return;
        var overwrite = File.Exists(dialog.FileName);
        var question = (overwrite ? "Replace the existing file (a backup will be retained)?" : "Export the exact validated program?") +
            Environment.NewLine + Path.GetFullPath(dialog.FileName) + Environment.NewLine +
            "Machine interlocks are not simulated. Operator review and commissioning are required before machine use.";
        if (MessageBox.Show(this, question, "Confirm export", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            session.Export(dialog.FileName, overwrite);
            StatusText.Text = "Exported " + snapshot.Sha256 + " to " + dialog.FileName;
        }
        catch (Exception ex)
        {
            InvalidatePreview();
            System.Diagnostics.Trace.TraceError(ex.ToString());
            MessageBox.Show(this, "Export failed: " + ex.Message, "Export blocked", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        XInput.Clear(); YInput.Clear(); ZInput.Clear(); InvalidatePreview();
    }

    private void UpdateLanguage()
    {
        var persian = language == "fa";
        Title = "Danobat RTM-2500 — Fagor 8055M";
        XLabel.Text = persian ? "حرکت میز X (میلی‌متر)" : "X table stroke travel (mm)";
        YLabel.Text = persian ? "باردهی عمودی Y در هر پاس خشن (میلی‌متر)" : "Y downfeed per roughing sweep (mm)";
        ZLabel.Text = persian ? "پوشش حرکت عرضی Z (میلی‌متر)" : "Z cross-travel coverage (mm)";
        DimensionsGroup.Header = persian ? "حرکت برنامه‌ریزی‌شده" : "Planned axis travel";
        GCodeGroup.Header = persian ? "اعتبارسنجی و پیش‌نمایش" : "Validation report and G-code";
        GenerateButton.Content = persian ? "اعتبارسنجی و تولید" : "Validate and generate";
        SaveButton.Content = persian ? "ذخیره پس از بازبینی" : "Export reviewed G-code";
        ClearButton.Content = persian ? "پاک کردن" : "Clear";
        SettingsButton.Content = persian ? "⚙ تنظیمات" : "⚙ Settings";
        FlowDirection = persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        GCodeOutput.FlowDirection = FlowDirection.LeftToRight;
    }
}
