using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Services.Pipeline
{
    public interface IBotAccountVideoStageService
    {
        Task ProcessAsync(VideoRecord video);
    }
}
