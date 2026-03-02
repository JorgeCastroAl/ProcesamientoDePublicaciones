using System;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using VideoProcessingSystemV2.Repositories;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Extraction
{
    /// <summary>
    /// Service for extracting videos from TikTok accounts using yt-dlp.
    /// </summary>
    public class VideoExtractionService : IVideoExtractionService
    {
        private readonly IVideoRepo _videoRepo;
        private readonly IYtDlpWrapper _ytDlp;

        public VideoExtractionService(IVideoRepo videoRepo, IYtDlpWrapper ytDlp)
        {
            _videoRepo = videoRepo;
            _ytDlp = ytDlp;
        }

        public async Task<ExtractionResult> ExtractVideosAsync(AccountToFollow account)
        {
            var result = new ExtractionResult
            {
                AccountUsername = account.Username
            };

            try
            {
                Log.Information("Starting video extraction for account: {Username}", account.Username);

                // Determine extraction count (10 for first time, 3 for subsequent)
                var count = await DetermineExtractionCountAsync(account.Username);
                Log.Information("Extraction count for {Username}: {Count}", account.Username, count);

                // Extract videos using yt-dlp
                var videos = await _ytDlp.ExtractVideosAsync(account.ProfileUrl, count);
                result.VideosFound = videos.Count;

                if (videos.Count == 0)
                {
                    Log.Warning("No videos found for account: {Username}", account.Username);
                    return result;
                }

                // Process each video in reverse chronological order (newest first)
                foreach (var video in videos)
                {
                    try
                    {
                        // Check for duplicates
                        var exists = await _videoRepo.ExistsByTikTokIdAsync(video.Id);

                        if (exists)
                        {
                            result.DuplicatesSkipped++;
                            Log.Debug("Skipping duplicate video: {VideoId}", video.Id);
                            continue;
                        }

                        // Create video record with pending status
                        var videoRecord = new VideoRecord
                        {
                            TiktokVideoId = video.Id,
                            AccountUsername = account.Username,
                            VideoUrl = video.WebpageUrl,
                            Title = video.Title,
                            Author = video.Uploader,
                            UploadDate = video.GetUploadDateTime() ?? DateTime.UtcNow,
                            Status = "pending"
                        };

                        await _videoRepo.CreateAsync(videoRecord);
                        result.VideosInserted++;

                        Log.Information(
                            "Inserted video: {VideoId} from {Username}",
                            video.Id,
                            account.Username
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing video {VideoId}", video.Id);
                        result.Errors.Add($"Video {video.Id}: {ex.Message}");
                    }
                }

                Log.Information(
                    "Extraction complete for {Username}: {Found} found, {Inserted} inserted, {Skipped} skipped",
                    account.Username,
                    result.VideosFound,
                    result.VideosInserted,
                    result.DuplicatesSkipped
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Extraction failed for {Username}", account.Username);
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Determines the extraction count based on account history.
        /// Returns 10 for first-time extraction, 3 for subsequent extractions.
        /// </summary>
        private async Task<int> DetermineExtractionCountAsync(string accountUsername)
        {
            try
            {
                var existingVideos = await _videoRepo.GetByAccountUsernameAsync(accountUsername);
                return existingVideos.Count == 0 ? 10 : 3;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error determining extraction count for {Username}, defaulting to 10", accountUsername);
                return 10;
            }
        }
    }
}
