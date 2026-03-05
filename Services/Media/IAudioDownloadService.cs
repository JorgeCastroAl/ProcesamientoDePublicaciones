using System.Threading.Tasks;

namespace FluxAnswer.Services.Media
{
    /// <summary>
    /// Service for downloading audio from TikTok videos.
    /// </summary>
    public interface IAudioDownloadService
    {
        Task<string> DownloadAudioAsync(string videoUrl, string outputPath);
    }
}
