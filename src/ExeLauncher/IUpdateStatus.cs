using System.Threading.Tasks;

namespace ExeLauncher
{
    public interface IUpdateStatus
    {
        Task UpdateStatus(string status, bool? hasCrashed = false, bool? isReady = false, bool? appHasClosed = false, bool? manualClose = false, bool? isFirstLaunch = false);
        string LogPath { get; set; }
    }
}
