using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Service for extracting comments from videos using yt-dlp.
    /// </summary>
    public class CommentsExtractionService : ICommentsExtractionService
    {
        private readonly string _ytDlpPath;
        private readonly int _timeoutSeconds;

        public CommentsExtractionService(string ytDlpPath = "yt-dlp", int timeoutSeconds = 30)
        {
            // Try to find yt-dlp in common locations if default path is used
            if (ytDlpPath == "yt-dlp")
            {
                _ytDlpPath = FindYtDlpExecutable();
            }
            else
            {
                _ytDlpPath = ytDlpPath;
            }
            
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Finds the yt-dlp executable in common locations.
        /// </summary>
        private string FindYtDlpExecutable()
        {
            // Check common locations
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TikTokManager", "yt-dlp.exe"),
                Path.Combine(Environment.CurrentDirectory, "yt-dlp.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe"),
                "yt-dlp.exe", // Try PATH
                "yt-dlp"      // Try PATH (Linux/Mac)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Log.Information("Found yt-dlp at: {Path}", path);
                    return path;
                }
            }

            // Default to PATH
            Log.Warning("yt-dlp executable not found in common locations, using default path");
            return "yt-dlp";
        }

        public async Task<List<CommentData>> ExtractCommentsAsync(string videoUrl, int limit = 12)
        {
            try
            {
                Log.Information("Extracting up to {Limit} comments from: {Url}", limit, videoUrl);
                return await ExtractCommentsWithYtDlpAsync(videoUrl, limit);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting comments from {Url}", videoUrl);
                return new List<CommentData>();
            }
        }

        private async Task<List<CommentData>> ExtractCommentsWithYtDlpAsync(string videoUrl, int limit)
        {
            string? infoJsonPath = null;

            try
            {
                // Generate unique filename for .info.json
                var tempDir = Path.GetTempPath();
                var uniqueId = Guid.NewGuid().ToString("N");
                var outputTemplate = Path.Combine(tempDir, $"comments_{uniqueId}");
                infoJsonPath = $"{outputTemplate}.info.json";

                // Note: --max-comments is not available in all yt-dlp versions
                // We'll get all comments and limit them in code
                var arguments = $"--write-comments --write-info-json --skip-download -o \"{outputTemplate}\" \"{videoUrl}\"";

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

                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();

                var exited = process.WaitForExit(_timeoutSeconds * 1000);

                if (!exited)
                {
                    process.Kill();
                    Log.Warning("Comments extraction timed out after {Timeout}s for {Url}", _timeoutSeconds, videoUrl);
                    return new List<CommentData>();
                }

                if (process.ExitCode != 0)
                {
                    Log.Warning("yt-dlp comments extraction failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                    return new List<CommentData>();
                }

                // Check if .info.json file was created
                if (!File.Exists(infoJsonPath))
                {
                    Log.Warning("Comments info.json file not found at: {Path}", infoJsonPath);
                    return new List<CommentData>();
                }

                // Parse the .info.json file
                var jsonContent = await File.ReadAllTextAsync(infoJsonPath);
                var jsonObject = JObject.Parse(jsonContent);

                var comments = new List<CommentData>();

                // Extract comments array
                var commentsArray = jsonObject["comments"] as JArray;
                if (commentsArray == null || commentsArray.Count == 0)
                {
                    Log.Information("No comments found in video: {Url}", videoUrl);
                    return comments;
                }

                // Parse up to 'limit' comments
                foreach (var commentToken in commentsArray.Take(limit))
                {
                    try
                    {
                        var comment = commentToken.ToObject<CommentData>();
                        if (comment != null && !string.IsNullOrEmpty(comment.Text))
                        {
                            comments.Add(comment);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to parse comment from JSON");
                    }
                }

                Log.Information("Extracted {Count} comments from {Url}", comments.Count, videoUrl);
                return comments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting comments with yt-dlp from {Url}", videoUrl);
                return new List<CommentData>();
            }
            finally
            {
                // Clean up temporary .info.json file
                if (!string.IsNullOrEmpty(infoJsonPath) && File.Exists(infoJsonPath))
                {
                    try
                    {
                        File.Delete(infoJsonPath);
                        Log.Debug("Cleaned up temporary file: {Path}", infoJsonPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to delete temporary file: {Path}", infoJsonPath);
                    }
                }
            }
        }
    }
}
