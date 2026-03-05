using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace FluxAnswer.Services
{
    /// <summary>
    /// Manages the PocketBase process lifecycle.
    /// </summary>
    public class PocketBaseManager : IDisposable
    {
        private Process? _pocketBaseProcess;
        private readonly string _pocketBasePath;
        private readonly string _dataDirectory;
        private readonly string _bindIp;
        private readonly int _port;
        private readonly string _healthCheckUrl;
        private bool _isManaged;

        public PocketBaseManager(
            string? pocketBasePath = null,
            string? dataDirectory = null,
            string? bindIp = null,
            int? port = null)
        {
            // Default paths
            _pocketBasePath = pocketBasePath ?? FindPocketBaseExecutable();
            _dataDirectory = dataDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TikTokManager",
                "pocketbase_data"
            );

            _bindIp = string.IsNullOrWhiteSpace(bindIp) ? "0.0.0.0" : bindIp.Trim();
            _port = port is > 0 and <= 65535 ? port.Value : 8090;

            var healthCheckHost = IsAnyAddress(_bindIp) ? "127.0.0.1" : _bindIp;
            _healthCheckUrl = $"http://{healthCheckHost}:{_port}/";
        }

        /// <summary>
        /// Starts PocketBase if not already running.
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                // Check if PocketBase is already running
                if (await IsPocketBaseRunningAsync())
                {
                    Log.Information("PocketBase is already running");
                    _isManaged = false;
                    return true;
                }

                // Verify executable exists
                if (!File.Exists(_pocketBasePath))
                {
                    Log.Error("PocketBase executable not found at: {Path}", _pocketBasePath);
                    return false;
                }

                // Create data directory if it doesn't exist
                Directory.CreateDirectory(_dataDirectory);

                Log.Information("Starting PocketBase from: {Path}", _pocketBasePath);
                Log.Information("Data directory: {DataDir}", _dataDirectory);
                Log.Information("Bind endpoint: {BindEndpoint}", $"http://{_bindIp}:{_port}");

                // Start PocketBase process
                _pocketBaseProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pocketBasePath,
                        Arguments = $"serve --http \"{_bindIp}:{_port}\" --dir \"{_dataDirectory}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Path.GetDirectoryName(_pocketBasePath) ?? Environment.CurrentDirectory
                    }
                };

                _pocketBaseProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log.Debug("PocketBase: {Output}", e.Data);
                    }
                };

                _pocketBaseProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log.Warning("PocketBase Error: {Error}", e.Data);
                    }
                };

                _pocketBaseProcess.Start();
                _pocketBaseProcess.BeginOutputReadLine();
                _pocketBaseProcess.BeginErrorReadLine();
                _isManaged = true;

                // Wait for PocketBase to start (increased timeout)
                Log.Information("Waiting for PocketBase to initialize...");
                await Task.Delay(3000);

                // Verify it's running with retries
                for (int i = 0; i < 5; i++)
                {
                    if (await IsPocketBaseRunningAsync())
                    {
                        Log.Information("PocketBase started successfully. Health check URL: {HealthCheckUrl}", _healthCheckUrl);
                        return true;
                    }
                    
                    Log.Debug("PocketBase not ready yet, waiting... (attempt {Attempt}/5)", i + 1);
                    await Task.Delay(1000);
                }

                Log.Error("PocketBase process started but not responding after 8 seconds");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start PocketBase");
                return false;
            }
        }

        /// <summary>
        /// Stops PocketBase if it was started by this manager.
        /// </summary>
        public void Stop()
        {
            if (_pocketBaseProcess != null && !_pocketBaseProcess.HasExited && _isManaged)
            {
                try
                {
                    Log.Information("Stopping PocketBase...");
                    _pocketBaseProcess.Kill(true);
                    _pocketBaseProcess.WaitForExit(5000);
                    Log.Information("PocketBase stopped");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error stopping PocketBase");
                }
            }
        }

        /// <summary>
        /// Checks if PocketBase is running by attempting to connect.
        /// </summary>
        private async Task<bool> IsPocketBaseRunningAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.GetAsync(_healthCheckUrl);
                // Any HTTP response (including 404) means PocketBase is running
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the PocketBase executable in common locations.
        /// </summary>
        private string FindPocketBaseExecutable()
        {
            // Check common locations
            var possiblePaths = new[]
            {
                Path.Combine(Environment.CurrentDirectory, "pocketbase.exe"),
                Path.Combine(Environment.CurrentDirectory, "pocketbase", "pocketbase.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pocketbase.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pocketbase", "pocketbase.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TikTokManager", "pocketbase.exe"),
                "pocketbase.exe" // Try PATH
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Log.Information("Found PocketBase at: {Path}", path);
                    return path;
                }
            }

            // Default to current directory
            Log.Warning("PocketBase executable not found in common locations, using default path");
            return Path.Combine(Environment.CurrentDirectory, "pocketbase.exe");
        }

        private static bool IsAnyAddress(string ip)
        {
            return string.Equals(ip, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ip, "::", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ip, "[::]", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            Stop();
            _pocketBaseProcess?.Dispose();
        }
    }
}

