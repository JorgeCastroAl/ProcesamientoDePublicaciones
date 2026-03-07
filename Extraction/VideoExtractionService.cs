using System;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using FluxAnswer.Repositories;
using FluxAnswer.Models;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Service for extracting videos from TikTok accounts using yt-dlp.
    /// </summary>
    public class VideoExtractionService : IVideoExtractionService
    {
        private readonly IVideoRepo _videoRepo;
        private readonly IYtDlpWrapper _ytDlp;
        private readonly ISocialNetworkRepo _socialNetworkRepo;
        private string? _tiktokSocialNetworkId;

        public VideoExtractionService(IVideoRepo videoRepo, IYtDlpWrapper ytDlp, ISocialNetworkRepo socialNetworkRepo)
        {
            _videoRepo = videoRepo;
            _ytDlp = ytDlp;
            _socialNetworkRepo = socialNetworkRepo;
        }

        public async Task<ExtractionResult> ExtractVideosAsync(AccountToFollow account)
        {
            NormalizeAccount(account);

            var result = new ExtractionResult
            {
                AccountUsername = account.Username
            };

            try
            {
                if (string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(account.ProfileUrl))
                {
                    var message = $"Invalid account data (Id={account.Id ?? "n/a"}): Username/ProfileUrl required";
                    Log.Warning(message);
                    result.Errors.Add(message);
                    return result;
                }

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
                var socialNetworkId = await GetTikTokSocialNetworkIdAsync();
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
                            SocialNetworkId = socialNetworkId,
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
            if (string.IsNullOrWhiteSpace(accountUsername))
            {
                return 10;
            }

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

        private async Task<string?> GetTikTokSocialNetworkIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(_tiktokSocialNetworkId))
                return _tiktokSocialNetworkId;

            try
            {
                var network = await _socialNetworkRepo.GetByCodeAsync("tiktok");
                _tiktokSocialNetworkId = network?.Id;

                if (string.IsNullOrWhiteSpace(_tiktokSocialNetworkId))
                    Log.Warning("Social network 'tiktok' not found. New video records will be created without social_network_id.");

                return _tiktokSocialNetworkId;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve social network id for 'tiktok'");
                return null;
            }
        }

        private static void NormalizeAccount(AccountToFollow account)
        {
            account.Username = account.Username?.Trim() ?? string.Empty;
            account.ProfileUrl = account.ProfileUrl?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(account.Username) && string.IsNullOrWhiteSpace(account.ProfileUrl))
            {
                var cleanUsername = account.Username.TrimStart('@');
                if (!string.IsNullOrWhiteSpace(cleanUsername))
                {
                    account.ProfileUrl = $"https://www.tiktok.com/@{cleanUsername}";
                }
            }

            if (string.IsNullOrWhiteSpace(account.Username) && !string.IsNullOrWhiteSpace(account.ProfileUrl))
            {
                var marker = "/@";
                var markerIndex = account.ProfileUrl.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex >= 0)
                {
                    var start = markerIndex + marker.Length;
                    var tail = account.ProfileUrl[start..];
                    var endIndex = tail.IndexOfAny(new[] { '/', '?', '&' });
                    account.Username = (endIndex >= 0 ? tail[..endIndex] : tail).Trim();
                }
            }
        }
    }
}

