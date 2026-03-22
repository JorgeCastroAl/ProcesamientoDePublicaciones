using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using FluxAnswer.Configuration;
using FluxAnswer.Models;
using FluxAnswer.Repositories;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Manages periodic search-based extraction using TikTokApi.
    /// For each account: calls SearchAsync(username, allActiveKeywords, maxAgeDays)
    /// then registers new videos in PocketBase.
    /// </summary>
    public class SearchExtractionCycleManager : ISearchExtractionCycleManager
    {
        private readonly IAccountToFollowRepo _accountRepo;
        private readonly ISearchCriteriaRepo _criteriaRepo;
        private readonly ITikTokApiSearchService _searchService;
        private readonly IVideoRepo _videoRepo;
        private readonly ISocialNetworkRepo _socialNetworkRepo;
        private readonly IConfigurationManager _config;
        private System.Threading.Timer? _timer;
        private CancellationTokenSource? _cts;
        private bool _isRunning;
        private readonly object _lock = new();
        private string? _tiktokSocialNetworkId;

        public DateTime? LastExtractionTime { get; private set; }
        public event EventHandler<ExtractionCompletedEventArgs>? ExtractionCompleted;

        public SearchExtractionCycleManager(
            IAccountToFollowRepo accountRepo,
            ISearchCriteriaRepo criteriaRepo,
            ITikTokApiSearchService searchService,
            IVideoRepo videoRepo,
            ISocialNetworkRepo socialNetworkRepo,
            IConfigurationManager config)
        {
            _accountRepo = accountRepo;
            _criteriaRepo = criteriaRepo;
            _searchService = searchService;
            _videoRepo = videoRepo;
            _socialNetworkRepo = socialNetworkRepo;
            _config = config;
        }

        public Task StartAsync()
        {
            lock (_lock)
            {
                if (_isRunning) return Task.CompletedTask;
                _isRunning = true;
                _cts = new CancellationTokenSource();

                var intervalMs = _config.ExtractionIntervalMinutes * 60 * 1000;
                _timer = new System.Threading.Timer(
                    async _ => await ExecuteCycleAsync(),
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(intervalMs));

                Log.Information("SearchExtractionCycleManager started (interval={Interval}min)", _config.ExtractionIntervalMinutes);
            }
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            lock (_lock)
            {
                if (!_isRunning) return Task.CompletedTask;
                _isRunning = false;
                _cts?.Cancel();
                _timer?.Dispose();
                _timer = null;
                Log.Information("SearchExtractionCycleManager stopped");
            }
            return Task.CompletedTask;
        }

        private async Task ExecuteCycleAsync()
        {
            if (_cts?.Token.IsCancellationRequested == true) return;

            var eventArgs = new ExtractionCompletedEventArgs { StartTime = DateTime.UtcNow };

            try
            {
                Log.Information("========== Starting Search Extraction Cycle ==========");

                var accounts = await _accountRepo.GetAllAsync();
                var criteria = await _criteriaRepo.GetActiveAsync();
                var keywords = criteria.Select(c => c.Keyword).Where(k => !string.IsNullOrWhiteSpace(k)).ToList();

                if (keywords.Count == 0)
                {
                    Log.Warning("No active search criteria found, skipping cycle");
                    return;
                }

                var maxAgeDays = _config.SearchMaxAgeDays;
                var socialNetworkId = await GetTikTokSocialNetworkIdAsync();

                Log.Information("Search extraction: {AccountCount} accounts, {KeywordCount} keywords, maxAge={MaxAge}d",
                    accounts.Count, keywords.Count, maxAgeDays);

                foreach (var account in accounts)
                {
                    if (_cts?.Token.IsCancellationRequested == true) break;
                    if (string.IsNullOrWhiteSpace(account.Username)) continue;

                    var result = new ExtractionResult { AccountUsername = account.Username };

                    try
                    {
                        // Single call: pass all keywords at once
                        var videos = await _searchService.SearchAsync(account.Username, keywords, maxAgeDays);
                        result.VideosFound = videos.Count;

                        foreach (var video in videos)
                        {
                            try
                            {
                                var exists = await _videoRepo.ExistsByTikTokIdAsync(video.Id);
                                if (exists) { result.DuplicatesSkipped++; continue; }

                                var record = new VideoRecord
                                {
                                    TiktokVideoId = video.Id,
                                    SocialNetworkId = socialNetworkId,
                                    AccountUsername = video.Uploader,
                                    VideoUrl = video.WebpageUrl,
                                    Title = video.Title,
                                    Author = video.Uploader,
                                    UploadDate = video.GetUploadDateTime() ?? DateTime.UtcNow,
                                    Status = "pending",
                                    SkipTranscription = _config.SkipTranscription,
                                };

                                await _videoRepo.CreateAsync(record);
                                result.VideosInserted++;
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error registering video {VideoId}", video.Id);
                                result.Errors.Add($"Video {video.Id}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Search failed for @{Username}", account.Username);
                        result.Errors.Add(ex.Message);
                    }

                    eventArgs.Results.Add(result);
                    eventArgs.AccountsProcessed++;
                    eventArgs.TotalVideosFound += result.VideosFound;
                    eventArgs.TotalVideosInserted += result.VideosInserted;
                    eventArgs.TotalDuplicatesSkipped += result.DuplicatesSkipped;
                }

                LastExtractionTime = DateTime.UtcNow;
                eventArgs.EndTime = LastExtractionTime.Value;

                Log.Information(
                    "========== Search Extraction Complete: {Accounts} accounts, {Found} found, {Inserted} inserted, {Skipped} skipped in {Duration:F2}s ==========",
                    eventArgs.AccountsProcessed, eventArgs.TotalVideosFound,
                    eventArgs.TotalVideosInserted, eventArgs.TotalDuplicatesSkipped,
                    eventArgs.Duration.TotalSeconds);

                ExtractionCompleted?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fatal error during search extraction cycle");
                eventArgs.EndTime = DateTime.UtcNow;
                ExtractionCompleted?.Invoke(this, eventArgs);
            }
        }

        private async Task<string?> GetTikTokSocialNetworkIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(_tiktokSocialNetworkId)) return _tiktokSocialNetworkId;
            try
            {
                var network = await _socialNetworkRepo.GetByCodeAsync("tiktok");
                _tiktokSocialNetworkId = network?.Id;
                return _tiktokSocialNetworkId;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve social network id for 'tiktok'");
                return null;
            }
        }
    }
}
