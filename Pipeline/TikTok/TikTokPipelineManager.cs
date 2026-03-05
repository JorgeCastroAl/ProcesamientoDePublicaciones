using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using FluxAnswer.Configuration;
using FluxAnswer.Repositories;
using FluxAnswer.Models;
using FluxAnswer.Services.Media;
using FluxAnswer.Services.Scraping.TikTok;
using FluxAnswer.Services.AI;
using FluxAnswer.Services.Database;
using FluxAnswer.Services.Pipeline;

namespace FluxAnswer.Pipeline.TikTok
{
    /// <summary>
    /// Manages continuous video processing pipeline.
    /// </summary>
    public class TikTokPipelineManager : ITikTokPipelineManager
    {
        private readonly IVideoRepo _videoRepo;
        private readonly ICommentsExtractionService _commentsService;
        private readonly IAudioStageService _audioStageService;
        private readonly ITranscriptionStageService _transcriptionStageService;
        private readonly IVideoResponseStageService _responseStageService;
        private readonly IBotAccountVideoStageService _botAccountVideoStageService;
        private readonly IConfigurationManager _config;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _processingTask;
        private bool _isRunning;
        private readonly object _lock = new object();
        private DateTime? _lastProcessedTime;

        public event EventHandler<TikTokVideoProcessedEventArgs>? ItemProcessed;
        public event EventHandler<TikTokVideoProcessedEventArgs>? VideoProcessed;

        public TikTokPipelineManager(
            IVideoRepo videoRepo,
            ICommentsExtractionService commentsService,
            IAudioStageService audioStageService,
            ITranscriptionStageService transcriptionStageService,
            IVideoResponseStageService responseStageService,
            IBotAccountVideoStageService botAccountVideoStageService,
            IConfigurationManager config)
        {
            _videoRepo = videoRepo;
            _commentsService = commentsService;
            _audioStageService = audioStageService;
            _transcriptionStageService = transcriptionStageService;
            _responseStageService = responseStageService;
            _botAccountVideoStageService = botAccountVideoStageService;
            _config = config;
        }

        public Task StartAsync()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Log.Warning("Processing pipeline manager is already running");
                    return Task.CompletedTask;
                }

                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();

                _processingTask = Task.Run(() => ProcessingLoopAsync(_cancellationTokenSource.Token));

