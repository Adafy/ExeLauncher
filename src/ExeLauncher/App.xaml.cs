using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using NLog.Config;

namespace ExeLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private LauncherService _launcher;
        private Logger _logger;
        private string _logFileName;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var packageName = Configuration.Package;
            }
            catch (Exception)
            {
                var crashWindow = new MainWindow();
                crashWindow.Show();

                crashWindow.HandleStartupCrash(
                    $"Application's package information is missing. Please provide the package as a launch argument for ExeLauncher. {Environment.NewLine}{Environment.NewLine}package=Id of the app's nuget package{Environment.NewLine}{Environment.NewLine}" +
                    $"Example: {Environment.NewLine}{Environment.NewLine}exelauncher \"MyCompany.MyApplication\" ");

                return;
            }
            
            InitializeLogging();

            var showUi = Configuration.ShowLauncher;

            IUpdateStatus statusUpdater = null;

            if (showUi)
            {
                _logger.Info("Gui is enabled. Displaying launcher window.");
                var window = new MainWindow();
                window.Show();

                statusUpdater = window;
            }
            else
            {
                _logger.Info("Gui is disabled. Only show the actual application and not the launcher.");
            }

            if (statusUpdater != null)
            {
                statusUpdater.LogPath = _logFileName;
            }

            try
            {
                if (statusUpdater != null)
                {
                    _launcher = new LauncherService((statusText, hasCrashed, isReady, appHasClosed) =>
                        statusUpdater.UpdateStatus(statusText, hasCrashed, isReady, appHasClosed));
                }
                else
                {
                    _launcher = new LauncherService(async (statusText, hasCrashed, isReady, appHasClosed) =>
                    {
                        if (appHasClosed.GetValueOrDefault() == false)
                        {
                            return;
                        }

                        await Current.Dispatcher.BeginInvoke(new Action((() =>
                        {
                            Current.Shutdown();
                        })));
                    });
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to initialize launcher");
                statusUpdater?.UpdateStatus("Failed to initialize launcher.", true);
            }

            Task.Run(() => _launcher.Launch());
        }

        private void InitializeLogging()
        {
            var appRootFolder = Configuration.ApplicationRootFolder(Configuration.ApplicationName);
            var config = new LoggingConfiguration();
            _logFileName = Path.Combine(appRootFolder, "logs", "launcher.log");

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = _logFileName };
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            LogManager.Configuration = config;

            _logger = LogManager.GetCurrentClassLogger();
        }
    }
}
