using System.Threading.Tasks;

namespace ExeLauncher
{
    public interface IUpdateStatus
    {
        Task UpdateStatus(string status, bool? hasCrashed = false, bool? isReady = false, bool? appHasClosed = false);
        string LogPath { get; set; }
    }
}
