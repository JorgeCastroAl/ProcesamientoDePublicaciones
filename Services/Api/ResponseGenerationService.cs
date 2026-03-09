using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using FluxAnswer.Configuration;
using FluxAnswer.Models;
using FluxAnswer.Repositories;

namespace FluxAnswer.Services.Api
{
    /// <summary>
    /// Service for generating responses using local REST API with graceful degradation.
    /// </summary>
    public class ResponseGenerationService : IResponseGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationManager _config;
        private readonly string _apiAuditLogPath;
        private readonly IBotAccountRepo _botAccountRepo;
        private readonly IBotAccountVideoRepo _botAccountVideoRepo;
        private static readonly object _apiAuditLogLock = new object();

        public ResponseGenerationService(
            IConfigurationManager config,
            IBotAccountRepo botAccountRepo,
            IBotAccountVideoRepo botAccountVideoRepo)
        {
            _config = config;
            _botAccountRepo = botAccountRepo;
            _botAccountVideoRepo = botAccountVideoRepo;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TikTokManager",
                "logs"
            );

            Directory.CreateDirectory(logDirectory);
            _apiAuditLogPath = Path.Combine(logDirectory, "opinion-api-audit.log");
        }

        public async Task<ResponseResult> GenerateResponseAsync(
            VideoRecord video,
            string transcription,
            List<CommentData> comments)
        {
            try
            {
                Log.Information("Generating response for video: {VideoId}", video.TiktokVideoId);

                string? transcriptionValue = null;
                if (!string.IsNullOrWhiteSpace(transcription) &&
                    transcription != "[Transcription failed]" &&
                    transcription != "[No transcription available]")
                {
                    transcriptionValue = transcription;
                }

                List<string> commentsList;
                if (comments.Count > 0)
                {
                    commentsList = comments
                        .Select(c => c.Text)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .ToList();
                }
                else
                {
                    commentsList = new List<string>();
                }

                if (commentsList.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(transcriptionValue))
                    {
                        var fallbackText = transcriptionValue.Length > 500
                            ? transcriptionValue.Substring(0, 500)
                            : transcriptionValue;

                        commentsList.Add(fallbackText);
                        Log.Information("No comments found; using transcription fallback as single comment for video {VideoId}", video.TiktokVideoId);
                    }
                    else if (!string.IsNullOrWhiteSpace(video.Title))
                    {
                        commentsList.Add(video.Title);
                        Log.Information("No comments/transcription found; using title fallback as single comment for video {VideoId}", video.TiktokVideoId);
                    }
                    else
                    {
                        Log.Warning("No comments, transcription, or title available to query Opinion API for video {VideoId}", video.TiktokVideoId);
                        return new ResponseResult
                        {
                            Success = false,
                            ErrorMessage = "No input text available for Opinion API"
                        };
                    }
                }

                var request = new ResponseApiRequest
                {
                    VideoId = video.TiktokVideoId,
                    Comments = commentsList,
                    Transcription = transcriptionValue,
                    TituloVideo = video.Title,
                    ProfileType = "DEFENSA_PERSONA"
                };

                var json = JsonConvert.SerializeObject(request, Formatting.Indented);
                var requestId = Guid.NewGuid().ToString("N");
                var apiUrl = GetResponseApiUrl();
                Log.Information("=== REQUEST TO OPINION API ===");
                Log.Information("URL: {ApiUrl}", apiUrl);
                Log.Information("VideoId: {VideoId}", request.VideoId);
                Log.Information("Comments: {Comments}", request.Comments != null ? $"{request.Comments.Count} comments" : "null");
                Log.Information("Transcription: {Transcription}", request.Transcription != null ? $"{request.Transcription.Length} chars" : "null");
                Log.Information("TituloVideo: {TituloVideo}", request.TituloVideo ?? "null");
                Log.Information("ProfileType: {ProfileType}", request.ProfileType);
                Log.Information("JSON Body: {Json}", json);

                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        Log.Information("Calling Opinion API (attempt {Attempt}/{MaxAttempts}) for video {VideoId}",
                            attempt, maxAttempts, video.TiktokVideoId);

                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await _httpClient.PostAsync(apiUrl, content);

                        Log.Information("=== RESPONSE FROM OPINION API ===");
                        Log.Information("Status Code: {StatusCode}", response.StatusCode);

                        var responseContent = await response.Content.ReadAsStringAsync();
                        Log.Information("Response Body: {ResponseBody}", responseContent);
                        WriteApiAuditLog(new
                        {
                            timestamp_utc = DateTime.UtcNow,
                            request_id = requestId,
                            attempt,
                            max_attempts = maxAttempts,
                            url = apiUrl,
                            video_id = video.TiktokVideoId,
                            request = request,
                            response_status_code = (int)response.StatusCode,
                            response_body = responseContent,
                            success = response.IsSuccessStatusCode
                        });

                        if (response.IsSuccessStatusCode)
                        {
                            var apiResponse = JsonConvert.DeserializeObject<ResponseApiResponse>(responseContent);

                            if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Opinion))
                            {
                                Log.Information("Response generated successfully for video: {VideoId} - Profile: {Profile}, Comments: {Count}",
                                    video.TiktokVideoId, apiResponse.ProfileUsed, apiResponse.CommentsAnalyzed);
                                return new ResponseResult
                                {
                                    Success = true,
                                    ResponseText = apiResponse.Opinion
                                };
                            }

                            Log.Warning("Response API returned empty opinion for video: {VideoId}", video.TiktokVideoId);
                            return new ResponseResult
                            {
                                Success = false,
                                ErrorMessage = "API returned empty opinion"
                            };
                        }

                        bool serverError = (int)response.StatusCode >= 500;
                        if (serverError && attempt < maxAttempts)
                        {
                            var delayMs = attempt * 1500;
                            Log.Warning("Opinion API server error HTTP {StatusCode} for video {VideoId}. Retrying in {DelayMs}ms",
                                response.StatusCode, video.TiktokVideoId, delayMs);
                            await Task.Delay(delayMs);
                            continue;
                        }

                        Log.Warning("[ERROR] Response API returned HTTP {StatusCode} for video: {VideoId}. Error: {Error}",
                            response.StatusCode, video.TiktokVideoId, responseContent);
                        return new ResponseResult
                        {
                            Success = false,
                            ErrorMessage = $"API returned HTTP {response.StatusCode}: {responseContent}"
                        };
                    }
                    catch (HttpRequestException ex)
                    {
                        WriteApiAuditLog(new
                        {
                            timestamp_utc = DateTime.UtcNow,
                            request_id = requestId,
                            attempt,
                            max_attempts = maxAttempts,
                            url = apiUrl,
                            video_id = video.TiktokVideoId,
                            request = request,
                            success = false,
                            error_type = "HttpRequestException",
                            error_message = ex.Message
                        });

                        if (attempt < maxAttempts)
                        {
                            var delayMs = attempt * 1500;
                            Log.Warning("Opinion API unreachable on attempt {Attempt}/{MaxAttempts} for video {VideoId}: {Message}. Retrying in {DelayMs}ms",
                                attempt, maxAttempts, video.TiktokVideoId, ex.Message, delayMs);
                            await Task.Delay(delayMs);
                            continue;
                        }

                        Log.Information("Response API is unreachable for video {VideoId}: {Message}", video.TiktokVideoId, ex.Message);
                        return new ResponseResult
                        {
                            Success = false,
                            ErrorMessage = "API unreachable"
                        };
                    }
                    catch (TaskCanceledException ex)
                    {
                        WriteApiAuditLog(new
                        {
                            timestamp_utc = DateTime.UtcNow,
                            request_id = requestId,
                            attempt,
                            max_attempts = maxAttempts,
                            url = apiUrl,
                            video_id = video.TiktokVideoId,
                            request = request,
                            success = false,
                            error_type = "TaskCanceledException",
                            error_message = ex.Message
                        });

                        if (attempt < maxAttempts)
                        {
                            var delayMs = attempt * 1500;
                            Log.Warning("Opinion API timeout on attempt {Attempt}/{MaxAttempts} for video {VideoId}: {Message}. Retrying in {DelayMs}ms",
                                attempt, maxAttempts, video.TiktokVideoId, ex.Message, delayMs);
                            await Task.Delay(delayMs);
                            continue;
                        }

                        Log.Information("Response API timeout for video {VideoId}: {Message}", video.TiktokVideoId, ex.Message);
                        return new ResponseResult
                        {
                            Success = false,
                            ErrorMessage = "API timeout"
                        };
                    }
                }

                return new ResponseResult
                {
                    Success = false,
                    ErrorMessage = "API retries exhausted"
                };
            }
            catch (Exception ex)
            {
                WriteApiAuditLog(new
                {
                    timestamp_utc = DateTime.UtcNow,
                    url = GetResponseApiUrl(),
                    video_id = video.TiktokVideoId,
                    success = false,
                    error_type = ex.GetType().Name,
                    error_message = ex.Message,
                    stack_trace = ex.StackTrace
                });

                Log.Error(ex, "Unexpected error generating response for video: {VideoId}", video.TiktokVideoId);
                return new ResponseResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public Task<(bool Success, int GeneratedCount)> GenerateCustomCommentsForBotAccountsAsync(
            VideoRecord video,
            int customCommentsPerAccount)
        {
            return GenerateCustomCommentsInternalAsync(video, customCommentsPerAccount);
        }

        private async Task<(bool Success, int GeneratedCount)> GenerateCustomCommentsInternalAsync(VideoRecord video, int customCommentsPerAccount)
        {
            if (customCommentsPerAccount <= 0)
            {
                Log.Information("Custom comments generation disabled (count <= 0) for video {VideoId}", video.TiktokVideoId);
                return (true, 0);
            }

            if (string.IsNullOrWhiteSpace(video.Id))
            {
                Log.Warning("Cannot generate custom comments because video.Id is empty for TikTok video {VideoId}", video.TiktokVideoId);
                return (false, 0);
            }

            if (string.IsNullOrWhiteSpace(video.ResponseText))
            {
                Log.Warning("Cannot generate custom comments because ResponseText is empty for video {VideoId}", video.TiktokVideoId);
                return (false, 0);
            }

            var accounts = await _botAccountRepo.GetActiveAccountsAsync();
            if (accounts.Count == 0)
            {
                Log.Information("No active bot accounts found. Skipping custom comments for video {VideoId}", video.TiktokVideoId);
                return (true, 0);
            }

            Log.Information(
                "Generating custom comments for video {VideoId}. Accounts: {Accounts}, CountPerAccount: {CountPerAccount}, Endpoint: {Endpoint}",
                video.TiktokVideoId,
                accounts.Count,
                customCommentsPerAccount,
                GetModifyCommentApiUrl());

            var totalCreated = 0;
            var hadAccountFailures = false;
            foreach (var account in accounts)
            {
                if (string.IsNullOrWhiteSpace(account.Id))
                {
                    Log.Warning("Skipping bot account with empty id (username: {Username})", account.Username);
                    hadAccountFailures = true;
                    continue;
                }

                var existingForAccount = await _botAccountVideoRepo.GetByBotAccountIdAsync(account.Id);
                var existingForVideo = existingForAccount
                    .Where(link => string.Equals(link.VideoId, video.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (existingForVideo.Count >= customCommentsPerAccount)
                {
                    Log.Information(
                        "Skipping account {Username} for video {VideoId}: already has {ExistingCount}/{ConfiguredCount} custom comments",
                        account.Username,
                        video.TiktokVideoId,
                        existingForVideo.Count,
                        customCommentsPerAccount);
                    continue;
                }

                var existingCommentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var existingLink in existingForVideo)
                {
                    var key = NormalizeCommentKey(existingLink.CustomCommentText);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        existingCommentKeys.Add(key);
                    }
                }

                var requestTemplate = new PolishCommentRequest
                {
                    AffinityLevel = NormalizeEnumValue(account.AffinityLevel, "IMPARCIAL", "IMPARCIAL", "FANATICO", "PARCIALIZADO"),
                    MoodState = NormalizeEnumValue(account.MoodState, "ALEGRE", "ALEGRE", "MOLESTO", "EUFORICO"),
                    PersonalityType = NormalizeEnumValue(account.PersonalityType, "AMABLE", "AMABLE", "CONFRONTACIONAL", "TECNICO"),
                    Gender = NormalizeEnumValue(account.Gender, "NEUTRO", "MUJER", "HOMBRE", "NEUTRO"),
                    GeneratedComment = video.ResponseText
                };

                var missingCount = customCommentsPerAccount - existingForVideo.Count;
                var createdForAccount = 0;
                var generationAttempts = 0;
                var maxGenerationAttempts = Math.Max(missingCount * 3, missingCount);

                while (createdForAccount < missingCount && generationAttempts < maxGenerationAttempts)
                {
                    generationAttempts++;
                    var targetIndex = existingForVideo.Count + createdForAccount + 1;
                    var modifiedComment = await ModifyCommentAsync(video, account, requestTemplate, targetIndex, customCommentsPerAccount);
                    if (string.IsNullOrWhiteSpace(modifiedComment))
                    {
                        continue;
                    }

                    var commentKey = NormalizeCommentKey(modifiedComment);
                    if (string.IsNullOrWhiteSpace(commentKey))
                    {
                        continue;
                    }

                    if (existingCommentKeys.Contains(commentKey))
                    {
                        Log.Information(
                            "Duplicate custom comment avoided for account {Username} on video {VideoId}",
                            account.Username,
                            video.TiktokVideoId);
                        continue;
                    }

                    var botAccountVideo = new BotAccountVideo(account.Id, video.Id, modifiedComment)
                    {
                        CommentSent = false,
                        Priority = video.Priority
                    };

                    await _botAccountVideoRepo.CreateAsync(botAccountVideo);
                    existingCommentKeys.Add(commentKey);
                    createdForAccount++;
                    totalCreated++;
                }

                if (createdForAccount < missingCount)
                {
                    hadAccountFailures = true;
                    Log.Warning(
                        "Could not complete requested custom comments for account {Username} on video {VideoId}. Created {Created}/{RequestedMissing}",
                        account.Username,
                        video.TiktokVideoId,
                        createdForAccount,
                        missingCount);
                }
            }

            Log.Information(
                "Custom comments generation finished for video {VideoId}. Records created in bot_account_video: {TotalCreated}",
                video.TiktokVideoId,
                totalCreated);

            return (!hadAccountFailures, totalCreated);
        }

        private async Task<string?> ModifyCommentAsync(
            VideoRecord video,
            BotAccount account,
            PolishCommentRequest requestTemplate,
            int index,
            int totalPerAccount)
        {
            var payload = new PolishCommentRequest
            {
                AffinityLevel = requestTemplate.AffinityLevel,
                MoodState = requestTemplate.MoodState,
                PersonalityType = requestTemplate.PersonalityType,
                Gender = requestTemplate.Gender,
                GeneratedComment = requestTemplate.GeneratedComment
            };

            var json = JsonConvert.SerializeObject(payload, Formatting.None);
            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var modifyCommentApiUrl = GetModifyCommentApiUrl();
                    var requestId = Guid.NewGuid().ToString("N");
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(modifyCommentApiUrl, content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    WriteApiAuditLog(new
                    {
                        timestamp_utc = DateTime.UtcNow,
                        request_id = requestId,
                        endpoint = "modify-comment",
                        attempt,
                        max_attempts = maxAttempts,
                        url = modifyCommentApiUrl,
                        video_id = video.TiktokVideoId,
                        bot_account_id = account.Id,
                        bot_account_username = account.Username,
                        custom_comment_index = index,
                        custom_comment_total = totalPerAccount,
                        request = payload,
                        response_status_code = (int)response.StatusCode,
                        response_body = responseContent,
                        success = response.IsSuccessStatusCode
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonConvert.DeserializeObject<PolishCommentResponse>(responseContent);
                        var polishedComment = apiResponse?.PolishedComment;

                        if (!string.IsNullOrWhiteSpace(polishedComment))
                        {
                            Log.Information(
                                "Custom comment generated for account {Username} ({Index}/{Total}) on video {VideoId}",
                                account.Username,
                                index,
                                totalPerAccount,
                                video.TiktokVideoId);
                            return polishedComment;
                        }

                        Log.Warning(
                            "modify-comment returned empty polishedComment for account {Username} ({Index}/{Total}) on video {VideoId}",
                            account.Username,
                            index,
                            totalPerAccount,
                            video.TiktokVideoId);
                        return null;
                    }

                    var isServerError = (int)response.StatusCode >= 500;
                    if (isServerError && attempt < maxAttempts)
                    {
                        await Task.Delay(attempt * 1500);
                        continue;
                    }

                    Log.Warning(
                        "modify-comment failed HTTP {StatusCode} for account {Username} ({Index}/{Total}) on video {VideoId}: {Body}",
                        response.StatusCode,
                        account.Username,
                        index,
                        totalPerAccount,
                        video.TiktokVideoId,
                        responseContent);
                    return null;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(attempt * 1500);
                        continue;
                    }

                    Log.Warning(
                        ex,
                        "modify-comment exception for account {Username} ({Index}/{Total}) on video {VideoId}",
                        account.Username,
                        index,
                        totalPerAccount,
                        video.TiktokVideoId);
                    return null;
                }
            }

            return null;
        }

        private static string NormalizeEnumValue(string? rawValue, string defaultValue, params string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue;
            }

            var normalized = rawValue.Trim().ToUpperInvariant();
            return allowed.Contains(normalized) ? normalized : defaultValue;
        }

        private static string NormalizeCommentKey(string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return string.Empty;
            }

            return comment.Trim();
        }

        private string GetResponseApiUrl()
        {
            return _config.ResponseApiUrl;
        }

        private string GetModifyCommentApiUrl()
        {
            if (!string.IsNullOrWhiteSpace(_config.ModifyCommentApiUrl))
            {
                return _config.ModifyCommentApiUrl;
            }

            return BuildModifyCommentApiUrl(_config.ResponseApiUrl);
        }

        private static string BuildModifyCommentApiUrl(string generateCommentApiUrl)
        {
            if (string.IsNullOrWhiteSpace(generateCommentApiUrl))
            {
                return "http://localhost:9090/api/opinion/modify-comment";
            }

            const string generatePath = "/api/opinion/generate-comment";
            const string modifyPath = "/api/opinion/modify-comment";

            if (generateCommentApiUrl.EndsWith(generatePath, StringComparison.OrdinalIgnoreCase))
            {
                return generateCommentApiUrl.Substring(0, generateCommentApiUrl.Length - generatePath.Length) + modifyPath;
            }

            if (generateCommentApiUrl.EndsWith("/"))
            {
                return generateCommentApiUrl.TrimEnd('/') + modifyPath;
            }

            return generateCommentApiUrl + modifyPath;
        }

        private void WriteApiAuditLog(object payload)
        {
            try
            {
                var line = JsonConvert.SerializeObject(payload, Formatting.None);
                lock (_apiAuditLogLock)
                {
                    File.AppendAllText(_apiAuditLogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write opinion API audit log");
            }
        }
    }
}
