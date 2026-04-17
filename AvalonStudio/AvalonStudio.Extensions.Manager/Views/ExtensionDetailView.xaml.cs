using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonStudio.Extensions.Manager.Views
{
    public class ExtensionDetailView : UserControl
    {
        public ExtensionDetailView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
