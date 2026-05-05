using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    /// <summary>POC wizard for creating a brand-new mod project. Visual only —
    /// validates flow + look. Wiring (writing files, generating .a7tinfo
    /// templates, scaffolding assets.xml) lands in a later phase.</summary>
    public partial class NewModWizardDialog : Window
    {
        public NewModWizardDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
        private void OnCreateClick(object? sender, RoutedEventArgs e) => Close();
    }
}
