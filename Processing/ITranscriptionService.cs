using System.Threading.Tasks;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Service for transcribing audio files using AssemblyAI.
    /// </summary>
    public interface ITranscriptionService
    {
        Task<string> TranscribeAsync(string audioPath);
    }
}
