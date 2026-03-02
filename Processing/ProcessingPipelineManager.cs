using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using VideoProcessingSystemV2.Configuration;
using VideoProcessingSystemV2.Repositories;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Manages continuous video processing pipeline.
    /// </summary>
    public class ProcessingPipelineManager : IProcessingPipelineManager
    {
        private readonly IVideoRepo _videoRepo;
        private readonly ICommentRepo _commentRepo;
        private readonly IResponseRepo _responseRepo;
        private readonly IAudioDownloadService _audioDownloadService;
        private readonly ICommentsExtractionService _commentsService;
        private readonly ITranscriptionService _transcriptionService;
        private readonly IResponseGenerationService _responseService;
        private readonly IConfigurationManager _config;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _processingTask;
        private bool _isRunning;
        private readonly object _lock = new object();
        private DateTime? _lastProcessedTime;

        public event EventHandler<VideoProcessedEventArgs>? VideoProcessed;

        public ProcessingPipelineManager(
            IVideoRepo videoRepo,
            ICommentRepo commentRepo,
            IResponseRepo responseRepo,
            IAudioDownloadService audioDownloadService,
            ICommentsExtractionService commentsService,
            ITranscriptionService transcriptionService,
            IResponseGenerationService responseService,
            IConfigurationManager config)
        {
            _videoRepo = videoRepo;
            _commentRepo = commentRepo;
            _responseRepo = responseRepo;
            _audioDownloadService = audioDownloadService;
            _commentsService = commentsService;
            _transcriptionService = transcriptionService;
            _responseService = responseService;
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

        public async Task<ProcessingStatistics> GetStatisticsAsync()
        {
            try
            {
                // Query database for real-time statistics
                var allVideos = await _videoRepo.GetAllAsync();
                
                var stats = new ProcessingStatistics
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
                return new ProcessingStatistics
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
                    // Get videos that are not completed (filtered from database)
                    Log.Debug("Querying database for incomplete videos...");
                    var incompleteVideos = await _videoRepo.GetIncompleteVideosAsync();
                    incompleteVideos = incompleteVideos
                        .OrderBy(v => v.UpdatedAt)
                        .ThenBy(v => v.CreatedAt)
                        .ToList();

                    Log.Information("=== INCOMPLETE VIDEOS QUERY RESULT ===");
                    Log.Information("Found {Count} incomplete videos", incompleteVideos.Count);
                    
                    if (incompleteVideos.Count > 0)
                    {
                        Log.Information("First 5 incomplete videos:");
                        foreach (var v in incompleteVideos.Take(5))
                        {
                            Log.Information("  - {VideoId}: Audio={Audio}, Comments={Comments}, Transcription={Trans}, Response={Resp}",
                                v.TiktokVideoId, v.AudioDownloaded, v.CommentsExtracted, 
                                v.TranscriptionCompleted, v.ResponseGenerated);
                        }
                    }

                    if (incompleteVideos.Count == 0)
                    {
                        // No videos to process, wait
                        Log.Debug("No incomplete videos, waiting {Seconds}s...", _config.ProcessingPollIntervalSeconds);
                        await Task.Delay(_config.ProcessingPollIntervalSeconds * 1000, cancellationToken);
                        continue;
                    }

                    // Process first incomplete video (FIFO)
                    var video = incompleteVideos.First();
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
            // Si skip_transcription está activo, no requerimos audio/transcripción
            bool audioOk = _config.SkipTranscription || video.AudioDownloaded;
            bool transcriptionOk = _config.SkipTranscription || video.TranscriptionCompleted;
            
            // Video is fully completed if all required stages are done
            return audioOk && 
                   video.CommentsExtracted && 
                   transcriptionOk && 
                   video.ResponseGenerated;
        }

        private async Task ProcessNextVideoAsync(VideoRecord video)
        {
            var startTime = DateTime.UtcNow;
            string? audioPath = null;
            bool needsAudioFile = false;

            try
            {
                Log.Information("========== Processing video: {VideoId} ==========", video.TiktokVideoId);
                Log.Information("Stage status - Audio: {Audio}, Comments: {Comments}, Transcription: {Trans}, Response: {Resp}",
                    video.AudioDownloaded, video.CommentsExtracted, video.TranscriptionCompleted, video.ResponseGenerated);

                bool requiresAudio = !_config.SkipTranscription && !video.TranscriptionCompleted;

                // Step 1: Download MP3 (only when transcription is required)
                if (requiresAudio && !video.AudioDownloaded)
                {
                    try
                    {
                        Log.Information("Step 1/4: Downloading audio for {VideoId}", video.TiktokVideoId);
                        video.SetStatus(VideoStatus.DownloadingAudio);
                        await UpdateVideoAsync(video);
                        
                        audioPath = Path.Combine(
                            _config.TempDirectory,
                            $"{video.TiktokVideoId}.mp3"
                        );
                        
                        await _audioDownloadService.DownloadAudioAsync(video.VideoUrl, audioPath);
                        
                        if (!File.Exists(audioPath))
                        {
                            throw new FileNotFoundException($"Audio file not created: {audioPath}");
                        }
                        
                        var fileInfo = new FileInfo(audioPath);
                        Log.Information("✓ Audio downloaded for {VideoId} - Size: {Size} bytes", 
                            video.TiktokVideoId, fileInfo.Length);
                        
                        // Mark audio as downloaded
                        video.AudioDownloaded = true;
                        await UpdateVideoAsync(video);
                        needsAudioFile = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "✗ Failed to download audio for {VideoId}: {Error}", 
                            video.TiktokVideoId, ex.Message);
                        throw new Exception($"Audio download failed: {ex.Message}", ex);
                    }
                }
                else if (requiresAudio)
                {
                    Log.Information("✓ Step 1/4: Audio already downloaded, skipping");
                    // Check if audio file exists for transcription
                    audioPath = Path.Combine(_config.TempDirectory, $"{video.TiktokVideoId}.mp3");
                    if (!video.TranscriptionCompleted && !File.Exists(audioPath))
                    {
                        // Need to re-download for transcription
                        Log.Warning("Audio file missing, re-downloading for transcription");
                        await _audioDownloadService.DownloadAudioAsync(video.VideoUrl, audioPath);
                        needsAudioFile = true;
                    }
                }
                else
                {
                    Log.Information("✓ Step 1/4: Audio not required (skip_transcription=true)");
                }

                // Step 2: Extract comments (if not already done)
                if (!video.CommentsExtracted)
                {
                    try
                    {
                        // If comments already exist in DB (e.g., restored from backup), reuse them
                        var existingComments = await GetCommentsByVideoAsync(video);
                        
                        if (existingComments.Count > 0)
                        {
                            Log.Information("✓ Found {Count} existing comments in database for {VideoId}, skipping extraction", 
                                existingComments.Count, video.TiktokVideoId);
                            video.CommentsExtracted = true;
                            video.ErrorMessage = null;
                            await UpdateVideoAsync(video);
                        }
                        else
                        {
                        Log.Information("Step 2/4: Extracting comments for {VideoId}", video.TiktokVideoId);
                        video.SetStatus(VideoStatus.ExtractingComments);
                        await UpdateVideoAsync(video);
                        
                        var comments = await _commentsService.ExtractCommentsAsync(video.VideoUrl, _config.CommentsExtractionLimit);
                        Log.Information("✓ Extracted {Count} comments for {VideoId}", comments.Count, video.TiktokVideoId);

                        // Save comments to database
                        int savedCount = 0;
                        foreach (var comment in comments)
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(video.Id))
                                {
                                    throw new InvalidOperationException("Video Id is null or empty, cannot save comments");
                                }
                                
                                var commentRecord = new CommentRecord
                                {
                                    VideoId = video.Id,
                                    CommentId = comment.CommentId,
                                    Text = comment.Text,
                                    Author = comment.Author,
                                    LikeCount = comment.LikeCount,
                                    Timestamp = comment.Timestamp.HasValue 
                                        ? DateTimeOffset.FromUnixTimeSeconds(comment.Timestamp.Value).DateTime 
                                        : (DateTime?)null
                                };
                                await _commentRepo.CreateAsync(commentRecord);
                                savedCount++;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to save comment from {Author}: {Error}", 
                                    comment.Author, ex.Message);
                            }
                        }
                        
                        Log.Information("✓ Saved {SavedCount}/{TotalCount} comments to database", 
                            savedCount, comments.Count);
                        
                        // Mark comments as extracted only when at least one comment was saved
                        if (savedCount > 0)
                        {
                            video.CommentsExtracted = true;
                            video.ErrorMessage = null;
                        }
                        else
                        {
                            video.CommentsExtracted = false;
                            video.ErrorMessage = "No comments found/saved for this video";
                            Log.Warning("No comments were saved for {VideoId}; comments_extracted remains false", video.TiktokVideoId);
                        }
                        await UpdateVideoAsync(video);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "✗ Failed to extract comments for {VideoId}: {Error}", 
                            video.TiktokVideoId, ex.Message);
                        // Keep as not extracted when extraction fails
                        video.CommentsExtracted = false;
                        video.ErrorMessage = $"Comments extraction failed: {ex.Message}";
                        await UpdateVideoAsync(video);
                    }
                }
                else
                {
                    Log.Information("✓ Step 2/4: Comments already extracted, skipping");
                }

                // Step 3: Transcribe audio (if not already done and not skipped by config)
                if (!video.TranscriptionCompleted)
                {
                    if (_config.SkipTranscription)
                    {
                        Log.Information("✓ Step 3/4: Transcription skipped by configuration");
                        // NO marcar como completado - se saltó explícitamente
                        video.Transcription = string.Empty;
                        video.TranscriptionCompleted = false;
                        await UpdateVideoAsync(video);
                    }
                    else
                    {
                        try
                        {
                            Log.Information("Step 3/4: Transcribing audio for {VideoId}", video.TiktokVideoId);
                            video.SetStatus(VideoStatus.Transcribing);
                            await UpdateVideoAsync(video);
                            
                            // Ensure audio file exists
                            if (string.IsNullOrEmpty(audioPath))
                            {
                                audioPath = Path.Combine(_config.TempDirectory, $"{video.TiktokVideoId}.mp3");
                            }
                            
                            if (!File.Exists(audioPath))
                            {
                                Log.Warning("Audio file not found, re-downloading for transcription");
                                await _audioDownloadService.DownloadAudioAsync(video.VideoUrl, audioPath);
                                needsAudioFile = true;
                            }
                            
                            var transcription = await _transcriptionService.TranscribeAsync(audioPath);
                            
                            if (string.IsNullOrWhiteSpace(transcription))
                            {
                                Log.Warning("Transcription is empty for {VideoId}", video.TiktokVideoId);
                                transcription = "[No transcription available]";
                            }
                            
                            Log.Information("✓ Transcription completed for {VideoId}: {Length} characters", 
                                video.TiktokVideoId, transcription.Length);

                            // Store transcription in video record
                            video.Transcription = transcription;
                            video.TranscriptionCompleted = true;
                            await UpdateVideoAsync(video);
                            Log.Debug("✓ Transcription saved to database for {VideoId}", video.TiktokVideoId);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "✗ Failed to transcribe audio for {VideoId}: {Error}", 
                                video.TiktokVideoId, ex.Message);
                            // Mark as completed anyway to avoid infinite retry
                            video.Transcription = "[Transcription failed]";
                            video.TranscriptionCompleted = true;
                            video.ErrorMessage = $"Transcription failed: {ex.Message}";
                            await UpdateVideoAsync(video);
                        }
                    }
                }
                else
                {
                    Log.Information("✓ Step 3/4: Transcription already completed, skipping");
                }

                // Step 4: Generate response (if not already done)
                // Validación inteligente: 
                // - Si skip_transcription = true: solo requiere comentarios extraídos
                // - Si skip_transcription = false: requiere transcripción completada
                bool canGenerateResponse = false;
                
                if (_config.SkipTranscription)
                {
                    // Modo sin transcripción: solo necesita comentarios
                    canGenerateResponse = video.CommentsExtracted;
                    if (!canGenerateResponse)
                    {
                        Log.Debug("Cannot generate response yet: comments not extracted (skip_transcription=true)");
                    }
                }
                else
                {
                    // Modo con transcripción: necesita transcripción completada
                    canGenerateResponse = video.TranscriptionCompleted;
                    if (!canGenerateResponse)
                    {
                        Log.Debug("Cannot generate response yet: transcription not completed (skip_transcription=false)");
                    }
                }

                if (!video.ResponseGenerated && canGenerateResponse)
                {
                    try
                    {
                        Log.Information("Step 4/4: Generating response for {VideoId}", video.TiktokVideoId);
                        Log.Information("=== RESPONSE GENERATION START ===");
                        Log.Information("Video ID: {VideoId}", video.Id);
                        Log.Information("TikTok Video ID: {TiktokVideoId}", video.TiktokVideoId);
                        Log.Information("Response Generated Flag: {Flag}", video.ResponseGenerated);
                        Log.Information("Can Generate Response: {CanGenerate}", canGenerateResponse);
                        
                        video.SetStatus(VideoStatus.GeneratingResponse);
                        await UpdateVideoAsync(video);
                        
                        // Get comments from database
                        var commentRecords = await GetCommentsByVideoAsync(video);
                        Log.Information("Retrieved {Count} comments from database for response generation", commentRecords.Count);

                        if (commentRecords.Count == 0 && _config.SkipTranscription && string.IsNullOrWhiteSpace(video.Transcription) && string.IsNullOrWhiteSpace(video.Title))
                        {
                            Log.Warning("No comments/transcription/title available for {VideoId}; resetting comments_extracted=false to trigger re-extraction", video.TiktokVideoId);
                            video.CommentsExtracted = false;
                            video.ErrorMessage = "No input available for response; comments_extracted reset for re-extraction";
                            await UpdateVideoAsync(video);
                            return;
                        }
                        
                        // Convert CommentRecord to CommentData
                        var comments = commentRecords.Select(c => new CommentData
                        {
                            CommentId = c.CommentId,
                            Text = c.Text,
                            Author = c.Author,
                            LikeCount = c.LikeCount,
                            Timestamp = c.Timestamp.HasValue 
                                ? new DateTimeOffset(c.Timestamp.Value, TimeSpan.Zero).ToUnixTimeSeconds() 
                                : (long?)null
                        }).ToList();
                        
                        Log.Information("Calling response generation API...");
                        var responseResult = await _responseService.GenerateResponseAsync(
                            video, video.Transcription, comments);

                        Log.Information("=== RESPONSE GENERATION RESULT ===");
                        Log.Information("Success: {Success}", responseResult.Success);
                        Log.Information("Error Message: {Error}", responseResult.ErrorMessage ?? "None");
                        
                        // Save response if successful
                        if (responseResult.Success)
                        {
                            if (string.IsNullOrEmpty(video.Id))
                            {
                                throw new InvalidOperationException("Video Id is null or empty, cannot save response");
                            }
                            
                            Log.Information("=== SAVING RESPONSE TO DATABASE ===");
                            Log.Information("Video ID: {VideoId}", video.Id);
                            Log.Information("Response Text Length: {Length}", responseResult.ResponseText.Length);
                            Log.Information("Response Text Preview: {Preview}", 
                                responseResult.ResponseText.Length > 100 
                                    ? responseResult.ResponseText.Substring(0, 100) + "..." 
                                    : responseResult.ResponseText);
                            
                            var responseRecord = new ResponseRecord
                            {
                                VideoId = video.Id,
                                ResponseText = responseResult.ResponseText,
                                ApiStatus = "success",
                                Posted = false,
                                PostedAt = DateTime.UtcNow.Date // Solo la fecha, sin hora
                            };
                            
                            Log.Information("Calling CreateResponseAsync...");
                            var savedResponse = await _responseRepo.CreateAsync(responseRecord);
                            Log.Information("✓ Response saved to database with ID: {ResponseId}", savedResponse.Id);
                            Log.Information("✓ Response saved for {VideoId} - Length: {Length} characters", 
                                video.TiktokVideoId, responseResult.ResponseText.Length);
                            
                            // Mark response as generated ONLY if successful
                            Log.Information("Marking video as response_generated=true");
                            video.ResponseGenerated = true;
                            await UpdateVideoAsync(video);
                            Log.Information("✓ Video updated with response_generated=true");
                        }
                        else
                        {
                            Log.Warning("⚠ Response generation failed for {VideoId}: {Error}", 
                                video.TiktokVideoId, responseResult.ErrorMessage ?? "Unknown error");
                            // DO NOT mark as generated - allow retry on next cycle
                            video.ErrorMessage = $"Response generation failed: {responseResult.ErrorMessage ?? "Unknown error"}";
                            await UpdateVideoAsync(video);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "✗ Failed to generate response for {VideoId}: {Error}", 
                            video.TiktokVideoId, ex.Message);
                        // DO NOT mark as generated - allow retry on next cycle
                        video.ErrorMessage = $"Response generation failed: {ex.Message}";
                        await UpdateVideoAsync(video);
                    }
                }
                else if (video.ResponseGenerated)
                {
                    Log.Information("✓ Step 4/4: Response already generated, skipping");
                }
                else if (!canGenerateResponse)
                {
                    Log.Information("⏸ Step 4/4: Cannot generate response yet - Prerequisites not met");
                    Log.Information("  - Comments Extracted: {CommentsExtracted}", video.CommentsExtracted);
                    Log.Information("  - Transcription Completed: {TranscriptionCompleted}", video.TranscriptionCompleted);
                    Log.Information("  - Skip Transcription: {SkipTranscription}", _config.SkipTranscription);
                }

                // Check if all stages are completed
                if (IsFullyCompleted(video))
                {
                    video.SetStatus(VideoStatus.Completed);
                    // Clear error message if all stages completed successfully
                    if (string.IsNullOrEmpty(video.ErrorMessage) || 
                        !video.ErrorMessage.Contains("failed"))
                    {
                        video.ErrorMessage = null;
                    }
                    await UpdateVideoAsync(video);
                    
                    Log.Information("========== ✓ All stages completed for video: {VideoId} ==========", 
                        video.TiktokVideoId);
                }
                else
                {
                    Log.Warning("⚠ Video {VideoId} has incomplete stages", video.TiktokVideoId);
                }

                var duration = DateTime.UtcNow - startTime;

                // Fire event
                VideoProcessed?.Invoke(this, new VideoProcessedEventArgs(video)
                {
                    Success = IsFullyCompleted(video),
                    ProcessingDuration = duration
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Video processing failed for {VideoId} at status {Status}: {Error}", 
                    video.TiktokVideoId, video.Status, ex.Message);
                Log.Error("Stack trace: {StackTrace}", ex.StackTrace);

                // Mark video as failed
                video.SetStatus(VideoStatus.Failed);
                video.ErrorMessage = $"Failed at {video.Status}: {ex.Message}";
                await UpdateVideoAsync(video);

                var duration = DateTime.UtcNow - startTime;

                // Fire event
                VideoProcessed?.Invoke(this, new VideoProcessedEventArgs(video)
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingDuration = duration
                });
            }
            finally
            {
                // Cleanup temporary audio file only if we downloaded it in this run
                if (needsAudioFile && !string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
                {
                    try
                    {
                        File.Delete(audioPath);
                        Log.Debug("✓ Cleaned up temporary audio file: {Path}", audioPath);
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
                throw new InvalidOperationException("Video Id is null or empty, cannot update record");
            }

            await _videoRepo.UpdateAsync(video.Id, video);
        }

        private async Task<List<CommentRecord>> GetCommentsByVideoAsync(VideoRecord video)
        {
            if (string.IsNullOrWhiteSpace(video.Id))
            {
                return new List<CommentRecord>();
            }

            return await _commentRepo.GetByVideoIdAsync(video.Id);
        }
    }
}
