using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FluxAnswer.Services.AI
{
    /// <summary>
    /// Minimal REST client for AssemblyAI speech-to-text using upload + transcript + polling.
    /// </summary>
    public sealed class AssemblyAiRestClient
    {
        private const string BaseUrl = "https://api.assemblyai.com/v2";
        private static readonly string[] SpeechModels = { "universal-3-pro", "universal-2" };

        private readonly HttpClient _httpClient;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _pollTimeout = TimeSpan.FromMinutes(10);

        public AssemblyAiRestClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("AssemblyAI API key is required.", nameof(apiKey));
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(11)
            };
            _httpClient.DefaultRequestHeaders.Add("authorization", apiKey);
        }

        public async Task<string> TranscribeFileAsync(string audioPath, CancellationToken cancellationToken = default)
        {
            var uploadUrl = await UploadFileAsync(audioPath, cancellationToken);
            var transcriptId = await SubmitTranscriptAsync(uploadUrl, cancellationToken);
            return await PollTranscriptAsync(transcriptId, cancellationToken);
        }

        private async Task<string> UploadFileAsync(string audioPath, CancellationToken cancellationToken)
        {
            using var fileStream = File.OpenRead(audioPath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await _httpClient.PostAsync($"{BaseUrl}/upload", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"AssemblyAI upload failed ({(int)response.StatusCode}): {body}");
            }

            var upload = JsonConvert.DeserializeObject<UploadResponse>(body);
            if (upload == null || string.IsNullOrWhiteSpace(upload.UploadUrl))
            {
                throw new InvalidOperationException("AssemblyAI upload response did not include upload_url.");
            }

            return upload.UploadUrl;
        }

        private async Task<string> SubmitTranscriptAsync(string audioUrl, CancellationToken cancellationToken)
        {
            var payload = new CreateTranscriptRequest
            {
                AudioUrl = audioUrl,
                LanguageDetection = true,
                SpeechModels = SpeechModels
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{BaseUrl}/transcript", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"AssemblyAI transcript submit failed ({(int)response.StatusCode}): {body}");
            }

            var submit = JsonConvert.DeserializeObject<TranscriptResponse>(body);
            if (submit == null || string.IsNullOrWhiteSpace(submit.Id))
            {
                throw new InvalidOperationException("AssemblyAI transcript submit response did not include id.");
            }

            return submit.Id;
        }

        private async Task<string> PollTranscriptAsync(string transcriptId, CancellationToken cancellationToken)
        {
            var started = DateTime.UtcNow;

            while (DateTime.UtcNow - started <= _pollTimeout)
            {
                using var response = await _httpClient.GetAsync($"{BaseUrl}/transcript/{transcriptId}", cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"AssemblyAI transcript poll failed ({(int)response.StatusCode}): {body}");
                }

                var status = JsonConvert.DeserializeObject<TranscriptResponse>(body);
                if (status == null)
                {
                    throw new InvalidOperationException("AssemblyAI transcript poll response could not be parsed.");
                }

                if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(status.Text))
                    {
                        throw new InvalidOperationException("AssemblyAI completed transcription with empty text.");
                    }

                    return status.Text;
                }

                if (string.Equals(status.Status, "error", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Transcription failed: {status.Error ?? "Unknown error"}");
                }

                await Task.Delay(_pollInterval, cancellationToken);
            }

            throw new TimeoutException("AssemblyAI transcription polling timed out.");
        }

        private sealed class UploadResponse
        {
            [JsonProperty("upload_url")]
            public string UploadUrl { get; set; } = string.Empty;
        }

        private sealed class CreateTranscriptRequest
        {
            [JsonProperty("audio_url")]
            public string AudioUrl { get; set; } = string.Empty;

            [JsonProperty("language_detection")]
            public bool LanguageDetection { get; set; }

            [JsonProperty("speech_models")]
            public string[] SpeechModels { get; set; } = Array.Empty<string>();
        }

        private sealed class TranscriptResponse
        {
            [JsonProperty("id")]
            public string Id { get; set; } = string.Empty;

            [JsonProperty("status")]
            public string Status { get; set; } = string.Empty;

            [JsonProperty("text")]
            public string Text { get; set; } = string.Empty;

            [JsonProperty("error")]
            public string? Error { get; set; }
        }
    }
}
