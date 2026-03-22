using System;
using System.Threading.Tasks;
using FluxAnswer.Configuration;
using FluxAnswer.Models;
using FluxAnswer.Repositories;
using FluxAnswer.Services.Scraping.TikTok;
using Serilog;

namespace FluxAnswer.Services.Pipeline
{
    public class CommentsStageService : ICommentsStageService
    {
        private readonly IVideoRepo _videoRepo;
        private readonly ICommentsExtractionService _commentsService;
        private readonly IConfigurationManager _config;

        public CommentsStageService(
            IVideoRepo videoRepo,
            ICommentsExtractionService commentsService,
            IConfigurationManager config)
        {
            _videoRepo = videoRepo;
            _commentsService = commentsService;
            _config = config;
        }

        public async Task ProcessAsync(VideoRecord video)
        {
            if (video.CommentsExtracted)
            {
                Log.Information("[OK] Comments already extracted for {VideoId}, skipping", video.TiktokVideoId);
                return;
            }

            try
            {
                Log.Information("Extracting comments for {VideoId}", video.TiktokVideoId);
                video.SetStatus(VideoStatus.ExtractingComments);
                await UpdateVideoAsync(video);

                var comments = await _commentsService.ExtractCommentsAsync(video.VideoUrl, _config.CommentsExtractionLimit);
                Log.Information("[OK] Extracted {Count} comments for {VideoId}", comments.Count, video.TiktokVideoId);

                video.CommentsExtracted = true;
                video.ErrorMessage = null;
                await UpdateVideoAsync(video);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ERROR] Failed to extract comments for {VideoId}: {Error}",
                    video.TiktokVideoId, ex.Message);
                video.CommentsExtracted = false;
                video.ErrorMessage = $"Comments extraction failed: {ex.Message}";
                await UpdateVideoAsync(video);
            }
        }

        private async Task UpdateVideoAsync(VideoRecord video)
        {
            if (string.IsNullOrWhiteSpace(video.Id))
                throw new InvalidOperationException("Video Id is null or empty, cannot update video entity");
            await _videoRepo.UpdateAsync(video.Id, video);
        }
    }
}
