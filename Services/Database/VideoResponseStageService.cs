using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;
using FluxAnswer.Models;
using FluxAnswer.Repositories;
using FluxAnswer.Services.Api;

namespace FluxAnswer.Services.Database
{
    /// <summary>
    /// Dedicated service for the response stage: prerequisites, API call and video update.
    /// </summary>
    public class VideoResponseStageService : IVideoResponseStageService
    {
        private readonly IVideoRepo _videoRepo;
        private readonly IResponseGenerationService _responseService;

        public VideoResponseStageService(
            IVideoRepo videoRepo,
            IResponseGenerationService responseService)
        {
            _videoRepo = videoRepo;
            _responseService = responseService;
        }

        public async Task ProcessAsync(VideoRecord video, bool skipTranscription, List<CommentData>? comments = null)
        {
            bool hasAnyInput = video.CommentsExtracted
                || !string.IsNullOrWhiteSpace(video.Transcription)
                || !string.IsNullOrWhiteSpace(video.Title);

            if (video.ResponseGenerated)
            {
                Log.Information("[OK] Step 4/4: Response already generated, skipping");
                return;
            }

            if (!hasAnyInput)
            {
                Log.Information("[WAIT] Step 4/4: Cannot generate response yet - No input data available");
                Log.Information("  - Comments Extracted: {CommentsExtracted}", video.CommentsExtracted);
                Log.Information("  - Has Transcription Text: {HasTranscriptionText}", !string.IsNullOrWhiteSpace(video.Transcription));
                Log.Information("  - Has Title: {HasTitle}", !string.IsNullOrWhiteSpace(video.Title));
                Log.Information("  - Skip Transcription: {SkipTranscription}", skipTranscription);
                return;
            }

            try
            {
                Log.Information("Step 4/4: Generating response for {VideoId}", video.TiktokVideoId);
                Log.Information("=== RESPONSE GENERATION START ===");
                Log.Information("Video ID: {VideoId}", video.Id);
                Log.Information("TikTok Video ID: {TiktokVideoId}", video.TiktokVideoId);

                video.SetStatus(VideoStatus.GeneratingResponse);
                await UpdateVideoAsync(video);

                var responseInputComments = comments ?? new List<CommentData>();
                Log.Information("Using {Count} comments for response generation", responseInputComments.Count);

                if (responseInputComments.Count == 0 && string.IsNullOrWhiteSpace(video.Transcription) && string.IsNullOrWhiteSpace(video.Title))
                {
                    Log.Warning("No comments, transcription, or title available for {VideoId}; response generation deferred", video.TiktokVideoId);
                    return;
                }

                var responseResult = await _responseService.GenerateResponseAsync(
                    video, video.Transcription ?? string.Empty, responseInputComments);

                if (responseResult.Success)
                {
                    if (string.IsNullOrWhiteSpace(video.Id))
                    {
                        Log.Warning("Video Id is null or empty, cannot persist generated response");
                        return;
                    }

                    video.ResponseText = responseResult.ResponseText;
                    video.ApiStatus = "success";
                    video.Posted = false;
                    video.PostedAt = null;
                    video.ResponseGenerated = true;
                    video.ErrorMessage = null;

                    await UpdateVideoAsync(video);
                    Log.Information("[OK] Response stored in video record for {VideoId}", video.TiktokVideoId);
                }
                else
                {
                    video.ApiStatus = "error";
                    video.ErrorMessage = $"Response generation failed: {responseResult.ErrorMessage ?? "Unknown error"}";
                    await UpdateVideoAsync(video);
                    Log.Warning("[WARN] Response generation failed for {VideoId}: {Error}",
                        video.TiktokVideoId, responseResult.ErrorMessage ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ERROR] Failed to generate response for {VideoId}: {Error}",
                    video.TiktokVideoId, ex.Message);
                video.ApiStatus = "error";
                video.ErrorMessage = $"Response generation failed: {ex.Message}";
                await UpdateVideoAsync(video);
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
