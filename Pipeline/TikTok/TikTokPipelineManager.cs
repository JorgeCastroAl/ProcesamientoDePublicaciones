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
using FluxAnswer.Services.Database;
using FluxAnswer.Services.Pipeline;

namespace FluxAnswer.Pipeline.TikTok
{
    /// <summary>
    /// Gestiona 5 hilos independientes, uno por cada etapa del pipeline.
    /// Cada hilo consulta PocketBase por el siguiente video que necesita su etapa específica.
    /// </summary>
    public class TikTokPipelineManager : ITikTokPipelineManager
    {
        private readonly IVideoRepo _videoRepo;
        private readonly IAudioStageService _audioStageService;
        private readonly ICommentsStageService _commentsStageService;
        private readonly ITranscriptionStageService _transcriptionStageService;
        private readonly IVideoResponseStageService _responseStageService;
        private readonly IBotAccountVideoStageService _botAccountVideoStageService;
        private readonly IConfigurationManager _config;

        private CancellationTokenSource? _cts;
        private readonly List<Task> _workers = new();
        private bool _isRunning;
        private readonly object _lock = new();
        private DateTime? _lastProcessedTime;

        public event EventHandler<TikTokVideoProcessedEventArgs>? ItemProcessed;
        public event EventHandler<TikTokVideoProcessedEventArgs>? VideoProcessed;

        public TikTokPipelineManager(
            IVideoRepo videoRepo,
            IAudioStageService audioStageService,
            ICommentsStageService commentsStageService,
            ITranscriptionStageService transcriptionStageService,
            IVideoResponseStageService responseStageService,
            IBotAccountVideoStageService botAccountVideoStageService,
            IConfigurationManager config)
        {
            _videoRepo = videoRepo;
            _audioStageService = audioStageService;
            _commentsStageService = commentsStageService;
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
                    Log.Warning("Pipeline manager is already running");
                    return Task.CompletedTask;
                }

                _isRunning = true;
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;

                _workers.Clear();
                _workers.Add(Task.Run(() => StageLoopAsync("Audio", _videoRepo.GetNextForAudioAsync, ProcessAudioAsync, ct)));
                _workers.Add(Task.Run(() => StageLoopAsync("Comments", _videoRepo.GetNextForCommentsAsync, ProcessCommentsAsync, ct)));
                _workers.Add(Task.Run(() => StageLoopAsync("Transcription", _videoRepo.GetNextForTranscriptionAsync, ProcessTranscriptionAsync, ct)));
                _workers.Add(Task.Run(() => StageLoopAsync("Response", _videoRepo.GetNextForResponseAsync, ProcessResponseAsync, ct)));
                _workers.Add(Task.Run(() => StageLoopAsync("CustomComments", _videoRepo.GetNextForCustomCommentsAsync, ProcessCustomCommentsAsync, ct)));

                Log.Information("Pipeline manager started: 5 stage workers running");
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    Log.Warning("Pipeline manager is not running");
                    return;
                }

                _isRunning = false;
                _cts?.Cancel();
            }

            await Task.WhenAll(_workers);
            _workers.Clear();
            Log.Information("Pipeline manager stopped");
        }

        public async Task<TikTokPipelineStatistics> GetStatisticsAsync()
        {
            try
            {
                var allVideos = await _videoRepo.GetAllAsync();
                return new TikTokPipelineStatistics
                {
                    PendingCount = allVideos.Count(v => v.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)),
                    ProcessingCount = allVideos.Count(v => v.Status.Equals("processing", StringComparison.OrdinalIgnoreCase)),
                    CompletedCount = allVideos.Count(v => v.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)),
                    FailedCount = allVideos.Count(v => v.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
                    LastProcessedTime = _lastProcessedTime
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting pipeline statistics");
                return new TikTokPipelineStatistics { LastProcessedTime = _lastProcessedTime, LastError = ex.Message };
            }
        }

        // ── Loop genérico por etapa ──

        private async Task StageLoopAsync(
            string stageName,
            Func<Task<VideoRecord?>> queryNext,
            Func<VideoRecord, Task> process,
            CancellationToken ct)
        {
            var pollSeconds = _config.ProcessingPollIntervalSeconds;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var video = await queryNext();

                    if (video == null)
                    {
                        const int idleSeconds = 180; // 3 minutes
                        Log.Debug("[{Stage}] No videos pending, sleeping {Seconds}s...", stageName, idleSeconds);
                        await Task.Delay(idleSeconds * 1000, ct);
                        continue;
                    }

                    Log.Information("[{Stage}] Processing video: {VideoId}", stageName, video.TiktokVideoId);
                    await process(video);
                    _lastProcessedTime = DateTime.UtcNow;
                }
                catch (OperationCanceledException)
                {
                    Log.Information("[{Stage}] Worker cancelled", stageName);
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[{Stage}] Worker error, restarting in 10s...", stageName);
                    await Task.Delay(10000, ct);
                }
            }
        }

        // ── Procesadores por etapa ──

        private async Task ProcessAudioAsync(VideoRecord video)
        {
            var audioResult = await _audioStageService.ProcessAsync(video, requiresAudio: true);

            // Limpiar archivo temporal si se descargó
            if (audioResult.NeedsCleanup && !string.IsNullOrEmpty(audioResult.AudioPath) && File.Exists(audioResult.AudioPath))
            {
                // No borrar aquí: el hilo de transcripción lo necesita.
                // Se borrará después de transcribir.
            }

            FireVideoProcessed(video, true);
        }

        private async Task ProcessCommentsAsync(VideoRecord video)
        {
            await _commentsStageService.ProcessAsync(video);
            FireVideoProcessed(video, true);
        }

        private async Task ProcessTranscriptionAsync(VideoRecord video)
        {
            string? audioPath = null;
            try
            {
                await _transcriptionStageService.ProcessAsync(video, video.SkipTranscription, audioPath);
            }
            finally
            {
                // Limpiar archivo de audio temporal después de transcribir
                var tempPath = Path.Combine(_config.TempDirectory, $"{video.TiktokVideoId}.mp3");
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); Log.Debug("Cleaned up audio: {Path}", tempPath); }
                    catch (Exception ex) { Log.Warning(ex, "Failed to delete audio: {Path}", tempPath); }
                }
            }

            FireVideoProcessed(video, true);
        }

        private async Task ProcessResponseAsync(VideoRecord video)
        {
            await _responseStageService.ProcessAsync(video);
            FireVideoProcessed(video, video.ResponseGenerated);
        }

        private async Task ProcessCustomCommentsAsync(VideoRecord video)
        {
            await _botAccountVideoStageService.ProcessAsync(video);

            // Marcar como completado si custom comments terminaron
            if (video.CustomCommentsSuccess)
            {
                video.SetStatus(VideoStatus.Completed);
                video.ErrorMessage = null;
                await UpdateVideoAsync(video);
                Log.Information("[OK] Video {VideoId} fully completed", video.TiktokVideoId);
            }

            FireVideoProcessed(video, video.CustomCommentsSuccess);
        }

        // ── Helpers ──

        private void FireVideoProcessed(VideoRecord video, bool success)
        {
            var args = new TikTokVideoProcessedEventArgs(video) { Success = success };
            ItemProcessed?.Invoke(this, args);
            VideoProcessed?.Invoke(this, args);
        }

        private async Task UpdateVideoAsync(VideoRecord video)
        {
            if (string.IsNullOrWhiteSpace(video.Id))
                throw new InvalidOperationException("Video Id is null or empty");
            await _videoRepo.UpdateAsync(video.Id, video);
        }
    }
}
