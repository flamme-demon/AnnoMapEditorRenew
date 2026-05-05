using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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

                var gamePath = this.FindControl<TextBox>("GamePathBox");
                if (gamePath != null)
                {
                    gamePath.Text = Settings.Instance.GamePath ?? "";
                    gamePath.TextChanged += OnGamePathChanged;
                }

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

        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (sender is not ComboBox combo) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string lang)
                Localizer.Current.Language = lang;
        }

        private void OnGamePathChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (sender is TextBox tb)
                Settings.Instance.GamePath = string.IsNullOrWhiteSpace(tb.Text) ? null : tb.Text;
        }

        private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
        {
            try
            {
                IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Sélectionne le dossier d'installation d'Anno 117",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    string path = folders[0].Path.LocalPath;
                    var box = this.FindControl<TextBox>("GamePathBox");
                    if (box != null) box.Text = path;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Browse failed: {ex.Message}");
            }
        }

        private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
    }
}
