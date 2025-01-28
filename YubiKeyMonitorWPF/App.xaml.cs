using System.Configuration;
using System.Data;
using System.Windows;

namespace YubiKeyMonitorWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (!System.OperatingSystem.IsWindowsVersionAtLeast(7))
            {
                MessageBox.Show("Questa applicazione richiede Windows 7 o successivo",
                    "Errore Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            MainWindow = new MainWindow();
            MainWindow.Show();
        }
    }
}
