using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Services.Pipeline
{
    public interface ITranscriptionStageService
    {
        Task<bool> ProcessAsync(VideoRecord video, bool skipTranscription, string? audioPath);
    }
}
