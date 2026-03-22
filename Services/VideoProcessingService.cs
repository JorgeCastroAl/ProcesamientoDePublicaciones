using System;
using System.Threading.Tasks;
using Serilog;
using FluxAnswer.Configuration;
using FluxAnswer.Extraction;
using FluxAnswer.Pipeline.TikTok;

namespace FluxAnswer.Services
{
    /// <summary>
    /// Main service controller for video processing system.
    /// Supports two extraction modes: YtDlp (original) and TikTokApiSearch (keyword-based).
    /// </summary>
    public class VideoProcessingService : IVideoProcessingService
    {
        private readonly StartupValidator _validator;
        private readonly IExtractionCycleManager _ytDlpExtractionManager;
        private readonly ISearchExtractionCycleManager _searchExtractionManager;
        private readonly ITikTokPipelineManager _processingManager;
        private ServiceState _state;
        private ExtractionMode _extractionMode = ExtractionMode.YtDlp;
        private readonly object _lock = new object();

        public ServiceState State
        {
            get { lock (_lock) { return _state; } }
            private set
            {
                lock (_lock)
                {
                    var oldState = _state;
                    _state = value;
                    if (oldState != value)
                        StateChanged?.Invoke(this, new ServiceStateChangedEventArgs(oldState, value));
                }
            }
        }

        public ExtractionMode ExtractionMode
        {
            get { lock (_lock) { return _extractionMode; } }
        }

        public event EventHandler<ServiceStateChangedEventArgs>? StateChanged;
        public event EventHandler<ErrorEventArgs>? ErrorOccurred;

        public VideoProcessingService(
            StartupValidator validator,
            IExtractionCycleManager ytDlpExtractionManager,
            ISearchExtractionCycleManager searchExtractionManager,
            ITikTokPipelineManager processingManager,
            IConfigurationManager config)
        {
            _validator = validator;
            _ytDlpExtractionManager = ytDlpExtractionManager;
            _searchExtractionManager = searchExtractionManager;
            _processingManager = processingManager;
            _state = ServiceState.Stopped;

            // Restore extraction mode from persisted configuration
            _extractionMode = string.Equals(config.ExtractionMode, "TikTokApi", StringComparison.OrdinalIgnoreCase)
                ? ExtractionMode.TikTokApiSearch
                : ExtractionMode.YtDlp;
        }

        public async Task StartAsync()
        {
            if (State != ServiceState.Stopped)
            {
                Log.Warning("Service is already running or starting");
                return;
            }

            try
            {
                State = ServiceState.Starting;
                Log.Information("========== Starting Video Processing Service (Mode={Mode}) ==========", _extractionMode);

                var isValid = await _validator.ValidateStartupRequirementsAsync();
                if (!isValid)
                {
                    State = ServiceState.Error;
                    var errorMsg = "Startup validation failed";
                    Log.Error(errorMsg);
                    ErrorOccurred?.Invoke(this, new ErrorEventArgs(errorMsg));
                    return;
                }

                // Start the active extraction manager based on mode
                await StartActiveExtractionManagerAsync();

                Log.Information("Starting processing pipeline manager...");
                await _processingManager.StartAsync();

                State = ServiceState.Running;
                Log.Information("========== Video Processing Service Started Successfully ==========");
            }
            catch (Exception ex)
            {
                State = ServiceState.Error;
                Log.Error(ex, "Failed to start video processing service");
                ErrorOccurred?.Invoke(this, new ErrorEventArgs("Failed to start service", ex));
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (State != ServiceState.Running)
            {
                Log.Warning("Service is not running");
                return;
            }

            try
            {
                State = ServiceState.Stopping;
                Log.Information("========== Stopping Video Processing Service ==========");

                // Stop both extraction managers (only the active one is running, but safe to call both)
                await _ytDlpExtractionManager.StopAsync();
                await _searchExtractionManager.StopAsync();

                Log.Information("Stopping processing pipeline manager...");
                var stopTask = _processingManager.StopAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
                var completedTask = await Task.WhenAny(stopTask, timeoutTask);
                if (completedTask == timeoutTask)
                    Log.Warning("Processing pipeline manager did not stop within 2 minutes");

                State = ServiceState.Stopped;
                Log.Information("========== Video Processing Service Stopped ==========");
            }
            catch (Exception ex)
            {
                State = ServiceState.Error;
                Log.Error(ex, "Error stopping video processing service");
                ErrorOccurred?.Invoke(this, new ErrorEventArgs("Error stopping service", ex));
                throw;
            }
        }

        public async Task SetExtractionModeAsync(ExtractionMode mode)
        {
            if (_extractionMode == mode) return;

            var wasRunning = State == ServiceState.Running;
            Log.Information("Switching extraction mode from {Old} to {New}", _extractionMode, mode);

            if (wasRunning)
            {
                // Stop current extraction manager
                await _ytDlpExtractionManager.StopAsync();
                await _searchExtractionManager.StopAsync();
            }

            lock (_lock) { _extractionMode = mode; }

            if (wasRunning)
            {
                await StartActiveExtractionManagerAsync();
            }

            Log.Information("Extraction mode switched to {Mode}", mode);
        }

        private async Task StartActiveExtractionManagerAsync()
        {
            if (_extractionMode == ExtractionMode.YtDlp)
            {
                Log.Information("Starting yt-dlp extraction cycle manager...");
                await _ytDlpExtractionManager.StartAsync();
            }
            else
            {
                Log.Information("Starting TikTokApi search extraction cycle manager...");
                await _searchExtractionManager.StartAsync();
            }
        }

        public async Task<ServiceStatistics> GetStatisticsAsync()
        {
            try
            {
                var stats = await _processingManager.GetStatisticsAsync();
                var lastExtraction = _extractionMode == ExtractionMode.YtDlp
                    ? _ytDlpExtractionManager.LastExtractionTime
                    : _searchExtractionManager.LastExtractionTime;

                return new ServiceStatistics
                {
                    LastExtractionTime = lastExtraction,
                    PendingCount = stats.PendingCount,
                    ProcessingCount = stats.ProcessingCount,
                    CompletedCount = stats.CompletedCount,
                    FailedCount = stats.FailedCount,
                    LastError = stats.LastError
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting service statistics");
                return new ServiceStatistics { LastError = $"Error getting statistics: {ex.Message}" };
            }
        }
    }
}
