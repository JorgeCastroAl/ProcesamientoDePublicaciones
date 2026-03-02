using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using VideoProcessingSystemV2.Models;

namespace VideoProcessingSystemV2.Processing
{
    /// <summary>
    /// Service for transcribing audio using AssemblyAI API.
    /// </summary>
    public class TranscriptionService : ITranscriptionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.assemblyai.com/v2";
        private const int PollingIntervalSeconds = 5;
        private const int MaxWaitMinutes = 5;

        public TranscriptionService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(MaxWaitMinutes + 1)
            };
            // AssemblyAI requires the API key directly without "Bearer" prefix
            _httpClient.DefaultRequestHeaders.Add("Authorization", _apiKey);
        }

        public async Task<string> TranscribeAsync(string audioPath)
        {
            try
            {
                Log.Information("Starting transcription for: {AudioPath}", audioPath);

                // Step 1: Upload audio file
                var uploadUrl = await UploadAudioAsync(audioPath);
                Log.Information("Audio uploaded successfully");

                // Step 2: Submit transcription request
                var transcriptId = await SubmitTranscriptionAsync(uploadUrl);
                Log.Information("Transcription submitted with ID: {TranscriptId}", transcriptId);

                // Step 3: Poll for completion
                var transcription = await PollForCompletionAsync(transcriptId);
                Log.Information("Transcription completed successfully");

                return transcription;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Transcription failed for: {AudioPath}", audioPath);
                throw;
            }
        }

        private async Task<string> UploadAudioAsync(string audioPath)
        {
            if (!File.Exists(audioPath))
            {
                throw new FileNotFoundException($"Audio file not found: {audioPath}");
            }

            var url = $"{BaseUrl}/upload";
            
            using var fileStream = File.OpenRead(audioPath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var uploadResponse = JsonConvert.DeserializeObject<UploadResponse>(responseContent);

            if (uploadResponse == null || string.IsNullOrEmpty(uploadResponse.UploadUrl))
            {
                throw new InvalidOperationException("Failed to get upload URL from AssemblyAI");
            }

            return uploadResponse.UploadUrl;
        }

        private async Task<string> SubmitTranscriptionAsync(string audioUrl)
        {
            var url = $"{BaseUrl}/transcript";

            var requestBody = new
            {
                audio_url = audioUrl,
                speech_models = new[] { "universal-2" }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("AssemblyAI API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<TranscriptionResult>(responseContent);

            if (result == null || string.IsNullOrEmpty(result.Id))
            {
                throw new InvalidOperationException("Failed to submit transcription to AssemblyAI");
            }

            return result.Id;
        }

        private async Task<string> PollForCompletionAsync(string transcriptId)
        {
            var url = $"{BaseUrl}/transcript/{transcriptId}";
            var startTime = DateTime.UtcNow;
            var maxWaitTime = TimeSpan.FromMinutes(MaxWaitMinutes);

            while (true)
            {
                // Check timeout
                if (DateTime.UtcNow - startTime > maxWaitTime)
                {
                    throw new TimeoutException($"Transcription timed out after {MaxWaitMinutes} minutes");
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TranscriptionResult>(responseContent);

                if (result == null)
                {
                    throw new InvalidOperationException("Failed to deserialize transcription status");
                }

                if (result.IsCompleted)
                {
                    if (string.IsNullOrEmpty(result.Text))
                    {
                        throw new InvalidOperationException("Transcription completed but text is empty");
                    }
                    return result.Text;
                }

                if (result.IsError)
                {
                    throw new InvalidOperationException($"Transcription failed: {result.Error}");
                }

                // Still processing, wait before next poll
                Log.Debug("Transcription status: {Status}, waiting {Interval}s...", result.Status, PollingIntervalSeconds);
                await Task.Delay(PollingIntervalSeconds * 1000);
            }
        }
    }
}

    internal class UploadResponse
    {
        [JsonProperty("upload_url")]
        public string UploadUrl { get; set; } = string.Empty;
    }
