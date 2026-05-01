using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AnnoMapEditor
{
    public partial class BootstrapWindow : Window
    {
        public BootstrapWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
