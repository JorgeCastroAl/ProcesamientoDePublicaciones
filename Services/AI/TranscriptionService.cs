using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace FluxAnswer.Services.AI
{
    /// <summary>
    /// Service for transcribing audio using AssemblyAI API.
    /// </summary>
    public class TranscriptionService : ITranscriptionService
    {
        private readonly AssemblyAiRestClient _client;
        private readonly string _apiKey;

        public TranscriptionService(string apiKey)
        {
            _apiKey = apiKey;
            _client = new AssemblyAiRestClient(_apiKey);
        }

        public async Task<string> TranscribeAsync(string audioPath)
        {
            try
            {
                var totalTimer = Stopwatch.StartNew();
                var fileInfo = new FileInfo(audioPath);
                Log.Information("[Transcription] Starting for file: {AudioPath} (Exists: {Exists}, SizeBytes: {Size})",
                    audioPath,
                    fileInfo.Exists,
                    fileInfo.Exists ? fileInfo.Length : 0);

                if (string.IsNullOrWhiteSpace(_apiKey) ||
                    string.Equals(_apiKey, "YOUR_ASSEMBLYAI_API_KEY_HERE", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("AssemblyAI API key is not configured.");
                }

                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException($"Audio file not found: {audioPath}");
                }

                Log.Information("[Transcription] Sending local file to AssemblyAI REST client");
                var transcript = await _client.TranscribeFileAsync(audioPath);

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    throw new InvalidOperationException("Transcription completed but returned empty text.");
                }

                totalTimer.Stop();
                Log.Information("[Transcription] Completed successfully in {ElapsedMs} ms", totalTimer.ElapsedMilliseconds);

                return transcript;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Transcription] Failed for file: {AudioPath}", audioPath);
                throw;
            }
        }
    }
}
