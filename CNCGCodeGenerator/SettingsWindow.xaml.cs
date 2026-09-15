using System.Windows;
using System.Windows.Controls;
using CNCGCodeGenerator.Core;

namespace CNCGCodeGenerator;

public partial class SettingsWindow : Window
{
    public OperationSettings Operation { get; private set; }
    public string SelectedLanguage { get; private set; }
    private readonly Dictionary<string, TextBox> inputs = [];
    private readonly ComboBox ySign = new(), zSign = new();
    public SettingsWindow(OperationSettings operation, string selectedLanguage)
    {
        InitializeComponent(); Operation = operation.Copy(); SelectedLanguage = selectedLanguage;
        OperationPanel.Children.Add(new TextBlock { Text = "Danobat RTM-2500 / serial 30106A / Fagor 8055M\nFixed: absolute G90, first X direction RIGHT (X-), Z step after each complete X out-and-back cycle. Coolant, magnet and dresser commands are disabled.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        foreach (var (key, label) in OperationSettings.Labels) { var input = new TextBox { Text = Operation.Values[key], Padding = new Thickness(5) }; inputs[key] = input; AddRow(label, input); }
        ySign.ItemsSource = new[] { "Y-", "Y+" }; ySign.SelectedIndex = Danobat30106AConfig.YDownSign == -1 ? 0 : 1;
        zSign.ItemsSource = new[] { "Z-", "Z+" }; zSign.SelectedIndex = Danobat30106AConfig.ZInitialCrossSign == -1 ? 0 : 1;
        AddRow("Machine configuration — Y down direction (unconfirmed)", ySign); AddRow("Machine configuration — initial Z cross direction (unconfirmed)", zSign);
    }
    private void AddRow(string label, Control input) { var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }); Grid.SetColumn(input, 1); grid.Children.Add(input); OperationPanel.Children.Add(grid); }
    private void Apply_Click(object sender, RoutedEventArgs e) { foreach (var (key, input) in inputs) Operation.Values[key] = input.Text; Danobat30106AConfig.YDownSign = ySign.SelectedIndex == 0 ? -1 : 1; Danobat30106AConfig.ZInitialCrossSign = zSign.SelectedIndex == 0 ? -1 : 1; DialogResult = true; }
}
