using System.Threading.Tasks;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Service for downloading audio from TikTok videos.
    /// </summary>
    public interface IAudioDownloadService
    {
        Task<string> DownloadAudioAsync(string videoUrl, string outputPath);
    }
}
