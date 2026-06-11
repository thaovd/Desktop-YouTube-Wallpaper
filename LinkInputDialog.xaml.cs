using System.Windows;
using System.Windows.Input;

namespace DesktopVideoWallpaper
{
    /// <summary>
    /// Interaction logic for LinkInputDialog.xaml
    /// </summary>
    public partial class LinkInputDialog : Window
    {
        public string ResultUrl { get; private set; } = string.Empty;

        public LinkInputDialog(string currentUrl)
        {
            InitializeComponent();
            TxtVideoUrl.Text = currentUrl;
            TxtVideoUrl.Focus();
            TxtVideoUrl.SelectAll();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultUrl = TxtVideoUrl.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
