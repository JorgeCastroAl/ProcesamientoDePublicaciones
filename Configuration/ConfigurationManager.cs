using System;
using System.IO;
using Newtonsoft.Json;
using Serilog;

namespace FluxAnswer.Configuration
{
    /// <summary>
    /// Manages application configuration with JSON file loading and automatic reload.
    /// </summary>
    public class ConfigurationManager : IConfigurationManager, IDisposable
    {
        private readonly string _configFilePath;
        private readonly FileSystemWatcher _fileWatcher;
        private AppSettings _settings;
        private readonly object _lock = new object();

        // Default values
        private const int DefaultExtractionIntervalMinutes = 60;
        private const int DefaultProcessingRetryCount = 3;
        private const int DefaultProcessingPollIntervalSeconds = 30;
        private const int DefaultCommentsExtractionLimit = 12;
        private const int DefaultCustomCommentsPerBotAccount = 0;
        private const bool DefaultSkipTranscription = false;
        private const bool DefaultRecreateDatabase = false;
        private const bool DefaultSeedDataRestoreEnabled = false;
        private const string DefaultSeedDataDirectory = "SeedData";
        private const string DefaultPocketBaseBindIp = "0.0.0.0";
        private const int DefaultPocketBasePort = 8090;

        public event EventHandler? ConfigurationChanged;
        public string ConfigFilePath => _configFilePath;

        public string PocketBaseUrl => _settings.PocketBaseUrl ?? string.Empty;
        public string PocketBaseBindIp => string.IsNullOrWhiteSpace(_settings.PocketBaseBindIp)
            ? DefaultPocketBaseBindIp
            : _settings.PocketBaseBindIp;
        public int PocketBasePort => _settings.PocketBasePort is > 0 and <= 65535
            ? _settings.PocketBasePort.Value
            : DefaultPocketBasePort;
        public string PocketBasePath => _settings.PocketBasePath ?? string.Empty;
        public string PocketBaseAdminEmail => _settings.PocketBaseAdminEmail ?? string.Empty;
        public string PocketBaseAdminPassword => _settings.PocketBaseAdminPassword ?? string.Empty;
        public string AssemblyAIApiKey => _settings.AssemblyAIApiKey ?? string.Empty;
        public string ResponseApiUrl => _settings.ResponseApiUrl ?? string.Empty;
        public string ModifyCommentApiUrl => string.IsNullOrWhiteSpace(_settings.ModifyCommentApiUrl)
            ? BuildModifyCommentApiUrl(_settings.ResponseApiUrl)
            : _settings.ModifyCommentApiUrl;
        public string FFmpegPath => _settings.FFmpegPath ?? "ffmpeg.exe";
        public int ExtractionIntervalMinutes => _settings.ExtractionIntervalMinutes ?? DefaultExtractionIntervalMinutes;
        public int ProcessingRetryCount => _settings.ProcessingRetryCount ?? DefaultProcessingRetryCount;
        public int ProcessingPollIntervalSeconds => _settings.ProcessingPollIntervalSeconds ?? DefaultProcessingPollIntervalSeconds;
        public string TempDirectory => _settings.TempDirectory ?? string.Empty;
        public int CommentsExtractionLimit => _settings.CommentsExtractionLimit ?? DefaultCommentsExtractionLimit;
        public int CustomCommentsPerBotAccount => _settings.CustomCommentsPerBotAccount ?? DefaultCustomCommentsPerBotAccount;
        public bool SkipTranscription => _settings.SkipTranscription ?? DefaultSkipTranscription;
        public bool RecreateDatabase => _settings.RecreateDatabase ?? DefaultRecreateDatabase;
        public bool SeedDataRestoreEnabled => _settings.SeedDataRestoreEnabled ?? DefaultSeedDataRestoreEnabled;
        public string SeedDataDirectory => _settings.SeedDataDirectory ?? DefaultSeedDataDirectory;

        public ConfigurationManager(string configFilePath)
        {
            _configFilePath = configFilePath;
            _settings = new AppSettings();

            // Load initial configuration
            Load();

            // Set up file watcher for automatic reload
            var directory = Path.GetDirectoryName(_configFilePath);
            var fileName = Path.GetFileName(_configFilePath);

            if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(fileName))
            {
                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                _fileWatcher.Changed += OnConfigFileChanged;
                _fileWatcher.EnableRaisingEvents = true;

                Log.Information("Configuration file watcher initialized for: {ConfigFile}", _configFilePath);
            }
            else
            {
                throw new ArgumentException("Invalid configuration file path", nameof(configFilePath));
            }
        }

        private void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_configFilePath))
                    {
                        Log.Warning("Configuration file not found: {ConfigFile}. Using default values.", _configFilePath);
                        _settings = new AppSettings();
                        return;
                    }

                    var json = File.ReadAllText(_configFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if (settings == null)
                    {
                        Log.Warning("Failed to deserialize configuration. Using default values.");
                        _settings = new AppSettings();
                        return;
                    }

                    _settings = settings;

                    Log.Information("Configuration loaded successfully from: {ConfigFile}", _configFilePath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error loading configuration from: {ConfigFile}", _configFilePath);
                    throw new InvalidOperationException($"Failed to load configuration from {_configFilePath}", ex);
                }
            }
        }

        public void Reload()
        {
            Log.Information("Reloading configuration...");
            Load();
            OnConfigurationChanged();
        }

        public void DisableOneTimeDatabaseRestoreFlags()
        {
            lock (_lock)
            {
                if ((_settings.RecreateDatabase ?? false) == false && (_settings.SeedDataRestoreEnabled ?? false) == false)
                {
                    return;
                }

                _settings.RecreateDatabase = false;
                _settings.SeedDataRestoreEnabled = false;

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);

                Log.Information("Configuration updated: recreate_database=false and seed_data_restore_enabled=false after successful restore.");
            }

            OnConfigurationChanged();
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce multiple file change events
            System.Threading.Thread.Sleep(100);

            try
            {
                Log.Information("Configuration file changed, reloading...");
                Reload();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reloading configuration after file change");
            }
        }

        private void OnConfigurationChanged()
        {
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
        }

        private static string BuildModifyCommentApiUrl(string? generateCommentApiUrl)
        {
            if (string.IsNullOrWhiteSpace(generateCommentApiUrl))
            {
                return string.Empty;
            }

            const string generatePath = "/api/opinion/generate-comment";
            const string modifyPath = "/api/opinion/modify-comment";

            if (generateCommentApiUrl.EndsWith(generatePath, StringComparison.OrdinalIgnoreCase))
            {
                return generateCommentApiUrl.Substring(0, generateCommentApiUrl.Length - generatePath.Length) + modifyPath;
            }

            if (generateCommentApiUrl.EndsWith("/", StringComparison.Ordinal))
            {
                return generateCommentApiUrl.TrimEnd('/') + modifyPath;
            }

            return generateCommentApiUrl + modifyPath;
        }
    }
}

