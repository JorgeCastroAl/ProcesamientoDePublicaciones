using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace FluxAnswer.Configuration
{
    /// <summary>
    /// Validates startup requirements before the service can run.
    /// </summary>
    public class StartupValidator
    {
        private readonly IConfigurationManager _config;
        private readonly HttpClient _httpClient;

        public StartupValidator(IConfigurationManager config)
        {
            _config = config;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        /// <summary>
        /// Validates all startup requirements.
        /// Returns true if all validations pass, false otherwise.
        /// </summary>
        public async Task<bool> ValidateStartupRequirementsAsync()
        {
            Log.Information("========== Starting Startup Validation ==========");

            bool allValid = true;

            // 1. Validate configuration fields
            if (!ValidateConfigurationFields())
            {
                allValid = false;
            }

            // 2. Validate PocketBase connectivity
            if (!await ValidatePocketBaseConnectivityAsync())
            {
                allValid = false;
            }

            // 3. Validate FFmpeg availability
            if (!ValidateFFmpegAvailability())
            {
                allValid = false;
            }

            // 4. Validate AssemblyAI API key
            if (!ValidateAssemblyAIApiKey())
            {
                allValid = false;
            }

            // 5. Validate yt-dlp availability
            if (!await ValidateYtDlpAvailabilityAsync())
            {
                allValid = false;
            }

            if (allValid)
            {
                Log.Information("========== All Startup Validations Passed ==========");
            }
            else
            {
                Log.Error("========== Startup Validation Failed ==========");
            }

            return allValid;
        }

        private bool ValidateConfigurationFields()
        {
            Log.Information("Validating configuration fields...");

            bool valid = true;

            if (string.IsNullOrWhiteSpace(_config.PocketBaseUrl))
            {
                Log.Error("Configuration Error: pocketbase_url is required but not set");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(_config.AssemblyAIApiKey))
            {
                Log.Error("Configuration Error: assemblyai_api_key is required but not set");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(_config.ResponseApiUrl))
            {
                Log.Error("Configuration Error: response_api_url is required but not set");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(_config.TempDirectory))
            {
                Log.Error("Configuration Error: temp_directory is required but not set");
                valid = false;
            }

            if (_config.ExtractionIntervalMinutes <= 0)
            {
                Log.Error("Configuration Error: extraction_interval_minutes must be greater than 0");
                valid = false;
            }

            if (_config.ProcessingRetryCount < 0)
            {
                Log.Error("Configuration Error: processing_retry_count must be 0 or greater");
                valid = false;
            }

            if (valid)
            {
                Log.Information("Configuration fields validation: PASSED");
            }

            return valid;
        }

        private async Task<bool> ValidatePocketBaseConnectivityAsync()
        {
            Log.Information("Validating PocketBase connectivity...");

            try
            {
                // Create a new HttpClient with aggressive timeout for validation
                using var validationClient = new HttpClient 
                { 
                    Timeout = TimeSpan.FromSeconds(5) 
                };

                // Use a cancellation token for extra safety
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));

                // Try to connect to PocketBase root endpoint
                // Even a 404 response means PocketBase is running
                var response = await validationClient.GetAsync($"{_config.PocketBaseUrl}/", cts.Token);

                // Any HTTP response (including 404) means PocketBase is running
                Log.Information("PocketBase connectivity validation: PASSED (HTTP {StatusCode})", (int)response.StatusCode);
                return true;
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "PocketBase connectivity validation: FAILED - Cannot connect to {Url}", _config.PocketBaseUrl);
                return false;
            }
            catch (TaskCanceledException)
            {
                Log.Error("PocketBase connectivity validation: FAILED - Connection timeout to {Url}", _config.PocketBaseUrl);
                return false;
            }
            catch (OperationCanceledException)
            {
                Log.Error("PocketBase connectivity validation: FAILED - Operation cancelled (timeout) to {Url}", _config.PocketBaseUrl);
                return false;
            }
        }

        private bool ValidateFFmpegAvailability()
        {
            Log.Information("Validating FFmpeg availability...");

            try
            {
                var ffmpegPath = _config.FFmpegPath;

                // Check if it's an absolute path
                if (Path.IsPathRooted(ffmpegPath))
                {
                    if (File.Exists(ffmpegPath))
                    {
                        Log.Information("FFmpeg found at: {Path}", ffmpegPath);
                        Log.Information("FFmpeg availability validation: PASSED");
                        return true;
                    }
                }

                // Try to find in PATH or current directory
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    Log.Information("FFmpeg availability validation: PASSED");
                    return true;
                }
                else
                {
                    Log.Warning("FFmpeg validation: Command executed but returned non-zero exit code");
                    Log.Information("FFmpeg availability validation: PASSED (with warnings)");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FFmpeg not found or not executable. This is optional for the system.");
                Log.Information("FFmpeg availability validation: PASSED (optional dependency)");
                return true; // FFmpeg is optional, so we don't fail startup
            }
        }

        private bool ValidateAssemblyAIApiKey()
        {
            Log.Information("Validating AssemblyAI API key...");

            if (string.IsNullOrWhiteSpace(_config.AssemblyAIApiKey))
            {
                Log.Error("AssemblyAI API key validation: FAILED - API key is empty");
                return false;
            }

            if (_config.AssemblyAIApiKey.Length < 20)
            {
                Log.Error("AssemblyAI API key validation: FAILED - API key appears to be invalid (too short)");
                return false;
            }

            Log.Information("AssemblyAI API key validation: PASSED (format check only)");
            return true;
        }

        private async Task<bool> ValidateYtDlpAvailabilityAsync()
        {
            Log.Information("Validating yt-dlp availability...");

            try
            {
                var ytDlp = new Extraction.YtDlpWrapperV2();
                var isAvailable = await ytDlp.ValidateAvailabilityAsync();

                if (isAvailable)
                {
                    Log.Information("yt-dlp availability validation: PASSED");
                    return true;
                }
                else
                {
                    Log.Error("yt-dlp availability validation: FAILED - yt-dlp not found or not executable");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "yt-dlp availability validation: FAILED");
                return false;
            }
        }
    }
}

