using System;

namespace FluxAnswer.Configuration
{
    /// <summary>
    /// Manages application configuration loading, validation, and reload on file changes.
    /// </summary>
    public interface IConfigurationManager
    {
        string ConfigFilePath { get; }
        string PocketBaseUrl { get; }
        string PocketBaseBindIp { get; }
        int PocketBasePort { get; }
        string PocketBaseAdminEmail { get; }
        string PocketBaseAdminPassword { get; }
        string AssemblyAIApiKey { get; }
        string ResponseApiUrl { get; }
        string ModifyCommentApiUrl { get; }
        string FFmpegPath { get; }
        int ExtractionIntervalMinutes { get; }
        int ProcessingRetryCount { get; }
        int ProcessingPollIntervalSeconds { get; }
        string TempDirectory { get; }
        int CommentsExtractionLimit { get; }
        int CustomCommentsPerBotAccount { get; }
        bool SkipTranscription { get; }
        bool RecreateDatabase { get; }
        bool SeedDataRestoreEnabled { get; }
        string SeedDataDirectory { get; }

        void Reload();
        event EventHandler ConfigurationChanged;
    }
}

