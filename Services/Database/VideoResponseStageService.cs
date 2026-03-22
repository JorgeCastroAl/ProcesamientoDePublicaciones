using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IExtractedCommentRepo _commentRepo;

        public VideoResponseStageService(
            IVideoRepo videoRepo,
            IResponseGenerationService responseService,
            IExtractedCommentRepo commentRepo)
        {
            _videoRepo = videoRepo;
            _responseService = responseService;
            _commentRepo = commentRepo;
        }

        public async Task ProcessAsync(VideoRecord video)
        {
            if (video.ResponseGenerated)
            {
                Log.Information("[OK] Response already generated for {VideoId}, skipping", video.TiktokVideoId);
                return;
            }

            bool hasAnyInput = video.CommentsExtracted
                || !string.IsNullOrWhiteSpace(video.Transcription)
                || !string.IsNullOrWhiteSpace(video.Title);

            if (!hasAnyInput)
            {
                Log.Information("[WAIT] Cannot generate response for {VideoId} - No input data available", video.TiktokVideoId);
                return;
            }

            try
            {
                Log.Information("Generating response for {VideoId}", video.TiktokVideoId);

                video.SetStatus(VideoStatus.GeneratingResponse);
                await UpdateVideoAsync(video);

                // Leer comentarios desde PocketBase
                var extractedRecords = await _commentRepo.GetByVideoIdAsync(video.VideoUrl);
                var comments = extractedRecords
                    .Select(r => new CommentData
                    {
                        CommentId = r.CommentExternalId,
                        Text = r.Text,
                        Author = r.Author,
                        LikeCount = r.LikeCount,
                    })
                    .ToList();

                Log.Information("Using {Count} comments from PocketBase for {VideoId}", comments.Count, video.TiktokVideoId);

                if (comments.Count == 0 && string.IsNullOrWhiteSpace(video.Transcription) && string.IsNullOrWhiteSpace(video.Title))
                {
                    Log.Warning("No comments, transcription, or title available for {VideoId}; response generation deferred", video.TiktokVideoId);
                    return;
                }

                var responseResult = await _responseService.GenerateResponseAsync(
                    video, video.Transcription ?? string.Empty, comments);

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
                    video.StatusCode = responseResult.StatusCode;

                    await UpdateVideoAsync(video);
                    Log.Information("[OK] Response stored for {VideoId}", video.TiktokVideoId);
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
                throw new InvalidOperationException("Video Id is null or empty, cannot update video entity");
            await _videoRepo.UpdateAsync(video.Id, video);
        }
    }
}
