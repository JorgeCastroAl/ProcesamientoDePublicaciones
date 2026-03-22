using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluxAnswer.Models;
using FluxAnswer.Repositories;
using Newtonsoft.Json.Linq;
using Serilog;

namespace FluxAnswer.Services.Scraping.TikTok
{
    public interface ITikTokWebCommentsExtractionService
    {
        Task<List<CommentData>> ExtractCommentsAsync(string videoUrl, int limit = 12);
    }

    /// <summary>
    /// Extracts TikTok comments via the public TikTok API (no cookies, no Playwright).
    /// Restored from previous production build.
    /// </summary>
    public class TikTokWebCommentsExtractionService : ITikTokWebCommentsExtractionService, ICommentsExtractionService
    {
        private readonly HttpClient _httpClient;
        private readonly IExtractedCommentRepo? _commentRepo;

        public TikTokWebCommentsExtractionService(IExtractedCommentRepo? commentRepo = null)
        {
            _commentRepo = commentRepo;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.tiktok.com/");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.tiktok.com");

            Log.Information("[Comments][Init] TikTokWebCommentsExtractionService created. Repo={RepoAvailable}", commentRepo != null);
        }

        public async Task<List<CommentData>> ExtractCommentsAsync(string videoUrl, int limit = 12)
        {
            var result = new List<CommentData>();
            try
            {
                var awemeId = TryExtractVideoId(videoUrl);
                if (string.IsNullOrWhiteSpace(awemeId))
                {
                    Log.Warning("[Comments] Could not parse video id from URL: {Url}", videoUrl);
                    return result;
                }

                Log.Information("[Comments][Start] Extracting comments via TikTok API for VideoId={VideoId} (limit={Limit})", awemeId, limit);

                long cursor = 0;
                bool hasMore = true;
                int requestedCount = Math.Min(Math.Max(limit, 1), 50);

                while (hasMore && result.Count < limit)
                {
                    var requestUri = $"https://www.tiktok.com/api/comment/list/?aid=1988&aweme_id={awemeId}&count={requestedCount}&cursor={cursor}";
                    Log.Debug("[Comments] API request: cursor={Cursor}, count={Count}", cursor, requestedCount);

                    var response = await _httpClient.GetAsync(requestUri);
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Warning("[Comments] API request failed. Status={StatusCode}, VideoId={VideoId}, Body={Body}",
                            (int)response.StatusCode, awemeId, Truncate(body, 500));
                        break;
                    }

                    var json = JObject.Parse(body);
                    var commentsArray = json["comments"] as JArray;

                    if (commentsArray == null || commentsArray.Count == 0)
                    {
                        var keys = string.Join(",", json.Properties().Select(p => p.Name).Take(20));
                        Log.Information("[Comments] API returned no comments. VideoId={VideoId}, has_more={HasMore}, cursor={Cursor}, keys={Keys}",
                            awemeId, json["has_more"]?.ToString() ?? "<null>", json["cursor"]?.ToString() ?? "<null>", keys);
                        break;
                    }

                    foreach (var item in commentsArray)
                    {
                        var text = item["text"]?.ToString();
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        result.Add(new CommentData
                        {
                            CommentId = item["cid"]?.ToString() ?? string.Empty,
                            Text = text,
                            Author = item["user"]?["nickname"]?.ToString() ?? string.Empty,
                            LikeCount = TryInt(item["digg_count"]),
                            Timestamp = TryLong(item["create_time"])
                        });

                        if (result.Count >= limit) break;
                    }

                    hasMore = json["has_more"]?.Value<int?>() == 1;
                    cursor = json["cursor"]?.Value<long?>() ?? 0;
                    if (!hasMore || cursor <= 0) break;
                }

                Log.Information("[Comments][Done] Extracted {Count} comments for VideoId={VideoId}", result.Count, awemeId);

                // Log each comment
                for (int i = 0; i < result.Count; i++)
                {
                    var c = result[i];
                    Log.Debug("[Comments][Result {Index}] Author='{Author}', Likes={Likes}, Text='{Text}'",
                        i, c.Author, c.LikeCount, c.Text.Length > 80 ? c.Text.Substring(0, 80) + "..." : c.Text);
                }

                // Persist to DB
                if (_commentRepo != null && result.Count > 0)
                {
                    Log.Information("[Comments][Persist] Saving {Count} comments to extract_comments...", result.Count);
                    await PersistCommentsAsync(videoUrl, result);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Comments][FATAL] Failed to extract comments for URL: {Url}", videoUrl);
                return result;
            }
        }

        private async Task PersistCommentsAsync(string videoUrl, List<CommentData> comments)
        {
            int saved = 0, failed = 0;
            foreach (var c in comments)
            {
                try
                {
                    await _commentRepo!.CreateAsync(new ExtractedCommentRecord
                    {
                        VideoId = videoUrl,
                        CommentExternalId = c.CommentId,
                        Text = c.Text,
                        Author = c.Author,
                        LikeCount = c.LikeCount ?? 0,
                        CommentedAt = c.Timestamp.HasValue
                            ? DateTimeOffset.FromUnixTimeSeconds(c.Timestamp.Value).DateTime
                            : null
                    });
                    saved++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Warning(ex, "[Comments][Persist] Failed to save comment by '{Author}': {Error}", c.Author, ex.Message);
                }
            }
            Log.Information("[Comments][Persist] Result: {Saved} saved, {Failed} failed out of {Total}", saved, failed, comments.Count);
        }

        private static string? TryExtractVideoId(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) return null;
            var match = Regex.Match(videoUrl, @"/video/(?<id>\d+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["id"].Value : null;
        }

        private static int? TryInt(JToken? token)
        {
            if (token == null) return null;
            return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : null;
        }

        private static long? TryLong(JToken? token)
        {
            if (token == null) return null;
            return long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : null;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
        }
    }
}
