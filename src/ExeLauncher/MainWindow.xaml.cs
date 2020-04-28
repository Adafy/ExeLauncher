using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using NLog.Config;
using Weikio.PluginFramework.Catalogs;
using Weikio.PluginFramework.Catalogs.NuGet.PackageManagement;

namespace ExeLauncher
{
    public partial class MainWindow : IUpdateStatus
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        public async Task UpdateStatus(string status, bool? hasCrashed = false, bool? isReady = false, bool? appHasClosed = false)
        {
            await Application.Current.Dispatcher.BeginInvoke(new Action((() =>
            {
                StatusTest.Text = status;

                if (hasCrashed.GetValueOrDefault())
                {
                    CloseButton.Visibility = Visibility.Visible;
                    Ring.Visibility = Visibility.Collapsed;
                    Visibility = Visibility.Visible;

                    if (!string.IsNullOrWhiteSpace(LogPath))
                    {
                        StatusTest.Text += $"{Environment.NewLine}See log file for more details: {LogPath}";
                    }
                            

                    return;
                }

                if (isReady.GetValueOrDefault())
                {
                    Visibility = Visibility.Collapsed;

                    return;
                }

                if (appHasClosed.GetValueOrDefault())
                {
                    Application.Current.Shutdown();
                }
            })));
        }

        public void HandleStartupCrash(string status)
        {
            StatusTest.Text = status;
            CloseButton.Visibility = Visibility.Visible;
            Ring.Visibility = Visibility.Collapsed;
            Visibility = Visibility.Visible;

            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                StatusTest.Text += $"{Environment.NewLine}See log file for more details: {LogPath}";
            }
        }
        public string LogPath { get; set; }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
