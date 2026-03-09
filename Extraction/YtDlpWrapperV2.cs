using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using FluxAnswer.Models;
using FluxAnswer.Services.Media;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Wrapper mejorado para yt-dlp con soporte completo de comentarios.
    /// Incluye manejo de cookies, lÃ­mites de comentarios y timeouts extendidos.
    /// </summary>
    public class YtDlpWrapperV2 : IYtDlpWrapper
    {
        private readonly string _ytDlpPath;
        private readonly int _timeoutSeconds;
        private readonly bool _enableComments;
        private readonly int _maxComments;
        private readonly string? _browserForCookies;

        public YtDlpWrapperV2(
            string ytDlpPath = "yt-dlp", 
            int timeoutSeconds = 180, 
            bool enableComments = true,
            int maxComments = 100,
            string? browserForCookies = "chrome")
        {
            _ytDlpPath = ExternalToolLocator.ResolveYtDlp(ytDlpPath);
            Log.Information("YtDlpWrapperV2 configured yt-dlp path: {Path}", _ytDlpPath);
            
            _timeoutSeconds = timeoutSeconds;
            _enableComments = enableComments;
            _maxComments = maxComments;
            _browserForCookies = browserForCookies;
        }

        /// <summary>
        /// Builds the yt-dlp arguments string with comment support.
        /// </summary>
        private string BuildArguments(string url, int count, bool includeComments)
        {
            var args = new List<string>
            {
                "--dump-json",
                "--flat-playlist",
                $"--playlist-end {count}"
            };

            if (includeComments && _enableComments)
            {
                // Enable comment extraction
                args.Add("--write-comments");
                
                // Detect platform and use appropriate extractor args
                string platform = "youtube"; // default
                if (url.Contains("tiktok.com"))
                {
                    platform = "tiktok";
                }
                else if (url.Contains("youtube.com") || url.Contains("youtu.be"))
                {
                    platform = "youtube";
                }
                
                // Limit number of comments to avoid timeouts
                args.Add($"--extractor-args \"{platform}:comment_sort=top;max_comments={_maxComments}\"");
                
                // Use browser cookies to avoid bot detection
                if (!string.IsNullOrEmpty(_browserForCookies))
                {
                    args.Add($"--cookies-from-browser {_browserForCookies}");
                }
            }

            args.Add($"\"{url}\"");
            
            return string.Join(" ", args);
        }

        /// <summary>
        /// Extracts video metadata from a TikTok profile URL.
        /// </summary>
        public async Task<List<VideoMetadata>> ExtractVideosAsync(string profileUrl, int count)
        {
            var arguments = BuildArguments(profileUrl, count, includeComments: false);

            Log.Information("Executing yt-dlp: {Arguments}", arguments);

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

                var exited = process.WaitForExit(_timeoutSeconds * 1000);

                if (!exited)
                {
                    process.Kill();
                    throw new TimeoutException($"yt-dlp process timed out after {_timeoutSeconds} seconds");
                }

                if (process.ExitCode != 0)
                {
                    Log.Error("yt-dlp failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                    throw new InvalidOperationException($"yt-dlp failed with exit code {process.ExitCode}: {errorOutput}");
                }

                // Parse JSON output (one JSON object per line)
                var videos = new List<VideoMetadata>();
                var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    try
                    {
                        var video = JsonConvert.DeserializeObject<VideoMetadata>(line);
                        if (video != null)
                        {
                            videos.Add(video);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log.Warning(ex, "Failed to parse yt-dlp JSON line: {Line}", line);
                    }
                }

                Log.Information("yt-dlp extracted {Count} videos from {Url}", videos.Count, profileUrl);
                return videos;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing yt-dlp for {Url}", profileUrl);
                throw;
            }
        }

        /// <summary>
        /// Extracts comments from a specific video URL.
        /// Incluye manejo de cookies y lÃ­mites para evitar bloqueos.
        /// </summary>
        public async Task<List<CommentData>> ExtractCommentsAsync(string videoUrl)
        {
            if (!_enableComments)
            {
                Log.Warning("Comment extraction is disabled");
                return new List<CommentData>();
            }

            var arguments = BuildArguments(videoUrl, 1, includeComments: true);

            Log.Information("Extracting comments with yt-dlp: {Arguments}", arguments);

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

                var exited = process.WaitForExit(_timeoutSeconds * 1000);

                if (!exited)
                {
                    process.Kill();
                    Log.Warning("Comment extraction timed out after {Timeout} seconds for {Url}", _timeoutSeconds, videoUrl);
                    return new List<CommentData>();
                }

                if (process.ExitCode != 0)
                {
                    Log.Error("yt-dlp comment extraction failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                    return new List<CommentData>();
                }

                // Parse JSON output to extract comments
                var comments = new List<CommentData>();
                var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    try
                    {
                        var json = JsonConvert.DeserializeObject<dynamic>(line);
                        
                        // Comments are in the "comments" field
                        if (json?.comments != null)
                        {
                            foreach (var comment in json.comments)
                            {
                                var commentData = new CommentData
                                {
                                    CommentId = comment.id?.ToString() ?? string.Empty,
                                    Text = comment.text?.ToString() ?? string.Empty,
                                    Author = comment.author?.ToString() ?? string.Empty,
                                    LikeCount = comment.like_count != null ? (int?)comment.like_count : null,
                                    Timestamp = comment.timestamp != null ? (long?)comment.timestamp : null
                                };
                                
                                comments.Add(commentData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to parse comment from JSON line");
                    }
                }

                Log.Information("Extracted {Count} comments from {Url}", comments.Count, videoUrl);
                return comments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting comments from {Url}", videoUrl);
                return new List<CommentData>();
            }
        }

        /// <summary>
        /// Validates that yt-dlp is available and executable.
        /// </summary>
        public async Task<bool> ValidateAvailabilityAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ytDlpPath,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    Log.Information("yt-dlp is available: version {Version}", output.Trim());
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "yt-dlp is not available at: {Path}", _ytDlpPath);
                return false;
            }
        }
    }
}

