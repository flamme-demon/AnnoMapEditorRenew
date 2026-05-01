using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AnnoMapEditor.UI.Avalonia.Windows;

namespace AnnoMapEditor
{
    public partial class App : Application
    {
        public static readonly string TitleShort = "Community Map Editor";
        public static readonly string SubTitle = "for Anno 1800 and 117";
        public static readonly string Title = $"{TitleShort} {SubTitle}";

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new StartWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
