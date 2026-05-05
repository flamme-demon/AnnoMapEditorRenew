using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AnnoMapEditor.UI.Avalonia.ViewModels;
using AnnoMapEditor.UI.Avalonia;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    public partial class StartWindow : Window
    {
        private readonly StartWindowViewModel _viewModel;
        private bool _autoStartAttempted;

        public StartWindow()
        {
            _viewModel = new StartWindowViewModel();
            DataContext = _viewModel;
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            WindowStateService.Attach(this, WindowKind.Start);

            var combo = this.FindControl<ComboBox>("LanguageSelector");
            if (combo != null)
            {
                combo.ItemsSource = new[] { "English", "Français" };
                combo.SelectedIndex = Localizer.Current.Language == "fr" ? 1 : 0;
            }

            var versionLabel = this.FindControl<TextBlock>("VersionLabel");
            if (versionLabel != null)
                versionLabel.Text = AppInfo.ShortVersionLabel;

            // Reflect Settings.AutoStart in the bypass toggle. We sync once at init
            // (won't re-fire OnAutoStartToggled because IsChecked == previous value).
            var autoStart = this.FindControl<CheckBox>("AutoStartCheckBox");
            if (autoStart != null)
                autoStart.IsChecked = Settings.Instance.AutoStart;

            // Auto-bypass : si Settings.AutoStart=true (default) ET l'autodetect a
            // trouvé un dossier d'install Anno 117 valide, on enchaîne
            // automatiquement sur MainWindow sans que l'utilisateur ait à cliquer
            // "Continuer". La fenêtre Start clignote brièvement le temps de
            // l'init DataManager. Si l'init échoue ou si AutoStart=false, on
            // reste sur StartWindow.
            Opened += async (_, _) =>
            {
                if (_autoStartAttempted) return;
                _autoStartAttempted = true;

                if (!Settings.Instance.AutoStart) return;

                string? gamePath = Settings.Instance.GamePath;
                if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath)) return;
                if (!Directory.Exists(Path.Combine(gamePath, "maindata"))) return;

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    bool ok = await _viewModel.InitializeAsync();
                    if (ok)
                    {
                        var main = new MainWindow();
                        main.Show();
                        Close();
                    }
                }, DispatcherPriority.Background);
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo)
                Localizer.Current.Language = combo.SelectedIndex == 1 ? "fr" : "en";
        }

        private void OnAutoStartToggled(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.IsChecked is bool value)
                Settings.Instance.AutoStart = value;
        }

        private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
        {
            try
            {
                IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Sélectionne le dossier d'installation du jeu Anno",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    _viewModel.GamePath = folders[0].Path.LocalPath;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Browse failed: {ex.Message}");
            }
        }

        private async void OnContinueClicked(object? sender, RoutedEventArgs e)
        {
            bool ok = await _viewModel.InitializeAsync();
            if (ok)
            {
                var main = new MainWindow();
                main.Show();
                Close();
            }
        }

        private void OnQuitClicked(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Close();
            }
        }
    }
}
