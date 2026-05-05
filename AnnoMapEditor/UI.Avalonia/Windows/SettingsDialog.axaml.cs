using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AnnoMapEditor.UI.Avalonia;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    public partial class SettingsDialog : Window
    {
        private bool _suppressEvents;

        public SettingsDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            // Sync UI from current settings without retriggering handlers (otherwise
            // setting IsChecked here would fire OnXxxToggled and bounce-write the
            // same value into Settings).
            _suppressEvents = true;
            try
            {
                var autoStart = this.FindControl<CheckBox>("AutoStartCheckBox");
                if (autoStart != null) autoStart.IsChecked = Settings.Instance.AutoStart;

                var expertMode = this.FindControl<CheckBox>("ExpertModeCheckBox");
                if (expertMode != null) expertMode.IsChecked = Settings.Instance.EnableExpertMode;

                var gamePath = this.FindControl<TextBox>("GamePathBox");
                if (gamePath != null) gamePath.Text = Settings.Instance.GamePath ?? "";

                SyncLanguage();
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void SyncLanguage()
        {
            var combo = this.FindControl<ComboBox>("LanguageCombo");
            if (combo is null) return;
            string current = Localizer.Current.Language;
            combo.SelectedIndex = current == "fr" ? 1 : 0;
        }


        private void OnAutoStartToggled(object? sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            if (sender is CheckBox cb && cb.IsChecked is bool value)
                Settings.Instance.AutoStart = value;
        }

        private void OnExpertModeToggled(object? sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            if (sender is CheckBox cb && cb.IsChecked is bool value)
                Settings.Instance.EnableExpertMode = value;
        }

        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (sender is not ComboBox combo) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string lang)
                Localizer.Current.Language = lang;
        }

        private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
    }
}
