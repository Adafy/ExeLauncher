using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NuGet.Protocol.Core.Types;
using Weikio.PluginFramework.Catalogs.NuGet.PackageManagement;

namespace ExeLauncher
{
    public class PackageManager
    {
        private readonly string _packageName;
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private NuGetDownloader _nuGetDownloader;
        private string _nugetConfigPath;
        private readonly LauncherStorageService _storageService;

        public PackageManager(string applicationName, string packageName)
        {
            _packageName = packageName;
            _storageService = new LauncherStorageService(applicationName);
            
            InitializeNuget();
        }

        public async Task DownloadVersion(string version)
        {
            _logger.Info("Downloading the package using package name {PackageName} and version {Version}", _packageName, version);

            try
            {
                var package = await FindPackage();

                var appVersionsFolder = _storageService.GetApplicationVersionsFolder();
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

                        launchCommand = newVersionRoot + "/" + Configuration.LaunchCommand;
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

                await _storageService.UpdatePendingVersion(version);
                await _storageService.UpdatePendingExe(launchCommand);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to download package using package name {PackageName} and version {Version}", _packageName, version);

                throw;
            }
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

        public async Task<(bool HasNewerVersion, string LatestVersion)> Scan(string currentVersion)
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
    }
}
