using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CNCGCodeGenerator
{
    public partial class SettingsWindow : Window
    {
        public double FeedRate { get; private set; }
        public double SpindleSpeed { get; private set; }
        public double SafeZ { get; private set; }

        public double XExtra { get; private set; }
        public double YExtra { get; private set; }

        public string SelectedLanguage { get; private set; }


        public SettingsWindow(
            double feedRate,
            double spindleSpeed,
            double safeZ,
            double xExtra,
            double yExtra,
            string language)
        {
            InitializeComponent();

            FeedInput.Text =
                feedRate.ToString(CultureInfo.InvariantCulture);

            SpindleInput.Text =
                spindleSpeed.ToString(CultureInfo.InvariantCulture);

            SafeZInput.Text =
                safeZ.ToString(CultureInfo.InvariantCulture);

            XExtraInput.Text =
                xExtra.ToString(CultureInfo.InvariantCulture);

            YExtraInput.Text =
                yExtra.ToString(CultureInfo.InvariantCulture);


            SelectedLanguage = language;

            if (language == "fa")
            {
                LanguageComboBox.SelectedIndex = 1;
                SetPersian();
            }
            else
            {
                LanguageComboBox.SelectedIndex = 0;
                SetEnglish();
            }
        }


        private void LanguageComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedIndex == 1)
            {
                SetPersian();
            }
            else if (LanguageComboBox.SelectedIndex == 0)
            {
                SetEnglish();
            }
        }


        private void SetEnglish()
        {
            SelectedLanguage = "en";

            Title = "CNC Settings";

            TitleText.Text = "CNC Settings";

            FeedLabel.Text = "Feed Rate (mm/min):";
            SpindleLabel.Text = "Spindle RPM:";
            SafeZLabel.Text = "Safe Z (mm):";

            XExtraLabel.Text =
                "X Extra Each Side (mm):";

            YExtraLabel.Text =
                "Y Extra Each Side (mm):";

            LanguageLabel.Text = "Language:";

            CancelButton.Content = "Cancel";
            SaveButton.Content = "Save Settings";

            FlowDirection = FlowDirection.LeftToRight;
        }


        private void SetPersian()
        {
            SelectedLanguage = "fa";

            Title = "تنظیمات CNC";

            TitleText.Text = "تنظیمات CNC";

            FeedLabel.Text = "سرعت پیشروی (mm/min):";
            SpindleLabel.Text = "دور اسپیندل (RPM):";
            SafeZLabel.Text = "ارتفاع امن Z (mm):";

            XExtraLabel.Text =
                "مقدار اضافه X از هر طرف (mm):";

            YExtraLabel.Text =
                "مقدار اضافه Y از هر طرف (mm):";

            LanguageLabel.Text = "زبان:";

            CancelButton.Content = "لغو";
            SaveButton.Content = "ذخیره تنظیمات";

            FlowDirection = FlowDirection.RightToLeft;
        }


        private void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!double.TryParse(
                    FeedInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double feedRate))
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "سرعت پیشروی نامعتبر است."
                        : "Invalid feed rate.");

                return;
            }


            if (!double.TryParse(
                    SpindleInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double spindleSpeed))
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "دور اسپیندل نامعتبر است."
                        : "Invalid spindle speed.");

                return;
            }


            if (!double.TryParse(
                    SafeZInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double safeZ))
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "ارتفاع امن Z نامعتبر است."
                        : "Invalid Safe Z.");

                return;
            }


            if (!double.TryParse(
                    XExtraInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double xExtra))
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "مقدار اضافه X نامعتبر است."
                        : "Invalid X extra distance.");

                return;
            }


            if (!double.TryParse(
                    YExtraInput.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double yExtra))
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "مقدار اضافه Y نامعتبر است."
                        : "Invalid Y extra distance.");

                return;
            }


            if (feedRate <= 0)
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "سرعت پیشروی باید بیشتر از صفر باشد."
                        : "Feed rate must be greater than 0.");

                return;
            }


            if (spindleSpeed <= 0)
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "دور اسپیندل باید بیشتر از صفر باشد."
                        : "Spindle speed must be greater than 0.");

                return;
            }


            if (xExtra < 0)
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "مقدار اضافه X نمی‌تواند منفی باشد."
                        : "X extra distance cannot be negative.");

                return;
            }


            if (yExtra < 0)
            {
                ShowError(
                    SelectedLanguage == "fa"
                        ? "مقدار اضافه Y نمی‌تواند منفی باشد."
                        : "Y extra distance cannot be negative.");

                return;
            }


            FeedRate = feedRate;
            SpindleSpeed = spindleSpeed;
            SafeZ = safeZ;

            XExtra = xExtra;
            YExtra = yExtra;


            DialogResult = true;
            Close();
        }


        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }


        private void ShowError(string message)
        {
            MessageBox.Show(
                message,
                SelectedLanguage == "fa"
                    ? "خطا"
                    : "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}