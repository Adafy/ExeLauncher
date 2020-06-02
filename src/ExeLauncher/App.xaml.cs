using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

// #if EXE
//             var hostFile = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
//             ConfigurationManager.OpenExeConfiguration(hostFile+".config");
// #endif

            Mouse.OverrideCursor = Cursors.Wait;
            
            try
            {
                var packageName = Configuration.Package;
            }
            catch (Exception)
            {
                var crashWindow = new MainWindow();
                crashWindow.Show();

                var configFileLocation = typeof(MainWindow).Assembly.Location;

                crashWindow.HandleStartupCrash(
                    $"Application's package information is missing. Please provide the package as a launch argument for ExeLauncher. {Environment.NewLine}{Environment.NewLine}package=Id of the app's nuget package{Environment.NewLine}{Environment.NewLine}" +
                    $"Example: {Environment.NewLine}{Environment.NewLine}exelauncher \"MyCompany.MyApplication\" " + Environment.NewLine + Environment.NewLine +
                    $"Running folder of ExeLauncher is {configFileLocation}");

                return;
            }

            InitializeLogging();
            var fullConfiguration = Configuration.GetFullConfiguration();
            
            _logger.Debug("Default configuration file path: {ConfigFilePath}. Application configuration file path: {ApplicationConfigFilePath}", Configuration.DefaultConfigurationFilePath, Configuration.ApplicationConfigurationFilePath);
            _logger.Debug("Configuration:");

            foreach (var config in fullConfiguration)
            {
                _logger.Debug("{ConfigKey}: {ConfigValue}", config.Key, config.Value);
            }
            
            var showUi = Configuration.ShowLauncher;

            IUpdateStatus statusUpdater = null;

            if (showUi == ShowLauncherEnum.Always || showUi == ShowLauncherEnum.FirstLaunch)
            {
                _logger.Info("Gui is enabled. Displaying launcher window.");
                var window = new MainWindow();

                if (showUi == ShowLauncherEnum.Always)
                {
                    window.Show();
                }

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

            if (!string.IsNullOrWhiteSpace(Configuration.ExportPath))
            {
                statusUpdater?.UpdateStatus($"Exporting configuration to {Configuration.ExportPath}");

                try
                {
                    var exporter = new ExporterService();

                    exporter.Export();

                    statusUpdater?.UpdateStatus($"Configuration exported to {Configuration.ExportPath}", manualClose: true);
                }
                catch (Exception)
                {
                    statusUpdater?.UpdateStatus($"Error when exporting configuration to {Configuration.ExportPath}");
                }

                return;
            }

            try
            {
                if (statusUpdater != null)
                {
                    _launcher = new LauncherService((statusText, hasCrashed, isReady, appHasClosed, manualClose, isFirstLaunch) =>
                        statusUpdater.UpdateStatus(statusText, hasCrashed, isReady, appHasClosed, manualClose, isFirstLaunch));
                }
                else
                {
                    _launcher = new LauncherService(async (statusText, hasCrashed, isReady, appHasClosed, manualClose, isFirstLaunch) =>
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
            var appRootFolder = Configuration.GetApplicationRootFolder();
            var config = new LoggingConfiguration();
            _logFileName = Path.Combine(appRootFolder, "logs", "launcher.log");

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = _logFileName };
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            LogManager.Configuration = config;

            _logger = LogManager.GetCurrentClassLogger();
        }
    }
}
