using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonStudio.Extensions.Manager.Views
{
    public class ExtensionsManagerView : UserControl
    {
        public ExtensionsManagerView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
