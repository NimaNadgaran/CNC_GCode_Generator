using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace CNCGCodeGenerator
{
    public partial class MainWindow : Window
    {
        // =========================
        // SETTINGS
        // =========================

        private double feedRate = 500;
        private double spindleSpeed = 12000;
        private double safeZ = 5;

        // Extra distance on each side
        private double xExtra = 5;
        private double yExtra = 5;

        // Language
        // "en" = English
        // "fa" = Farsi
        private string language = "en";


        // =========================
        // CONSTRUCTOR
        // =========================

        public MainWindow()
        {
            InitializeComponent();

            UpdateMainWindowLanguage();
        }


        // =========================
        // SETTINGS BUTTON
        // =========================

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow(
                feedRate,
                spindleSpeed,
                safeZ,
                xExtra,
                yExtra,
                language);

            bool? result = settingsWindow.ShowDialog();

            if (result == true)
            {
                feedRate = settingsWindow.FeedRate;
                spindleSpeed = settingsWindow.SpindleSpeed;
                safeZ = settingsWindow.SafeZ;

                xExtra = settingsWindow.XExtra;
                yExtra = settingsWindow.YExtra;

                language = settingsWindow.SelectedLanguage;

                UpdateMainWindowLanguage();

                StatusText.Text =
                    language == "fa"
                    ? "✓ تنظیمات با موفقیت به‌روزرسانی شد."
                    : "✓ Settings updated.";
            }
        }


        // =========================
        // GENERATE BUTTON
        // =========================

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            // -------------------------
            // X
            // -------------------------

            if (!double.TryParse(
                    XInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double x))
            {
                ShowError(
                    language == "fa"
                    ? "مقدار X نامعتبر است."
                    : "Invalid X value.");

                return;
            }


            // -------------------------
            // Y
            // -------------------------

            if (!double.TryParse(
                    YInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double y))
            {
                ShowError(
                    language == "fa"
                    ? "مقدار Y نامعتبر است."
                    : "Invalid Y value.");

                return;
            }


            // -------------------------
            // Z
            // -------------------------

            if (!double.TryParse(
                    ZInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double z))
            {
                ShowError(
                    language == "fa"
                    ? "مقدار Z نامعتبر است."
                    : "Invalid Z value.");

                return;
            }


            // =========================
            // VALIDATION
            // =========================

            if (x <= 0)
            {
                ShowError(
                    language == "fa"
                    ? "X باید بزرگ‌تر از صفر باشد."
                    : "X must be greater than 0.");

                return;
            }


            if (y <= 0)
            {
                ShowError(
                    language == "fa"
                    ? "Y باید بزرگ‌تر از صفر باشد."
                    : "Y must be greater than 0.");

                return;
            }


            if (feedRate <= 0)
            {
                ShowError(
                    language == "fa"
                    ? "Feed Rate باید بزرگ‌تر از صفر باشد."
                    : "Feed rate must be greater than 0.");

                return;
            }


            if (spindleSpeed <= 0)
            {
                ShowError(
                    language == "fa"
                    ? "Spindle Speed باید بزرگ‌تر از صفر باشد."
                    : "Spindle speed must be greater than 0.");

                return;
            }


            if (safeZ <= z)
            {
                ShowError(
                    language == "fa"
                    ? "Safe Z باید از عمق برش بیشتر باشد."
                    : "Safe Z must be higher than cutting Z.");

                return;
            }


            // =========================
            // GENERATE G-CODE
            // =========================

            string gcode = GenerateGCode(
                x,
                y,
                z);

            GCodeOutput.Text = gcode;

            StatusText.Text =
                language == "fa"
                ? "✓ کد G-Code با موفقیت تولید شد."
                : "✓ G-Code generated successfully.";
        }


        // =========================
        // G-CODE GENERATOR
        // =========================

        private string GenerateGCode(
            double x,
            double y,
            double z)
        {
            StringBuilder gcode = new StringBuilder();


            // =========================
            // EXTRA DISTANCE
            // =========================

            // Extra distance on BOTH sides

            double startX = -xExtra;
            double endX = x + xExtra;

            double startY = -yExtra;
            double endY = y + yExtra;


            // =========================
            // PROGRAM START
            // =========================

            gcode.AppendLine("%");

            gcode.AppendLine("O1001");


            // Millimeters
            gcode.AppendLine("G21");


            // Absolute positioning
            gcode.AppendLine("G90");


            // =========================
            // SAFE Z
            // =========================

            gcode.AppendLine(
                $"G00 Z{safeZ.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)}");


            // =========================
            // STARTING POSITION
            // =========================

            gcode.AppendLine(
                $"G00 X{startX.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)} " +
                $"Y{startY.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)}");


            // =========================
            // SPINDLE ON
            // =========================

            gcode.AppendLine(
                $"S{spindleSpeed.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)} M03");


            // =========================
            // CUTTING DEPTH
            // =========================

            gcode.AppendLine(
                $"G01 Z{z.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)} " +
                $"F{feedRate.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)}");


            // =========================
            // MOVE ACROSS X
            // =========================

            gcode.AppendLine(
                $"G01 X{endX.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)}");


            // =========================
            // MOVE ACROSS Y
            // =========================

            gcode.AppendLine(
                $"G01 Y{endY.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)}");


            // =========================
            // RETURN ACROSS X
            // =========================

            gcode.AppendLine(
                $"G01 X{startX.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)}");


            // =========================
            // RETURN TO SAFE Z
            // =========================

            gcode.AppendLine(
                $"G00 Z{safeZ.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)}");


            // =========================
            // SPINDLE OFF
            // =========================

            gcode.AppendLine("M05");


            // =========================
            // PROGRAM END
            // =========================

            gcode.AppendLine("M30");

            gcode.AppendLine("%");


            return gcode.ToString();
        }


        // =========================
        // SAVE BUTTON
        // =========================

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GCodeOutput.Text))
            {
                ShowError(
                    language == "fa"
                    ? "ابتدا G-Code را تولید کنید."
                    : "Generate G-Code first.");

                return;
            }


            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Title =
                    language == "fa"
                    ? "ذخیره G-Code دستگاه CNC"
                    : "Save CNC G-Code",

                Filter =
                    "NC Files (*.nc)|*.nc|" +
                    "G-Code Files (*.gcode)|*.gcode|" +
                    "TAP Files (*.tap)|*.tap|" +
                    "All Files (*.*)|*.*",

                DefaultExt = ".nc",

                FileName = "CNC_Program.nc"
            };


            bool? result = saveDialog.ShowDialog();


            if (result == true)
            {
                try
                {
                    File.WriteAllText(
                        saveDialog.FileName,
                        GCodeOutput.Text,
                        Encoding.ASCII);


                    StatusText.Text =
                        language == "fa"
                        ? $"✓ فایل ذخیره شد: {saveDialog.FileName}"
                        : $"✓ File saved: {saveDialog.FileName}";
                }
                catch (Exception ex)
                {
                    ShowError(
                        language == "fa"
                        ? $"امکان ذخیره فایل وجود ندارد:\n{ex.Message}"
                        : $"Could not save file:\n{ex.Message}");
                }
            }
        }


        // =========================
        // CLEAR BUTTON
        // =========================

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            XInput.Clear();

            YInput.Clear();

            ZInput.Clear();

            GCodeOutput.Clear();


            StatusText.Text =
                language == "fa"
                ? "آماده"
                : "Ready";
        }


        // =========================
        // ERROR MESSAGE
        // =========================

        private void ShowError(string message)
        {
            MessageBox.Show(
                message,
                language == "fa"
                    ? "خطا"
                    : "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);


            StatusText.Text =
                language == "fa"
                ? "✗ خطا"
                : "✗ Error";
        }


        // =========================
        // LANGUAGE UPDATE
        // =========================

        private void UpdateMainWindowLanguage()
        {
            if (language == "fa")
            {
                // -------------------------
                // WINDOW
                // -------------------------

                Title = "تولیدکننده G-Code دستگاه CNC";


                // -------------------------
                // LABELS
                // -------------------------

                XLabel.Text = "X (میلی‌متر)";

                YLabel.Text = "Y (میلی‌متر)";

                ZLabel.Text = "عمق Z (میلی‌متر)";


                // -------------------------
                // BUTTONS
                // -------------------------

                GenerateButton.Content = "تولید G-Code";

                SaveButton.Content = "ذخیره G-Code";

                ClearButton.Content = "پاک کردن";

                SettingsButton.Content = "⚙ تنظیمات";


                // -------------------------
                // GROUP BOXES
                // -------------------------

                DimensionsGroup.Header = "ابعاد";

                GCodeGroup.Header = "G-Code تولید شده";


                // -------------------------
                // FLOW DIRECTION
                // -------------------------

                FlowDirection =
                    FlowDirection.RightToLeft;
            }
            else
            {
                // -------------------------
                // WINDOW
                // -------------------------

                Title = "CNC G-Code Generator";


                // -------------------------
                // LABELS
                // -------------------------

                XLabel.Text = "X (mm)";

                YLabel.Text = "Y (mm)";

                ZLabel.Text = "Z Depth (mm)";


                // -------------------------
                // BUTTONS
                // -------------------------

                GenerateButton.Content =
                    "Generate G-Code";

                SaveButton.Content =
                    "Save G-Code";

                ClearButton.Content =
                    "Clear";

                SettingsButton.Content =
                    "⚙ Settings";


                // -------------------------
                // GROUP BOXES
                // -------------------------

                DimensionsGroup.Header =
                    "Dimensions";

                GCodeGroup.Header =
                    "Generated G-Code";


                // -------------------------
                // FLOW DIRECTION
                // -------------------------

                FlowDirection =
                    FlowDirection.LeftToRight;
            }
        }
    }
}