using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using AnnoMapEditor.UI.Avalonia.Windows;

namespace AnnoMapEditor
{
    public partial class App : Application
    {
        public static readonly string TitleShort = "Anno 117 Map Editor";
        public static readonly string SubTitle = "for Anno 117 — Pax Romana";
        public static readonly string Title = $"{TitleShort} — {SubTitle}";

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            // Apply the persisted theme as soon as resources are loaded so the
            // first frame already uses the user's choice (no Light → Dark flicker).
            ApplyTheme(UserSettings.Default.ThemeVariant);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // POC: when DEBUG_MODSTUDIO env var is set, boot the new mod
                // studio home screen instead of the legacy StartWindow. Lets us
                // validate the new UX visually without touching the existing
                // flow yet. Flip back by unsetting the env var.
                bool poc = System.Environment.GetEnvironmentVariable("ANNO_MOD_STUDIO_POC") == "1";
                desktop.MainWindow = poc
                    ? (Window)new ModStudioWindow()
                    : new StartWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>Toggle between "Light" (parchment) and "Dark" (navy) themes
        /// and persist the choice. Called from the FAB in the bottom bar.</summary>
        public static void ToggleTheme()
        {
            string next = UserSettings.Default.ThemeVariant == "Dark" ? "Light" : "Dark";
            UserSettings.Default.ThemeVariant = next;
            UserSettings.Default.Save();
            ApplyTheme(next);
        }

        public static string CurrentThemeVariant => UserSettings.Default.ThemeVariant;

        private static void ApplyTheme(string variant)
        {
            if (Current is null) return;
            Current.RequestedThemeVariant = variant == "Dark"
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }
}
