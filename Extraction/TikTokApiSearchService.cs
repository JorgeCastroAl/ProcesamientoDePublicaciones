using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using FluxAnswer.Models;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Searches TikTok videos by keywords using TikTokApi Python script.
    /// Transparent interface: pass username + keywords + maxAgeDays, get videos.
    /// </summary>
    public class TikTokApiSearchService : ITikTokApiSearchService
    {
        private readonly string _pythonPath;
        private readonly string _scriptPath;
        private const int TimeoutSeconds = 180;

        public TikTokApiSearchService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _scriptPath = Path.Combine(baseDir, "Scripts", "tiktok_search.py");

            var prodPython = @"C:\Program Files\TikTokSuite\Tools\Python\python.exe";
            _pythonPath = File.Exists(prodPython) ? prodPython : "python";
        }

        public async Task<List<VideoMetadata>> SearchAsync(
            string username, List<string> keywords, int maxAgeDays,
            int countPerKeyword = 10)
        {
            if (keywords == null || keywords.Count == 0)
                return new List<VideoMetadata>();

            var keywordsArg = string.Join(",", keywords);

            Log.Information(
                "TikTokApi search: username='{Username}' keywords=[{Keywords}] maxAge={MaxAge}d count={Count}",
                username, keywordsArg, maxAgeDays, countPerKeyword);

            var args = $"\"{_scriptPath}\" --username \"{username}\" --keywords \"{keywordsArg}\" --max-age-days {maxAgeDays} --count {countPerKeyword}";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var errorOutput = await process.StandardError.ReadToEndAsync();
            var exited = process.WaitForExit(TimeoutSeconds * 1000);

            if (!exited)
            {
                process.Kill();
                throw new TimeoutException($"TikTokApi script timed out after {TimeoutSeconds}s");
            }

            if (process.ExitCode != 0)
            {
                Log.Error("TikTokApi script failed: exit={ExitCode} stderr={Error}", process.ExitCode, errorOutput);
                throw new InvalidOperationException($"TikTokApi script failed (exit {process.ExitCode}): {errorOutput}");
            }

            if (string.IsNullOrWhiteSpace(output))
                return new List<VideoMetadata>();

            var videos = JsonConvert.DeserializeObject<List<VideoMetadata>>(output);

            Log.Information("TikTokApi search returned {Count} videos for @{Username}", videos?.Count ?? 0, username);

            return videos ?? new List<VideoMetadata>();
        }
    }
}
