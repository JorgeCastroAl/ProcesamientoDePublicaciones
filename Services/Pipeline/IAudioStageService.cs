using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Services.Pipeline
{
    public interface IAudioStageService
    {
        Task<(string? AudioPath, bool NeedsCleanup)> ProcessAsync(VideoRecord video, bool requiresAudio);
    }
}
