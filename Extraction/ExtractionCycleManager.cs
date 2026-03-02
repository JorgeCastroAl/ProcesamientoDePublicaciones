using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using VideoProcessingSystemV2.Configuration;
using VideoProcessingSystemV2.Repositories;

namespace VideoProcessingSystemV2.Extraction
{
    /// <summary>
    /// Manages periodic extraction cycles for all accounts.
    /// </summary>
    public class ExtractionCycleManager : IExtractionCycleManager
    {
        private readonly IAccountToFollowRepo _accountToFollowRepo;
        private readonly IVideoExtractionService _extractionService;
        private readonly IConfigurationManager _config;
        private System.Threading.Timer? _timer;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;
        private readonly object _lock = new object();

        public DateTime? LastExtractionTime { get; private set; }
        public event EventHandler<ExtractionCompletedEventArgs>? ExtractionCompleted;

        public ExtractionCycleManager(
            IAccountToFollowRepo accountToFollowRepo,
            IVideoExtractionService extractionService,
            IConfigurationManager config)
        {
            _accountToFollowRepo = accountToFollowRepo;
            _extractionService = extractionService;
            _config = config;
        }

        public Task StartAsync()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Log.Warning("Extraction cycle manager is already running");
                    return Task.CompletedTask;
                }

                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();

                var intervalMs = _config.ExtractionIntervalMinutes * 60 * 1000;
                _timer = new System.Threading.Timer(
                    async _ => await ExecuteCycleAsync(),
                    null,
                    TimeSpan.Zero, // Start immediately
                    TimeSpan.FromMilliseconds(intervalMs)
                );

                Log.Information(
                    "Extraction cycle manager started with interval: {Interval} minutes",
                    _config.ExtractionIntervalMinutes
                );
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    Log.Warning("Extraction cycle manager is not running");
                    return Task.CompletedTask;
                }

                _isRunning = false;
                _cancellationTokenSource?.Cancel();
                _timer?.Dispose();
                _timer = null;

                Log.Information("Extraction cycle manager stopped");
            }

            return Task.CompletedTask;
        }

        private async Task ExecuteCycleAsync()
        {
            if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                return;
            }

            var startTime = DateTime.UtcNow;
            var eventArgs = new ExtractionCompletedEventArgs
            {
                StartTime = startTime
            };

            try
            {
                Log.Information("========== Starting Extraction Cycle ==========");

                // Get all accounts to follow
                var accounts = await _accountToFollowRepo.GetAllAsync();
                Log.Information("Found {Count} accounts to process", accounts.Count);

                // Process each account sequentially
                foreach (var account in accounts)
                {
                    if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        Log.Information("Extraction cycle cancelled");
                        break;
                    }

                    try
                    {
                        Log.Information("Processing account: {Username}", account.Username);
                        var result = await _extractionService.ExtractVideosAsync(account);

                        eventArgs.Results.Add(result);
                        eventArgs.AccountsProcessed++;
                        eventArgs.TotalVideosFound += result.VideosFound;
                        eventArgs.TotalVideosInserted += result.VideosInserted;
                        eventArgs.TotalDuplicatesSkipped += result.DuplicatesSkipped;

                        if (!result.IsSuccess)
                        {
                            Log.Warning(
                                "Extraction failed for {Username} with {ErrorCount} errors",
                                account.Username,
                                result.Errors.Count
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing account: {Username}", account.Username);
                        
                        var errorResult = new Models.ExtractionResult
                        {
                            AccountUsername = account.Username
                        };
                        errorResult.Errors.Add(ex.Message);
                        eventArgs.Results.Add(errorResult);
                    }
                }

                LastExtractionTime = DateTime.UtcNow;
                eventArgs.EndTime = LastExtractionTime.Value;

                Log.Information(
                    "========== Extraction Cycle Complete: {Accounts} accounts, {Found} found, {Inserted} inserted, {Skipped} skipped in {Duration:F2}s ==========",
                    eventArgs.AccountsProcessed,
                    eventArgs.TotalVideosFound,
                    eventArgs.TotalVideosInserted,
                    eventArgs.TotalDuplicatesSkipped,
                    eventArgs.Duration.TotalSeconds
                );

                // Fire completion event
                ExtractionCompleted?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fatal error during extraction cycle");
                eventArgs.EndTime = DateTime.UtcNow;
                ExtractionCompleted?.Invoke(this, eventArgs);
            }
        }
    }
}
