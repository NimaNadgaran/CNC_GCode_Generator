using CNCGCodeGenerator.Core;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CNCGCodeGenerator;

public partial class SettingsWindow : Window
{
    public OperationSettings Operation { get; private set; }
    public MachineProfile Profile { get; private set; }
    public string SelectedLanguage { get; private set; }
    private readonly Dictionary<string, TextBox> inputs = [];
    private readonly List<RuleRow> rows = [];
    private readonly ComboBox mode = new(), timing = new(), direction = new(), language = new();

    public SettingsWindow(OperationSettings operation, MachineProfile profile, string selectedLanguage)
    {
        InitializeComponent();
        Operation = operation.Copy(); Profile = ProfilePersistence.Deserialize(ProfilePersistence.Serialize(profile)); SelectedLanguage = selectedLanguage;
        StatusColumn.ItemsSource = Enum.GetValues<Provenance>();
        OperationPanel.Children.Add(new TextBlock
        {
            Text = "Enter planned axis travel on the main screen, including operator-calculated overtravel/allowances. These are not workpiece dimensions. Start at the first X endpoint and Z coverage edge using the approved setup procedure. No automatic approach or retract is generated. Settings and validation reports use English; main labels also support Persian.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        foreach (var (key, label) in OperationSettings.Labels)
        {
            var input = new TextBox { Text = Operation.Values[key], Padding = new Thickness(5) };
            inputs[key] = input; AddRow(label, input);
        }
        mode.ItemsSource = new[] { "Absolute G90", "Incremental G91 (restores G90)" }; mode.SelectedIndex = (int)Operation.CoordinateMode;
        timing.ItemsSource = new[] { "Z step after every X stroke", "Z step after complete X out-and-back cycle" }; timing.SelectedIndex = (int)Operation.CrossStepTiming;
        direction.ItemsSource = new[] { "First X stroke right (sign from profile)", "First X stroke left (sign from profile)" }; direction.SelectedIndex = (int)Operation.FirstXDirection;
        language.ItemsSource = new[] { "English", "فارسی" }; language.SelectedIndex = selectedLanguage == "fa" ? 1 : 0;
        AddRow("Coordinate mode", mode); AddRow("Cross-step timing", timing); AddRow("First X physical direction", direction); AddRow("Main-screen language", language);
        OperationPanel.Children.Add(new TextBlock { Text = "Each roughing sweep begins with Y downfeed. Successive sweeps reverse Z coverage. Finishing sweeps have no Y downfeed; extra spark-out round trips stay at final Z. Coolant and dresser commands are disabled pending machine confirmation.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        DisplayProfile();
    }
    private void AddRow(string label, Control input)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(input, 1); grid.Children.Add(input); OperationPanel.Children.Add(grid);
    }
    private void DisplayProfile()
    {
        ProfileVersionInput.Text = Profile.ProfileVersion;
        rows.Clear(); rows.AddRange(Profile.Rules.Select(pair => new RuleRow(pair.Key, pair.Value)));
        RulesGrid.ItemsSource = null; RulesGrid.ItemsSource = rows;
    }
    private MachineProfile ReadProfile()
    {
        RulesGrid.CommitEdit(DataGridEditingUnit.Cell, true); RulesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var version = ProfileVersionInput.Text;
        var result = Profile with { ProfileVersion = version, Rules = rows.ToDictionary(r => r.Name, r => r.ToRule(version)) };
        return ProfilePersistence.Deserialize(ProfilePersistence.Serialize(result));
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Profile = ReadProfile();
            foreach (var (key, input) in inputs) Operation.Values[key] = input.Text;
            Operation.CoordinateMode = (CoordinateMode)mode.SelectedIndex;
            Operation.CrossStepTiming = (CrossStepTiming)timing.SelectedIndex;
            Operation.FirstXDirection = (TableDirection)direction.SelectedIndex;
            SelectedLanguage = language.SelectedIndex == 1 ? "fa" : "en";
            DialogResult = true;
        }
        catch (ValidationException ex) { ShowError(ex); }
    }
    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Machine profile (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { Profile = ProfilePersistence.Deserialize(File.ReadAllText(dialog.FileName)); DisplayProfile(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ValidationException) { ShowError(ex); }
    }
    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var draft = ReadProfile();
            var path = MainWindow.ProfilePath;
            if (MessageBox.Show(this, "Save this profile for application restart? Unknown rules will still block export." + Environment.NewLine + path +
                (File.Exists(path) ? "\nThe existing profile will be replaced with a backup retained." : ""), "Save machine profile", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.Write(path, Encoding.UTF8.GetBytes(ProfilePersistence.Serialize(draft)), overwriteConfirmed: true);
            Profile = draft;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ValidationException) { ShowError(ex); }
    }
    private void ShowError(Exception ex) => MessageBox.Show(this, ex.Message, "Settings error", MessageBoxButton.OK, MessageBoxImage.Error);

    public sealed class RuleRow(string name, MachineRule rule)
    {
        public string Name { get; } = name;
        public string? Value { get; set; } = rule.Value;
        public string Unit { get; } = rule.Unit;
        public Provenance Status { get; set; } = rule.Status;
        public bool OperatorApproved { get; set; } = rule.OperatorApproved;
        public string Source { get; set; } = rule.Source;
        public string? DateConfirmed { get; set; } = rule.DateConfirmed;
        public string Notes { get; set; } = rule.Notes;
        public MachineRule ToRule(string version) => new()
        {
            Value = string.IsNullOrEmpty(Value) ? null : Value,
            Unit = Unit,
            Status = Status,
            OperatorApproved = OperatorApproved,
            Source = Source,
            DateConfirmed = DateConfirmed,
            Notes = Notes,
            ProfileVersion = version
        };
    }
}
