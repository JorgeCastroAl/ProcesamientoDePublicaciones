using System;
using System.IO;
using System.Threading.Tasks;
using FluxAnswer.Configuration;
using FluxAnswer.Models;
using FluxAnswer.Repositories;
using FluxAnswer.Services.Media;
using Serilog;

namespace FluxAnswer.Services.Pipeline
{
    public class AudioStageService : IAudioStageService
    {
        private readonly IAudioDownloadService _audioDownloadService;
        private readonly IVideoRepo _videoRepo;
        private readonly IConfigurationManager _config;

        public AudioStageService(
            IAudioDownloadService audioDownloadService,
            IVideoRepo videoRepo,
            IConfigurationManager config)
        {
            _audioDownloadService = audioDownloadService;
            _videoRepo = videoRepo;
            _config = config;
        }

        public async Task<(string? AudioPath, bool NeedsCleanup)> ProcessAsync(VideoRecord video, bool requiresAudio)
        {
            if (!requiresAudio)
            {
                Log.Information("[OK] Step 1/4: Audio not required (skip_transcription=true)");
                return (null, false);
            }

            var audioPath = Path.Combine(_config.TempDirectory, $"{video.TiktokVideoId}.mp3");

            if (!video.AudioDownloaded)
            {
                Log.Information("Step 1/4: Downloading audio for {VideoId}", video.TiktokVideoId);
                video.SetStatus(VideoStatus.DownloadingAudio);
                await UpdateVideoAsync(video);

                await _audioDownloadService.DownloadAudioAsync(video.VideoUrl, audioPath);

                if (!File.Exists(audioPath))
                {
                    throw new FileNotFoundException($"Audio file not created: {audioPath}");
                }

                var fileInfo = new FileInfo(audioPath);
                Log.Information("[OK] Audio downloaded for {VideoId} - Size: {Size} bytes",
                    video.TiktokVideoId, fileInfo.Length);

                video.AudioDownloaded = true;
                await UpdateVideoAsync(video);
                return (audioPath, true);
            }

            Log.Information("[OK] Step 1/4: Audio already downloaded, skipping");

            if (!video.TranscriptionCompleted && !File.Exists(audioPath))
            {
                Log.Warning("Audio file missing, re-downloading for transcription");
                await _audioDownloadService.DownloadAudioAsync(video.VideoUrl, audioPath);
                return (audioPath, true);
            }

            return (audioPath, false);
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
