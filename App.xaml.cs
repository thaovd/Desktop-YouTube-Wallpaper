using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DesktopVideoWallpaper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Thiết lập chế độ tắt ứng dụng thủ công ngay từ đầu
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string errPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            try
            {
                File.AppendAllText(errPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unhandled Exception: {e.Exception}\n");
            }
            catch { }

            // Đánh dấu là đã xử lý để tránh crash ứng dụng
            e.Handled = true;
        }
    }
}
