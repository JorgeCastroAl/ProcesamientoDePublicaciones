using System.Threading.Tasks;

namespace FluxAnswer.Services.AI
{
    /// <summary>
    /// Service for transcribing audio files using AssemblyAI.
    /// </summary>
    public interface ITranscriptionService
    {
        Task<string> TranscribeAsync(string audioPath);
    }
}
