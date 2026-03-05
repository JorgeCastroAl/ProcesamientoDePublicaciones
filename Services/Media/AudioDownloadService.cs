using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace FluxAnswer.Services.Media
{
    /// <summary>
    /// Service for downloading audio from videos using yt-dlp.
    /// </summary>
    public class AudioDownloadService : IAudioDownloadService
    {
        private readonly string _ytDlpPath;
        private readonly int _timeoutMinutes;
        private readonly int _maxRetries;
        private readonly int _retryDelaySeconds;

        public AudioDownloadService(
            string ytDlpPath = "yt-dlp",
            int timeoutMinutes = 5,
            int maxRetries = 3,
            int retryDelaySeconds = 5)
        {
            if (ytDlpPath == "yt-dlp")
            {
                _ytDlpPath = FindYtDlpExecutable();
            }
            else
            {
                _ytDlpPath = ytDlpPath;
            }

            _timeoutMinutes = timeoutMinutes;
            _maxRetries = maxRetries;
            _retryDelaySeconds = retryDelaySeconds;
        }

        private string FindYtDlpExecutable()
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TikTokManager", "yt-dlp.exe"),
                Path.Combine(Environment.CurrentDirectory, "yt-dlp.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe"),
                "yt-dlp.exe",
                "yt-dlp"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Log.Information("Found yt-dlp at: {Path}", path);
                    return path;
                }
            }

            Log.Warning("yt-dlp executable not found in common locations, using default path");
            return "yt-dlp";
        }

        private string FindFfmpegExecutable()
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TikTokManager", "ffmpeg.exe"),
                Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                "ffmpeg.exe",
                "ffmpeg"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Log.Information("Found ffmpeg at: {Path}", path);
                    return Path.GetDirectoryName(path) ?? path;
                }
            }

            Log.Warning("ffmpeg executable not found in common locations, using default path");
            return "ffmpeg";
        }

        public async Task<string> DownloadAudioAsync(string videoUrl, string outputPath)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    Log.Information(
                        "Downloading audio (attempt {Attempt}/{MaxRetries}): {Url}",
                        attempt,
                        _maxRetries,
                        videoUrl
                    );

                    var result = await DownloadAudioInternalAsync(videoUrl, outputPath);

                    if (attempt > 1)
                    {
                        Log.Information("Audio download succeeded on attempt {Attempt}", attempt);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt < _maxRetries)
                    {
                        Log.Warning(
                            ex,
                            "Audio download failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                            attempt,
                            _maxRetries,
                            _retryDelaySeconds
                        );

                        await Task.Delay(_retryDelaySeconds * 1000);
                    }
                }
            }

            Log.Error(
                lastException,
                "Audio download failed after {MaxRetries} attempts: {Url}",
                _maxRetries,
                videoUrl
            );

            throw new InvalidOperationException(
                $"Audio download failed after {_maxRetries} attempts",
                lastException
            );
        }

        private async Task<string> DownloadAudioInternalAsync(string videoUrl, string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var ffmpegPath = FindFfmpegExecutable();
            var arguments = $"-x --audio-format mp3 --audio-quality 192K --ffmpeg-location \"{ffmpegPath}\" -o \"{outputPath}\" \"{videoUrl}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();

                var exited = process.WaitForExit(_timeoutMinutes * 60 * 1000);

                if (!exited)
                {
                    process.Kill();
                    throw new TimeoutException($"Audio download timed out after {_timeoutMinutes} minutes");
                }

                if (process.ExitCode != 0)
                {
                    Log.Error("yt-dlp audio download failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                    throw new InvalidOperationException($"yt-dlp failed with exit code {process.ExitCode}: {errorOutput}");
                }

                if (!File.Exists(outputPath))
                {
                    throw new FileNotFoundException($"Audio file was not created at: {outputPath}");
                }

                Log.Information("Audio downloaded successfully: {Path}", outputPath);
                return outputPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error downloading audio from {Url}", videoUrl);
                throw;
            }
        }
    }
}
