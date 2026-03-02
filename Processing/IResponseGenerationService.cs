using System.Collections.Generic;
using System.Threading.Tasks;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Service for generating responses using local REST API.
    /// </summary>
    public interface IResponseGenerationService
    {
        Task<ResponseResult> GenerateResponseAsync(
            VideoRecord video, 
            string transcription, 
            List<CommentData> comments);
    }
}
