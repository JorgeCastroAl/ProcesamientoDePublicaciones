using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Services.Pipeline
{
    public interface ICommentsStageService
    {
        Task ProcessAsync(VideoRecord video);
    }
}
