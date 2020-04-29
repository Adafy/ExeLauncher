using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NLog;

namespace ExeLauncher
{
    public class LauncherService
    {
        private string _packageName;
        private string _applicationName;
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private bool _useWin32ToDetectOpenWindow = true;
        private readonly Func<string, bool, bool, bool?, Task> _statusUpdater;
        private readonly LauncherStorageService _storageService;
        private readonly AutoUpdater _autoUpdater;
        private readonly PackageManager _packageManager;

        public LauncherService(Func<string, bool, bool, bool?, Task> statusUpdater = null)
        {
            _statusUpdater = statusUpdater;
            _applicationName = Configuration.ApplicationName;
            _packageName = Configuration.Package;
            _storageService = new LauncherStorageService(_applicationName);
            _autoUpdater = new AutoUpdater(_applicationName, _packageName);
            _packageManager = new PackageManager(_applicationName, _packageName);

            _logger.Info("Application root folder is {AppRootFolder}", Configuration.ApplicationRootFolder(_applicationName));
        }

        public async Task Launch()
        {
            _applicationName = Configuration.ApplicationName;
            _packageName = Configuration.Package;

            _logger.Info("Application root folder is {AppRootFolder}", Configuration.ApplicationRootFolder(_applicationName));

            try
            {
                if (Configuration.Clean)
                {
                    try
                    {
                        _storageService.Clean();
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e,
                            "Failed to clean {AppRootFolder}. Quite likely some files are locked. Close the application or restart the computer and try again.",
                            Configuration.ApplicationRootFolder(_applicationName));

                        throw;
                    }
                }

                UpdateCurrentExecutable();

                var initializationResult = await Initialize();

                if (initializationResult.IsFirstLaunch)
                {
                    await UpdateStatus("Preparing for the first launch...");
                }

                var latestVersion = await _packageManager.Scan(initializationResult.CurrentVersion);

                if (latestVersion.HasNewerVersion)
                {
                    if (!initializationResult.IsFirstLaunch)
                    {
                        await UpdateStatus("Downloading latest version...");
                    }

                    await _packageManager.DownloadVersion(latestVersion.LatestVersion);
                    UpdateCurrentExecutable();
                }

                var currentExecutable = _storageService.GetCurrentExecutable();

                await UpdateStatus("Launching...");

                _logger.Info("Launching {ExeFile}", currentExecutable);

                await Launch(currentExecutable);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to launch application");

                await UpdateStatus("Failed to launch application", hasCrashed: true);
            }
        }

