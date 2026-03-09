using System;
using System.IO;

namespace FluxAnswer.Services.Media
{
    internal static class ExternalToolLocator
    {
        public static string ResolveYtDlp(string configuredPath = "yt-dlp")
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && configuredPath != "yt-dlp")
            {
                return configuredPath;
            }

            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TikTokSuite", "Tools", "YtDlp", "yt-dlp.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TikTokManager", "yt-dlp.exe"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Tools", "YtDlp", "yt-dlp.exe")),
                Path.Combine(Environment.CurrentDirectory, "yt-dlp.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe"),
                "yt-dlp.exe",
                "yt-dlp"
            };

            return FindFirstExistingOrDefault(possiblePaths, "yt-dlp");
        }

        public static string ResolveFfmpegLocation(string configuredPath = "ffmpeg.exe")
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && configuredPath != "ffmpeg.exe")
            {
                return configuredPath;
            }

            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TikTokSuite", "Tools", "FFmpeg", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TikTokManager", "ffmpeg.exe"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Tools", "FFmpeg", "ffmpeg.exe")),
                Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                "ffmpeg.exe",
                "ffmpeg"
            };

            var resolved = FindFirstExistingOrDefault(possiblePaths, "ffmpeg");

            if (resolved.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(resolved) ?? resolved;
            }

            return resolved;
        }

        private static string FindFirstExistingOrDefault(string[] candidates, string fallback)
        {
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }
    }
}