using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using AnnoMapEditor.UI.Avalonia.ViewModels;
using AnnoMapEditor.UI.Avalonia;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    public partial class StartWindow : Window
    {
        private readonly StartWindowViewModel _viewModel;

        public StartWindow()
        {
            _viewModel = new StartWindowViewModel();
            DataContext = _viewModel;
            InitializeComponent();
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
