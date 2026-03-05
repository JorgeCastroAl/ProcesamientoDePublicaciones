using System;
using System.Threading.Tasks;
using FluxAnswer.Configuration;
using FluxAnswer.Models;
using FluxAnswer.Repositories;
using FluxAnswer.Services.Api;
using Serilog;

namespace FluxAnswer.Services.Pipeline
{
    public class BotAccountVideoStageService : IBotAccountVideoStageService
    {
        private readonly IVideoRepo _videoRepo;
        private readonly IResponseGenerationService _responseGenerationService;
        private readonly IConfigurationManager _config;

        public BotAccountVideoStageService(
            IVideoRepo videoRepo,
            IResponseGenerationService responseGenerationService,
            IConfigurationManager config)
        {
            _videoRepo = videoRepo;
            _responseGenerationService = responseGenerationService;
            _config = config;
        }

        public async Task ProcessAsync(VideoRecord video)
        {
            if (!video.ResponseGenerated || string.IsNullOrWhiteSpace(video.ResponseText))
            {
                Log.Warning("[WAIT] BotAccountVideo stage skipped for {VideoId}: response_text is not ready", video.TiktokVideoId);
                video.CustomCommentsSuccess = false;
                video.CustomCommentsGeneratedCount = 0;
                await UpdateVideoAsync(video);
                return;
            }

            Log.Information("Step 5/5: Generating BotAccountVideo records for {VideoId}", video.TiktokVideoId);
            video.SetStatus(VideoStatus.Processing);
            await UpdateVideoAsync(video);

            var customCommentsResult = await _responseGenerationService.GenerateCustomCommentsForBotAccountsAsync(
                video,
                _config.CustomCommentsPerBotAccount);

            video.CustomCommentsSuccess = customCommentsResult.Success;
            video.CustomCommentsGeneratedCount = customCommentsResult.GeneratedCount;
            await UpdateVideoAsync(video);

            if (!customCommentsResult.Success)
            {
                Log.Warning("[WARN] BotAccountVideo stage completed with errors for video {VideoId}", video.TiktokVideoId);
            }
            else
            {
                Log.Information("[OK] BotAccountVideo stage completed for {VideoId}. Records: {Count}",
                    video.TiktokVideoId,
                    customCommentsResult.GeneratedCount);
            }
        }

        private async Task UpdateVideoAsync(VideoRecord video)
        {
            if (string.IsNullOrWhiteSpace(video.Id))
            {
                throw new InvalidOperationException("Video Id is null or empty, cannot update video entity");
            }

            await _videoRepo.UpdateAsync(video.Id, video);
        }
    }
}
