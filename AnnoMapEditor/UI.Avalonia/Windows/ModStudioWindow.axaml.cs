using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    /// <summary>
    /// POC home screen for the Mods Studio rework. Lists local mods + offers
    /// New / Open / Import-vanilla actions. Fully visual at this stage — every
    /// click either opens a placeholder dialog or routes to MainWindow with the
    /// existing flow until the per-action wiring is done.
    /// </summary>
    public partial class ModStudioWindow : Window
    {
        public ModStudioWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private async void OnNewModClick(object? sender, RoutedEventArgs e)
        {
            // POC: show the wizard so the user can validate the flow visually.
            var wiz = new NewModWizardDialog();
            await wiz.ShowDialog(this);
        }

        private void OnOpenModClick(object? sender, RoutedEventArgs e)
        {
            // POC: open MainWindow as today (no project model yet).
            var main = new MainWindow();
            main.Show();
            Close();
        }
    }
}
