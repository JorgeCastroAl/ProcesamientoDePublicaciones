using System.Threading.Tasks;
using System.Collections.Generic;
using FluxAnswer.Models;

namespace FluxAnswer.Services.Database
{
    /// <summary>
    /// Encapsulates the response generation stage for a video.
    /// </summary>
    public interface IVideoResponseStageService
    {
        Task ProcessAsync(VideoRecord video, bool skipTranscription, List<CommentData>? comments = null);
    }
}
