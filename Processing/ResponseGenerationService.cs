using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Service for generating responses using local REST API with graceful degradation.
    /// </summary>
    public class ResponseGenerationService : IResponseGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiAuditLogPath;
        private static readonly object _apiAuditLogLock = new object();

        public ResponseGenerationService(string apiUrl)
        {
            _apiUrl = apiUrl;
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

                // Build request - use transcription as subject if available, otherwise use title
                // Prepare transcription (null if not available or failed)
                string? transcriptionValue = null;
                if (!string.IsNullOrWhiteSpace(transcription) && 
                    transcription != "[Transcription failed]" && 
                    transcription != "[No transcription available]")
                {
                    transcriptionValue = transcription;
                }

                // Prepare comments list (API requires at least one comment)
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
                Log.Information("=== REQUEST TO OPINION API ===");
                Log.Information("URL: {ApiUrl}", _apiUrl);
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
                        var response = await _httpClient.PostAsync(_apiUrl, content);

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
                            url = _apiUrl,
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

                        Log.Warning("✗ Response API returned HTTP {StatusCode} for video: {VideoId}. Error: {Error}",
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
                            url = _apiUrl,
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
                            url = _apiUrl,
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
                    url = _apiUrl,
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