        private async Task Launch(string executable)
        {
            var startInfo = new ProcessStartInfo(executable);

            try
            {
                if (!string.IsNullOrWhiteSpace(Configuration.WorkingDir))
                {
                    _logger.Info("Setting application's working directory to {WorkingDir} based on configuration", Configuration.WorkingDir);
                    startInfo.WorkingDirectory = Configuration.WorkingDir;
                }

                else
                {
                    _logger.Info("No working directory is provided, automatically parse it from the executable {Executable}", Configuration.WorkingDir);

                    var workingDirectory = Path.GetDirectoryName(executable);

                    if (!string.IsNullOrWhiteSpace(workingDirectory))
                    {

                        startInfo.WorkingDirectory = workingDirectory;
                    }
                    else
                    {
                        throw new Exception("Failed to automatically set working directory for process based on the executable.");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to set working directory for process. Default to application's root path {ApplicationRootPath}", _storageService.GetApplicationRootFolderPath());
                startInfo.WorkingDirectory = _storageService.GetApplicationRootFolderPath();
            }
            
            try
            {
                if (!Directory.Exists(startInfo.WorkingDirectory))
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                _logger.Info("Working directory {WorkingDirectory} doesn't exists or it is invalid. Default to version root {VersionRoot}", startInfo.WorkingDirectory, _storageService.GetCurrentRoot());

                startInfo.WorkingDirectory = _storageService.GetCurrentRoot();
            }

            if (!string.IsNullOrWhiteSpace(Configuration.Arguments))
            {
                _logger.Info("Configuration contains arguments. Adding the following into app's launch arguments: {Arguments}.", Configuration.Arguments);

                startInfo.Arguments = Configuration.Arguments;
            }                    
            
            var process = Process.Start(startInfo);

            if (process != null)
            {
                process.EnableRaisingEvents = true;

                var counter = 0;

                while (true)
                {
                    if (_useWin32ToDetectOpenWindow)
                    {
                        var processInfo = Process.GetProcessById(process.Id);

                        if (IsWindowVisible(processInfo.MainWindowHandle))
                        {
                            break;
                        }

                        try
                        {
                            if (!string.IsNullOrWhiteSpace(processInfo.MainWindowTitle))
                            {
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e, "Failed to get main application's title");
                        }

                        counter += 1;

                        if (counter > 100)
                        {
                            _logger.Info("Hit the main window launch timer break. Hide the launcher anyway.");

                            break;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(0.1));
                    }
                }

                _logger.Info("The application has started. Hiding launcher");

                await UpdateStatus("Ready", isReady: true);
                _autoUpdater.Start();

                process.Exited += async (sender, args) =>
                {
                    _logger.Info("The application has exited. Closing launcher");
                    _autoUpdater.Dispose();

                    await UpdateStatus("Exiting", appHasClosed: true);
                };
            }
            else
            {
                throw new Exception($"Failed to launch process with executable {executable} and with working directory {startInfo.WorkingDirectory}");
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern Boolean ShowWindow(IntPtr hWnd, Int32 nCmdShow);

        private async Task UpdateStatus(string status, bool? hasCrashed = false, bool? isReady = false, bool? appHasClosed = false)
        {
            if (_statusUpdater == null)
            {
                return;
            }

            await _statusUpdater(status, hasCrashed.GetValueOrDefault(), isReady.GetValueOrDefault(), appHasClosed);
        }

        private void UpdateCurrentExecutable()
        {
            try
            {
                var pendingVersion = _storageService.GetPendingVersion();
                var pendingExe = _storageService.GetPendingExe();
                var pendingRoot = _storageService.GetPendingVersionRootPath();

                if (!string.IsNullOrWhiteSpace(Configuration.LaunchCommand) && !string.IsNullOrWhiteSpace(_storageService.GetCurrentExecutable()) && !string.IsNullOrWhiteSpace(_storageService.GetCurrentRoot()))
                {
                    var launchCommand = _storageService.GetCurrentRoot() + "/" + Configuration.LaunchCommand;
                    _storageService.UpdateCurrentVersionExe(launchCommand);
                }
                        
                if (string.IsNullOrWhiteSpace(pendingExe) || string.IsNullOrWhiteSpace(pendingVersion))
                {
                    return;
                }

                _storageService.UpdateCurrentVersion(pendingVersion);
                _storageService.UpdateCurrentVersionExe(pendingExe);
                _storageService.UpdateCurrentVersionRoot(pendingRoot);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to update the current executable.");

                throw;
            }
        }

        private async Task<(bool IsFirstLaunch, string CurrentVersion, string AppRootFolder, string AppVersionFolder)> Initialize()
        {
            _logger.Info("Initializing launcher");
            var appRootFolder = _storageService.GetApplicationRootFolderPath();

            _logger.Info("Application root folder: {AppRootFolder}", appRootFolder);

            var appVersionsFolder = _storageService.GetApplicationVersionsFolder();
            _logger.Info("Application versions folder: {AppVersionsFolder}", appVersionsFolder);

            var isFirstLaunch = !Directory.Exists(appVersionsFolder);
            Directory.CreateDirectory(appVersionsFolder);

            _logger.Info("Is first launch: {IsFirstLaunch}", isFirstLaunch);

            var currentVersion = await _storageService.GetCurrentVersion();

            return (isFirstLaunch, currentVersion, appRootFolder, appVersionsFolder);
        }
    }
}
