using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace ExeLauncher
{
    public class LauncherStorageService
    {
        private readonly string _applicationName;
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public LauncherStorageService(string applicationName)
        {
            _applicationName = applicationName;
        }

        public string GetPendingVersion()
        {
            var pendingVersionFile = GetPendingInstallVersionFilePath();

            if (!File.Exists(pendingVersionFile))
            {
                return string.Empty;
            }

            var pendingVersion = File.ReadAllText(pendingVersionFile, Encoding.UTF8);

            return pendingVersion;
        }
        
        public string GetPendingVersionRootPath()
        {
            var pendingInstallVersionRootPath = GetPendingInstallVersionRootPath();

            if (!File.Exists(pendingInstallVersionRootPath))
            {
                return string.Empty;
            }

            var pendingVersion = File.ReadAllText(pendingInstallVersionRootPath, Encoding.UTF8);

            return pendingVersion;
        }

        public string GetPendingExe()
        {
            var pendingExeFile = GetPendingInstallExeFilePath();

            if (!File.Exists(pendingExeFile))
            {
                return string.Empty;
            }

            var pendingVersion = File.ReadAllText(pendingExeFile, Encoding.UTF8);

            return pendingVersion;
        }

        public async Task<string> GetCurrentVersion()
        {
            var appRootFolder = GetApplicationRootFolderPath();
            var currentVersionFile = GetCurrentVersionFilePath();
            var currentVersion = "";

            if (File.Exists(currentVersionFile))
            {
                _logger.Info("Reading current version from {CurrentVersionFile}", currentVersionFile);
                currentVersion = await File.ReadAllTextAsync(currentVersionFile, Encoding.UTF8);

                _logger.Info("Current version: {CurrentVersion}", currentVersion);
            }

            return currentVersion;
        }

        public string GetCurrentExecutable()
        {
            var exePathFile = GetExecutableFilePath();

            if (!File.Exists(exePathFile))
            {
                _logger.Error("Current executable file {ExeFile} is missing. Unknown state.", exePathFile);

                throw new Exception($"Current executable file {exePathFile} is missing. Unknown state.");
            }

            return File.ReadAllText(exePathFile, Encoding.UTF8);
        }
        
        public string GetCurrentRoot()
        {
            var rootFilePath = GetCurrentVersionRootFilePath();

            if (!File.Exists(rootFilePath))
            {
                _logger.Error("Current root file {RootFile} is missing. Unknown state.", rootFilePath);

                throw new Exception($"Current root file {rootFilePath} is missing. Unknown state.");
            }

            return File.ReadAllText(rootFilePath, Encoding.UTF8);
        }

        public void UpdateCurrentVersion(string newVersion)
        {
            try
            {
                var currentVersionFile = GetCurrentVersionFilePath();
                _logger.Info("Updating {CurrentVersionFile} to contain the current version {NewVersion}", currentVersionFile, newVersion);

                File.WriteAllText(currentVersionFile, newVersion, Encoding.UTF8);
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    "Failed to update the current version info. Current version file: {CurrentVersionFile}. Pending version: {NewVersion}. The pending version file removed to clear any invalid states.",
                    GetCurrentVersionFilePath(), newVersion);

                throw new Exception("Failed to update current version");
            }
            finally
            {
                var pendingInstallVersionFilePath = GetPendingInstallVersionFilePath();

                if (File.Exists(pendingInstallVersionFilePath))
                {
                    try
                    {
                        File.Delete(pendingInstallVersionFilePath);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }

        public void UpdateCurrentVersionExe(string pendingExe)
        {
            try
            {
                var exePathFile = GetExecutableFilePath();
                _logger.Info("Updating {ExecutablePathFile} to contain the path to the current version {Executable}", exePathFile, pendingExe);

                File.WriteAllText(exePathFile, pendingExe, Encoding.UTF8);
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    "Failed to update the current exe. Current exe file: {CurrentExeFile}. Pending exe: {NewVersion}. The pending version file removed to clear any invalid states.",
                    GetExecutableFilePath(), pendingExe);

                throw new Exception("Failed to update current exe");
            }
            finally
            {
                var pendingInstallExeFilePath = GetPendingInstallExeFilePath();

                if (File.Exists(pendingInstallExeFilePath))
                {
                    try
                    {
                        File.Delete(pendingInstallExeFilePath);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }
        
        public void UpdateCurrentVersionRoot(string rootPath)
        {
            try
            {
                var rootFilePath = GetCurrentVersionRootFilePath();
                _logger.Info("Updating {RootPathFile} to contain the path to the current version {Root}", rootFilePath, rootPath);

                File.WriteAllText(rootFilePath, rootPath, Encoding.UTF8);
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    "Failed to update the current root path. Current root path file: {CurrentExeFile}. Pending root path: {NewVersion}. The pending version file removed to clear any invalid states.",
                    GetCurrentVersionRootFilePath(), rootPath);

                throw new Exception("Failed to update current version root path");
            }
            finally
            {
                var pendingInstallVersionRootPath = GetPendingInstallVersionRootPath();

                if (File.Exists(pendingInstallVersionRootPath))
                {
                    try
                    {
                        File.Delete(pendingInstallVersionRootPath);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }

        public async Task UpdatePendingVersion(string version)
        {
            var pendingVersionFile = GetPendingInstallVersionFilePath();
            _logger.Info("Writing version {DownloadedVersion} to the pending install version file {PendingVersionFile}", version, pendingVersionFile);

            await File.WriteAllTextAsync(pendingVersionFile, version, Encoding.UTF8);
        }
        
        public async Task UpdatePendingVersionRoot(string rootPath)
        {
            var pendingInstallVersionRootPath = GetPendingInstallVersionRootPath();
            _logger.Info("Writing root path {RootPath} to the pending install version root path folder {PendingRootPath}", rootPath, pendingInstallVersionRootPath);

            await File.WriteAllTextAsync(pendingInstallVersionRootPath, rootPath, Encoding.UTF8);
        }

        public async Task UpdatePendingExe(string launchCommand)
        {
            var pendingExeFile = GetPendingInstallExeFilePath();

            _logger.Info("Writing path to the downloaded executable {DownloadedExecutable} to the pending install exe file {PendingFile}", launchCommand,
                pendingExeFile);

            await File.WriteAllTextAsync(pendingExeFile, launchCommand, Encoding.UTF8);
        }

        public void Clean()
        {
            try
            {
                Directory.Delete(GetApplicationRootFolderPath(), true);
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    "Failed to clean {AppRootFolder}. Quite likely some files are locked. Close the application or restart the computer and try again.",
                    Configuration.ApplicationRootFolder(_applicationName));

                throw;
            }
        }

        private string GetPendingInstallVersionFilePath()
        {
            var appRootFolder = GetApplicationRootFolderPath();

            return Path.Combine(appRootFolder, "pendingversion.txt");
        }

        private string GetPendingInstallVersionRootPath()
        {
            var appRootFolder = GetApplicationRootFolderPath();

            return Path.Combine(appRootFolder, "pendingroot.txt");
        }
        
        private string GetPendingInstallExeFilePath()
        {
            var appRootFolder = GetApplicationRootFolderPath();

            return Path.Combine(appRootFolder, "pendingexe.txt");
        }

        public string GetApplicationRootFolderPath()
        {
            var result = Configuration.ApplicationRootFolder(_applicationName);
            Directory.CreateDirectory(result);

            return result;
        }

        public string GetApplicationVersionsFolder()
        {
            var appRootFolder = GetApplicationRootFolderPath();

            return Path.Combine(appRootFolder, "versions");
        }

        private string GetCurrentVersionFilePath()
        {
            var appRootFolder = GetApplicationRootFolderPath();

            return Path.Combine(appRootFolder, "current.txt");
        }

        private string GetCurrentVersionRootFilePath()
        {
            var appRootFolder = GetApplicationRootFolderPath();

            return Path.Combine(appRootFolder, "currentroot.txt");
        }
        
        private string GetExecutableFilePath()
        {
            var appRootFolder = GetApplicationRootFolderPath();

            return Path.Combine(appRootFolder, "run.bat");
        }
    }
}
