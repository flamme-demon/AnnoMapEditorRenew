using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    public partial class SaveModDialog : Window
    {
        private TextBox? _nameBox;

        public SaveModDialog() : this(string.Empty) { }

        public SaveModDialog(string defaultName)
        {
            InitializeComponent();
            _nameBox = this.FindControl<TextBox>("NameBox");
            if (_nameBox != null)
            {
                _nameBox.Text = defaultName;
                _nameBox.SelectAll();
                _nameBox.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter) Save();
                    if (e.Key == Key.Escape) Close(null);
                };
                Opened += (_, _) => _nameBox.Focus();
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
        private void OnSave(object? sender, RoutedEventArgs e) => Save();

        private void Save()
        {
            string? name = _nameBox?.Text?.Trim();
            Close(string.IsNullOrEmpty(name) ? null : name);
        }
    }
}
