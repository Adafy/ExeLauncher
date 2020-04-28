using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NuGet.Protocol.Core.Types;
using Weikio.PluginFramework.Catalogs.NuGet.PackageManagement;

namespace ExeLauncher
{
    public class LauncherService
    {
        private NuGetDownloader _nuGetDownloader;
        private string _packageName;
        private string _appName;
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private bool _useWin32ToDetectOpenWindow = true;
        private string _nugetConfigPath;
        private readonly Func<string, bool, bool, bool?, Task> _statusUpdater;

        public LauncherService(Func<string, bool, bool, bool?, Task> statusUpdater = null)
        {
            _statusUpdater = statusUpdater;
            _appName = Configuration.ApplicationName;
            _packageName = Configuration.Package;

            InitializeNuget();

            _logger.Info("Application root folder is {AppRootFolder}", Configuration.ApplicationRootFolder(_appName));
        }

        public async Task Launch()
        {
            
            _appName = Configuration.ApplicationName;
            _packageName = Configuration.Package;

            InitializeNuget();

            _logger.Info("Application root folder is {AppRootFolder}", Configuration.ApplicationRootFolder(_appName));
            
            try
            {
                var initializationResult = await Initialize();

                if (initializationResult.IsFirstLaunch)
                {
                    await UpdateStatus("Preparing for the first launch...");
                }

                var latestVersion = await Scan(initializationResult.CurrentVersion);

                if (latestVersion.HasNewerVersion)
                {
                    if (!initializationResult.IsFirstLaunch)
                    {
                        await UpdateStatus("Downloading latest version...");
                    }

                    await DownloadVersion(initializationResult.AppVersionFolder, latestVersion.LatestVersion);
                    UpdateCurrentExecutable();
                }

                var currentExecutable = GetCurrentExecutable();

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

        private void InitializeNuget()
        {
            _nuGetDownloader = new NuGetDownloader();
            _nugetConfigPath = Configuration.NugetConfigPath;

            if (string.IsNullOrWhiteSpace(_nugetConfigPath))
            {
                _logger.Info(
                    "No nuget.config path is set, use machines default nuget configuration. To use custom Nuget.Config, please set configuration 'nuget'");
            }
            else
            {
                _logger.Info("Nuget.config path is set to {NugetConfigPath}, using that for the Nuget sources. No machine wide Nuget configurations are used",
                    _nugetConfigPath);

                if (!File.Exists(_nugetConfigPath))
                {
                    _logger.Error("Nuget.config path was set to {NugetConfigPath}, but the file is missing", _nugetConfigPath);

                    throw new Exception($"Nuget configuration file {_nugetConfigPath} is missing");
                }
            }
        }

        private async Task Launch(string currentExecutable)
        {
            var startInfo = new ProcessStartInfo(currentExecutable);
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

                process.Exited += async (sender, args) =>
                {
                    _logger.Info("The application has exited. Closing launcher");

                    await UpdateStatus("Exiting", appHasClosed: true);
                };
            }
            else
            {
                throw new Exception("Failed to launch process with executable: " + currentExecutable);
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

        private async Task DownloadVersion(string appVersionsFolder, string version)
        {
            _logger.Info("Downloading the package using package name {PackageName} and version {Version}", _packageName, version);

            try
            {
                var package = await FindPackage();

                var downloadedFiles = await _nuGetDownloader.DownloadAsync(package.Package, package.Repository, appVersionsFolder);
                _logger.Info("Files downloaded to {AppVersionsFolder}", appVersionsFolder);

                _logger.Debug("Downloaded the following files:");

                foreach (var downloadedFile in downloadedFiles)
                {
                    _logger.Debug("{FileName}", downloadedFile);
                }

                if (downloadedFiles.Any() != true)
                {
                    _logger.Error("The package didn't contain any files. Unknown state.");
                }

                string launchCommand;

                if (string.IsNullOrWhiteSpace(Configuration.LaunchCommand))
                {
                    try
                    {
                        launchCommand = downloadedFiles.SingleOrDefault(x => x.EndsWith(".exe"));
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to find executable from downloaded package. Does it contain many exe-files?");

                        throw;
                    }
                }
                else
                {
                    try
                    {
                        var newVersionPackage = downloadedFiles.Single(x => x.EndsWith(".nupkg"));
                        var newVersionRoot = Path.GetDirectoryName(newVersionPackage);
                    
                        launchCommand = newVersionRoot + "/"+ Configuration.LaunchCommand;
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to set launch command based on parameter {LaunchCommand}", Configuration.LaunchCommand);

                        throw;
                    }
                }

                if (launchCommand == null)
                {
                    _logger.Error("The package didn't contain an executable.");

                    throw new Exception("The package didn't contain an executable.");
                }

                _logger.Info("Executable to the downloaded version is {ExeFile}", launchCommand);

                var pendingExeFile = GetPendingInstallExePath(_appName);

                _logger.Info("Writing path to the downloaded executable {DownloadedExecutable} to the pending install exe file {PendingFile}", launchCommand,
                    pendingExeFile);

                await File.WriteAllTextAsync(pendingExeFile, launchCommand, Encoding.UTF8);

                var pendingVersionFile = GetPendingInstallVersion(_appName);
                _logger.Info("Writing version {DownloadedVersion} to the pending install version file {PendingVersionFile}", version, pendingVersionFile);

                await File.WriteAllTextAsync(pendingVersionFile, version, Encoding.UTF8);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to download package using package name {PackageName} and version {Version}", _packageName, version);

                throw;
            }
        }

        private async Task<(bool HasNewerVersion, string LatestVersion)> Scan(string currentVersion)
        {
            _logger.Info("Finding the application package using {PackageName}", _packageName);

            var packageAndRepo = await FindPackage();

            var versions = (await packageAndRepo.Package.GetVersionsAsync()).ToList();

            if (versions?.Any() != true)
            {
                _logger.Error("Couldn't find any versions for package {PackageName}", _packageName);

                throw new Exception("Couldn't find any versions for the app with the identifier " + _packageName);
            }

            var latestVersionInfo = versions.OrderByDescending(x => x.Version).First();
            _logger.Info("Latest available version for {PackageName} is {LatestVersion}", _packageName, latestVersionInfo.Version);

            var latestVersion = latestVersionInfo.Version.ToString();

            if (currentVersion != latestVersion)
            {
                _logger.Info("New package available for {PackageName}. Current version is {CurrentVersion} and latest version is {LatestVersion}", _packageName,
                    currentVersion, latestVersion);

                return (true, latestVersion);
            }

            return (false, latestVersion);
        }

        private async Task<(SourceRepository Repository, IPackageSearchMetadata Package)> FindPackage()
        {
            (SourceRepository Repository, IPackageSearchMetadata Package) result = default;
            var foundPackages = new List<(SourceRepository Repository, IPackageSearchMetadata Package)>();

            await foreach (var foundPackage in _nuGetDownloader.SearchPackagesAsync(_packageName,
                includePrerelease: true,
                nugetConfigFilePath: _nugetConfigPath))
            {
                foundPackages.Add(foundPackage);

                if (string.Equals(foundPackage.Package.Identity.Id, _packageName, StringComparison.InvariantCultureIgnoreCase))
                {
                    result = foundPackage;

                    break;
                }
            }

            if (result != default)
            {
                return result;
            }

            _logger.Error("Couldn't find package using the exact id {PackageName}", _packageName);

            if (foundPackages.Any() == true)
            {
                _logger.Error("Found the following non-matching packages:");

                foreach (var nonMatchingPackage in foundPackages)
                {
                    _logger.Error($"{nonMatchingPackage.Package.Identity}");
                }
            }

            throw new Exception("Couldn't find the package with the identifier " + _packageName);
        }

        private void UpdateCurrentExecutable()
        {
            var pendingVersionFile = GetPendingInstallVersion(_appName);
            var pendingExeFile = GetPendingInstallExePath(_appName);

            try
            {
                if (!File.Exists(pendingExeFile) || !File.Exists(pendingVersionFile))
                {
                    return;
                }

                var pendingVersion = File.ReadAllText(pendingVersionFile, Encoding.UTF8);
                var pendingExe = File.ReadAllText(pendingExeFile, Encoding.UTF8);

                var currentVersionFile = GetCurrentVersionFile(_appName);
                _logger.Info("Updating {CurrentVersionFile} to contain the current version {NewVersion}", currentVersionFile, pendingVersion);
                File.WriteAllText(currentVersionFile, pendingVersion, Encoding.UTF8);

                var exePathFile = GetExecutableFile(_appName);
                _logger.Info("Updating {ExecutablePathFile} to contain the path to the current version {Exeutable}", exePathFile, pendingExe);
                File.WriteAllText(exePathFile, pendingExe, Encoding.UTF8);
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    "Failed to update the current executable. Pending version file: {PendingVersionFile}, pending exe file: {PendingExeFile}. The files are removed to clear any invalid states.",
                    pendingVersionFile, pendingExeFile);

                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(pendingVersionFile))
                    {
                        File.Delete(pendingVersionFile);
                    }

                    if (File.Exists(pendingExeFile))
                    {
                        File.Delete(pendingExeFile);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to delete pending file: {PendingVersionFile}, {PendingExeFile}", pendingVersionFile, pendingExeFile);
                }
            }
        }

        private string GetCurrentExecutable()
        {
            var exePathFile = GetExecutableFile(_appName);

            if (!File.Exists(exePathFile))
            {
                _logger.Error("Current executable file {ExeFile} is missing. Unknown state.", exePathFile);
            }

            return File.ReadAllText(exePathFile, Encoding.UTF8);
        }

        private async Task<(bool IsFirstLaunch, string CurrentVersion, string AppRootFolder, string AppVersionFolder)> Initialize()
        {
            _logger.Info("Initializing launcher");
            var appRootFolder = GetApplicationRootFolder(_appName);

            _logger.Info("Application root folder: {AppRootFolder}", appRootFolder);

            var appVersionsFolder = Path.Combine(appRootFolder, "versions");
            _logger.Info("Application versions folder: {AppVersionsFolder}", appVersionsFolder);

            var isFirstLaunch = !Directory.Exists(appVersionsFolder);
            Directory.CreateDirectory(appVersionsFolder);

            _logger.Info("Is first launch: {IsFirstLaunch}", isFirstLaunch);

            var currentVersionFile = GetCurrentVersionFile(appRootFolder);
            var currentVersion = "";

            if (File.Exists(currentVersionFile))
            {
                _logger.Info("Reading current version from {CurrentVersionFile}", currentVersionFile);
                currentVersion = await File.ReadAllTextAsync(currentVersionFile, Encoding.UTF8);

                _logger.Info("Current version: {CurrentVersion}", currentVersion);
            }

            return (isFirstLaunch, currentVersion, appRootFolder, appVersionsFolder);
        }

        private static string GetCurrentVersionFile(string applicationName)
        {
            var appRootFolder = GetApplicationRootFolder(applicationName);

            return Path.Combine(appRootFolder, "current.txt");
        }

        private static string GetPendingInstallVersion(string applicationName)
        {
            var appRootFolder = GetApplicationRootFolder(applicationName);

            return Path.Combine(appRootFolder, "pendingversion.txt");
        }

        private static string GetPendingInstallExePath(string applicationName)
        {
            var appRootFolder = GetApplicationRootFolder(applicationName);

            return Path.Combine(appRootFolder, "pendingexe.txt");
        }

        private static string GetExecutableFile(string applicationName)
        {
            var appRootFolder = GetApplicationRootFolder(applicationName);

            return Path.Combine(appRootFolder, "run.bat");
        }

        private static string GetApplicationRootFolder(string applicationName)
        {
            var result = Configuration.ApplicationRootFolder(applicationName);
            Directory.CreateDirectory(result);

            return result;
        }
    }
}
