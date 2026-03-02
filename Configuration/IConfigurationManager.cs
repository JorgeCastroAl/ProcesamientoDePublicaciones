using System;

namespace VideoProcessingSystemV2.Configuration
{
    /// <summary>
    /// Manages application configuration loading, validation, and reload on file changes.
    /// </summary>
    public interface IConfigurationManager
    {
        string PocketBaseUrl { get; }
        string PocketBaseAdminEmail { get; }
        string PocketBaseAdminPassword { get; }
        string AssemblyAIApiKey { get; }
        string ResponseApiUrl { get; }
        string FFmpegPath { get; }
        int ExtractionIntervalMinutes { get; }
        int ProcessingRetryCount { get; }
        int ProcessingPollIntervalSeconds { get; }
        string TempDirectory { get; }
        int CommentsExtractionLimit { get; }
        bool SkipTranscription { get; }
        bool RecreateDatabase { get; }
        bool SeedDataRestoreEnabled { get; }
        string SeedDataDirectory { get; }

        void Reload();
        event EventHandler ConfigurationChanged;
    }
}