                Log.Information("Processing pipeline manager started");
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    Log.Warning("Processing pipeline manager is not running");
                    return;
                }

                _isRunning = false;
                _cancellationTokenSource?.Cancel();
            }

            if (_processingTask != null)
            {
                await _processingTask;
            }

            Log.Information("Processing pipeline manager stopped");
        }

        public async Task<TikTokPipelineStatistics> GetStatisticsAsync()
        {
            try
            {
                // Query database for real-time statistics
                var allVideos = await _videoRepo.GetAllAsync();
                
                var stats = new TikTokPipelineStatistics
                {
                    PendingCount = allVideos.Count(v => v.Status.ToLower() == "pending"),
                    ProcessingCount = allVideos.Count(v => v.Status.ToLower() == "processing"),
                    CompletedCount = allVideos.Count(v => v.Status.ToLower() == "completed"),
                    FailedCount = allVideos.Count(v => v.Status.ToLower() == "failed"),
                    LastProcessedTime = _lastProcessedTime
                };

                return stats;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting processing statistics");
                return new TikTokPipelineStatistics
                {
                    LastProcessedTime = _lastProcessedTime,
                    LastError = ex.Message
                };
            }
        }

        private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Get next incomplete video directly from database (FIFO by updated, created)
                    Log.Debug("Querying database for next incomplete video...");
                    var video = await _videoRepo.GetNextIncompleteVideoAsync();

                    if (video == null)
                    {
                        // No videos to process, wait
                        Log.Debug("No incomplete videos, waiting {Seconds}s...", _config.ProcessingPollIntervalSeconds);
                        await Task.Delay(_config.ProcessingPollIntervalSeconds * 1000, cancellationToken);
                        continue;
                    }

                    Log.Information("Processing next incomplete video: {VideoId}", video.TiktokVideoId);
                    await ProcessNextVideoAsync(video);

                    _lastProcessedTime = DateTime.UtcNow;
                }
                catch (OperationCanceledException)
                {
                    Log.Information("Processing loop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Processing thread crashed, restarting in 10 seconds...");
                    await Task.Delay(10000, cancellationToken);
                }
            }
        }

        private bool IsFullyCompleted(VideoRecord video)
        {
            // Si skip_transcription estÃ¡ activo, no requerimos audio/transcripciÃ³n
            bool audioOk = _config.SkipTranscription || video.AudioDownloaded;
            bool transcriptionOk = _config.SkipTranscription || video.TranscriptionCompleted;
            
            // Video is fully completed if all required stages are done
            return audioOk && 
                   video.CommentsExtracted && 
                   transcriptionOk && 
                     video.ResponseGenerated &&
                     video.CustomCommentsSuccess;
        }

        private async Task ProcessNextVideoAsync(VideoRecord video)
        {
            var startTime = DateTime.UtcNow;
            string? audioPath = null;
            bool needsAudioFile = false;
            var extractedComments = new List<CommentData>();

            try
            {
                Log.Information("========== Processing video: {VideoId} ==========", video.TiktokVideoId);
                Log.Information("Stage status - Audio: {Audio}, Comments: {Comments}, Transcription: {Trans}, Response: {Resp}",
                    video.AudioDownloaded, video.CommentsExtracted, video.TranscriptionCompleted, video.ResponseGenerated);

                bool requiresAudio = !_config.SkipTranscription && !video.TranscriptionCompleted;

                // Step 1: Download MP3 (only when transcription is required)
                await Task.Run(async () =>
                {
                    var audioResult = await _audioStageService.ProcessAsync(video, requiresAudio);
                    audioPath = audioResult.AudioPath;
                    if (audioResult.NeedsCleanup)
                    {
                        needsAudioFile = true;
                    }
                });

                // Step 2: Extract comments (if not already done)
                if (!video.CommentsExtracted)
                {
                    try
                    {
                        Log.Information("Step 2/4: Extracting comments for {VideoId}", video.TiktokVideoId);
                        video.SetStatus(VideoStatus.ExtractingComments);
                        await UpdateVideoAsync(video);
                        
                        var comments = await _commentsService.ExtractCommentsAsync(video.VideoUrl, _config.CommentsExtractionLimit);
                        extractedComments = comments;
                        Log.Information("[OK] Extracted {Count} comments for {VideoId}", comments.Count, video.TiktokVideoId);

                        // Mark extraction stage as completed even when no comments are returned.
                        // Response stage can still use transcription/title as fallback input.
                        video.CommentsExtracted = true;
                        video.ErrorMessage = null;
                        await UpdateVideoAsync(video);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[ERROR] Failed to extract comments for {VideoId}: {Error}", 
                            video.TiktokVideoId, ex.Message);
                        // Keep as not extracted when extraction fails
                        video.CommentsExtracted = false;
                        video.ErrorMessage = $"Comments extraction failed: {ex.Message}";
                        await UpdateVideoAsync(video);
                    }
                }
                else
                {
                    Log.Information("[OK] Step 2/4: Comments already extracted, skipping");
                }

                // Step 3: Transcribe audio (if not already done and not skipped by config)
                await Task.Run(async () =>
                {
                    var downloadedInTranscription = await _transcriptionStageService.ProcessAsync(
                        video,
                        _config.SkipTranscription,
                        audioPath);

                    if (downloadedInTranscription)
                    {
                        needsAudioFile = true;
                    }
                });

                // Step 4: Generate response in independent thread
                await Task.Run(async () =>
                {
                    await _responseStageService.ProcessAsync(video, _config.SkipTranscription, extractedComments);
                });

                // Step 5: Generate bot-account-video records in independent thread
                await Task.Run(async () =>
                {
                    await _botAccountVideoStageService.ProcessAsync(video);
                });

                // Completion is now decided by the BotAccountVideo stage
                if (IsFullyCompleted(video))
                {
                    video.SetStatus(VideoStatus.Completed);
                    if (string.IsNullOrEmpty(video.ErrorMessage) ||
                        !video.ErrorMessage.Contains("failed"))
                    {
                        video.ErrorMessage = null;
                    }
                    await UpdateVideoAsync(video);

                    Log.Information("========== [OK] All stages completed for video: {VideoId} ==========",
                        video.TiktokVideoId);
                }
                else
                {
                    Log.Warning("[WARN] Video {VideoId} has incomplete stages", video.TiktokVideoId);
                }

                var duration = DateTime.UtcNow - startTime;

                // Fire event
                var processedEventArgs = new TikTokVideoProcessedEventArgs(video)
                {
                    Success = IsFullyCompleted(video),
                    ProcessingDuration = duration
                };
                ItemProcessed?.Invoke(this, processedEventArgs);
                VideoProcessed?.Invoke(this, processedEventArgs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ERROR] Video processing failed for {VideoId} at status {Status}: {Error}", 
                    video.TiktokVideoId, video.Status, ex.Message);
                Log.Error("Stack trace: {StackTrace}", ex.StackTrace);

                // Mark video as failed
                video.SetStatus(VideoStatus.Failed);
                video.ErrorMessage = $"Failed at {video.Status}: {ex.Message}";
                await UpdateVideoAsync(video);

                var duration = DateTime.UtcNow - startTime;

                // Fire event
                var processedEventArgs = new TikTokVideoProcessedEventArgs(video)
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingDuration = duration
                };
                ItemProcessed?.Invoke(this, processedEventArgs);
                VideoProcessed?.Invoke(this, processedEventArgs);
            }
            finally
            {
                // Cleanup temporary audio file only if we downloaded it in this run
                if (needsAudioFile && !string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
                {
                    try
                    {
                        File.Delete(audioPath);
                        Log.Debug("[OK] Cleaned up temporary audio file: {Path}", audioPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to delete temporary audio file: {Path}", audioPath);
                    }
                }
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



