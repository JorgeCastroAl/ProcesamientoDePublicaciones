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
    /// </summary>
    public class VideoProcessingService : IVideoProcessingService
    {
        private readonly StartupValidator _validator;
        private readonly IExtractionCycleManager _extractionManager;
        private readonly ITikTokPipelineManager _processingManager;
        private ServiceState _state;
        private readonly object _lock = new object();

        public ServiceState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
            private set
            {
                lock (_lock)
                {
                    var oldState = _state;
                    _state = value;
                    if (oldState != value)
                    {
                        StateChanged?.Invoke(this, new ServiceStateChangedEventArgs(oldState, value));
                    }
                }
            }
        }

        public event EventHandler<ServiceStateChangedEventArgs>? StateChanged;
        public event EventHandler<ErrorEventArgs>? ErrorOccurred;

        public VideoProcessingService(
            StartupValidator validator,
            IExtractionCycleManager extractionManager,
            ITikTokPipelineManager processingManager)
        {
            _validator = validator;
            _extractionManager = extractionManager;
            _processingManager = processingManager;
            _state = ServiceState.Stopped;
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
                Log.Information("========== Starting Video Processing Service ==========");

                // Validate startup requirements
                var isValid = await _validator.ValidateStartupRequirementsAsync();
                if (!isValid)
                {
                    State = ServiceState.Error;
                    var errorMsg = "Startup validation failed";
                    Log.Error(errorMsg);
                    ErrorOccurred?.Invoke(this, new ErrorEventArgs(errorMsg));
                    return;
                }

                // Start extraction cycle manager
                Log.Information("Starting extraction cycle manager...");
                await _extractionManager.StartAsync();

                // Start processing pipeline manager
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

                // Stop extraction cycle manager
                Log.Information("Stopping extraction cycle manager...");
                await _extractionManager.StopAsync();

                // Stop processing pipeline manager (with timeout)
                Log.Information("Stopping processing pipeline manager...");
                var stopTask = _processingManager.StopAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));

                var completedTask = await Task.WhenAny(stopTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    Log.Warning("Processing pipeline manager did not stop within 2 minutes");
                }

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

        public async Task<ServiceStatistics> GetStatisticsAsync()
        {
            try
            {
                var stats = await _processingManager.GetStatisticsAsync();
                return new ServiceStatistics
                {
                    LastExtractionTime = _extractionManager.LastExtractionTime,
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
                return new ServiceStatistics
                {
                    LastError = $"Error getting statistics: {ex.Message}"
                };
            }
        }
    }
}



