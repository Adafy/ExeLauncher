using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace ExeLauncher
{
    public class AutoUpdater : IDisposable
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly LauncherStorageService _storageService;
        private Timer _autoUpdateTimer;
        private readonly PackageManager _packageManager;

        public AutoUpdater(LauncherStorageService launcherStorageService,  PackageManager packageManager)
        {
            _storageService = launcherStorageService;
            _packageManager = packageManager;
        }

        public void Start()
        {
            if (Configuration.AutoUpdateInterval <= 0)
            {
                _logger.Info("Auto update interval is set to {IntervalMinutes} minutes. Updates won't be automatically downloaded when the application is running.",
                    Configuration.AutoUpdateInterval);
            }
            else
            {
                _logger.Info(
                    "Auto update interval is set to {IntervalMinutes} minutes. Updates will be automatically downloaded when the application is running. New updates are installed when the application is launched the next time.",
                    Configuration.AutoUpdateInterval);

                var periodTimeSpan = TimeSpan.FromMinutes(Configuration.AutoUpdateInterval);

                _autoUpdateTimer = new Timer(async (e) =>
                {
                    await KeepUpdated();
                }, null, periodTimeSpan, periodTimeSpan);
            }
        }

        private async Task KeepUpdated()
        {
            try
            {
                var currentVersion = await _storageService.GetCurrentVersion();

                var latestVersion = await _packageManager.Scan(currentVersion);

                if (latestVersion.HasNewerVersion)
                {
                    await _packageManager.DownloadVersion(latestVersion.LatestVersion);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to keep application automatically updated");
            }
        }

        public void Dispose()
        {
            if (_autoUpdateTimer == null)
            {
                return;
            }

            try
            {
                _autoUpdateTimer.DisposeAsync();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
