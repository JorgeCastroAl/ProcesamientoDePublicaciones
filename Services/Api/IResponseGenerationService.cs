using System.Collections.Generic;
using System.Threading.Tasks;
using FluxAnswer.Models;

namespace FluxAnswer.Services.Api
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

        Task<(bool Success, int GeneratedCount)> GenerateCustomCommentsForBotAccountsAsync(
            VideoRecord video,
            int customCommentsPerAccount);
    }
}
