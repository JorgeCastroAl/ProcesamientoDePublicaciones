using System;
using System.IO;
using System.Threading.Tasks;
using FluxAnswer.Configuration;
using FluxAnswer.Models;
using FluxAnswer.Repositories;
using FluxAnswer.Services.AI;
using FluxAnswer.Services.Media;
using Serilog;

namespace FluxAnswer.Services.Pipeline
{
    public class TranscriptionStageService : ITranscriptionStageService
    {
        private readonly IAudioDownloadService _audioDownloadService;
        private readonly ITranscriptionService _transcriptionService;
        private readonly IVideoRepo _videoRepo;
        private readonly IConfigurationManager _config;

        public TranscriptionStageService(
            IAudioDownloadService audioDownloadService,
            ITranscriptionService transcriptionService,
            IVideoRepo videoRepo,
            IConfigurationManager config)
        {
            _audioDownloadService = audioDownloadService;
            _transcriptionService = transcriptionService;
            _videoRepo = videoRepo;
            _config = config;
        }

        public async Task<bool> ProcessAsync(VideoRecord video, bool skipTranscription, string? audioPath)
        {
            if (video.TranscriptionCompleted)
            {
                Log.Information("[OK] Step 3/4: Transcription already completed, skipping");
                return false;
            }

            if (skipTranscription)
            {
                Log.Information("[OK] Step 3/4: Transcription skipped by configuration");
                video.Transcription = string.Empty;
                video.TranscriptionCompleted = false;
                await UpdateVideoAsync(video);
                return false;
            }

            var downloadedInThisStage = false;

            try
            {
                Log.Information("Step 3/4: Transcribing audio for {VideoId}", video.TiktokVideoId);
                video.SetStatus(VideoStatus.Transcribing);
                await UpdateVideoAsync(video);

                if (string.IsNullOrEmpty(audioPath))
                {
                    audioPath = Path.Combine(_config.TempDirectory, $"{video.TiktokVideoId}.mp3");
                }

                if (!File.Exists(audioPath))
                {
                    Log.Warning("Audio file not found, re-downloading for transcription");
                    await _audioDownloadService.DownloadAudioAsync(video.VideoUrl, audioPath);
                    downloadedInThisStage = true;
                }

                var transcription = await _transcriptionService.TranscribeAsync(audioPath);
                if (string.IsNullOrWhiteSpace(transcription))
                {
                    Log.Warning("Transcription is empty for {VideoId}", video.TiktokVideoId);
                    transcription = "[No transcription available]";
                }

                Log.Information("[OK] Transcription completed for {VideoId}: {Length} characters",
                    video.TiktokVideoId, transcription.Length);

                video.Transcription = transcription;
                video.TranscriptionCompleted = true;
                await UpdateVideoAsync(video);
                Log.Debug("[OK] Transcription saved to database for {VideoId}", video.TiktokVideoId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ERROR] Failed to transcribe audio for {VideoId}: {Error}",
                    video.TiktokVideoId, ex.Message);
                video.Transcription = "[Transcription failed]";
                video.TranscriptionCompleted = true;
                video.ErrorMessage = $"Transcription failed: {ex.Message}";
                await UpdateVideoAsync(video);
            }

            return downloadedInThisStage;
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
