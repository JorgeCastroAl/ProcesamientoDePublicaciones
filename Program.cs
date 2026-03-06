using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using PocketBase.Framework;
using PocketBase.Framework.SchemaSync;
using FluxAnswer.Configuration;
using FluxAnswer.Repositories;
using FluxAnswer.Extraction;
using FluxAnswer.Pipeline.TikTok;
using FluxAnswer.Services;
using FluxAnswer.Services.Api;
using FluxAnswer.Services.AI;
using FluxAnswer.Services.Media;
using FluxAnswer.Services.Scraping.TikTok;
using FluxAnswer.Services.Database;
using FluxAnswer.Services.Pipeline;
using FluxAnswer.SystemTray;

namespace FluxAnswer
{
    class Program
    {
        [STAThread]
        static async Task<int> Main(string[] args)
        {
            // Configure Serilog
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TikTokManager",
                "logs"
            );

            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "video-processing-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            PocketBaseManager? pocketBaseManager = null;

            try
            {
                Log.Information("========== Video Processing System V2 Starting ==========");

                // Load configuration first (needed for PocketBase bind settings and credentials)
                var configPath = ResolveConfigPath();
                var configManager = new ConfigurationManager(configPath);

                if (args.Any(arg => string.Equals(arg, "--test-transcription", StringComparison.OrdinalIgnoreCase)))
                {
                    return await RunTranscriptionSdkTestAsync(configManager);
                }

                // Start PocketBase
                Log.Information("Initializing PocketBase...");
                pocketBaseManager = new PocketBaseManager(
                    bindIp: configManager.PocketBaseBindIp,
                    port: configManager.PocketBasePort);
                var pocketBaseStarted = await pocketBaseManager.StartAsync();
                
                if (!pocketBaseStarted)
                {
                    Log.Error("Failed to start PocketBase. Please ensure pocketbase.exe is available.");
                    MessageBox.Show(
                        "Failed to start PocketBase database.\n\n" +
                        "Please ensure pocketbase.exe is in one of these locations:\n" +
                        "- Current directory\n" +
                        "- Application directory\n" +
                        "- C:\\Users\\YOUR_USERNAME\\AppData\\Local\\TikTokManager\\",
                        "PocketBase Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 1;
                }

                // Sync PocketBase schema with models
                Log.Information("Synchronizing PocketBase schema...");
                
                var pbOptions = new PocketBaseOptions
                {
                    Url = configManager.PocketBaseUrl,
                    AdminEmail = configManager.PocketBaseAdminEmail,
                    AdminPassword = configManager.PocketBaseAdminPassword,
                    EnableAutoSync = true,
                    TimeoutSeconds = 30,
                    RecreateDatabase = configManager.RecreateDatabase,
                    EnableSeedDataRestore = configManager.SeedDataRestoreEnabled,
                    SeedDataDirectory = configManager.SeedDataDirectory
                };
                
                var syncService = new SchemaSyncService(pbOptions);
                var syncSuccess = await syncService.SyncAsync(Assembly.GetExecutingAssembly());
                var shouldBlockStartupOnSyncError = pbOptions.RecreateDatabase && pbOptions.EnableSeedDataRestore;
                
                if (!syncSuccess)
                {
                    var syncFailureReason = syncService.LastError;

                    if (shouldBlockStartupOnSyncError)
                    {
                        Log.Error("Schema synchronization or seed data restoration failed. Application startup aborted. Reason: {SyncFailureReason}", syncFailureReason ?? "n/a");
                        MessageBox.Show(
                            "Schema synchronization or seed data restoration failed.\n\n" +
                            "The service will NOT start to avoid processing with incomplete data.\n" +
                            $"Reason: {syncFailureReason ?? "See logs for details."}",
                            "Startup Blocked",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);

                        if (pocketBaseManager != null)
                        {
                            Log.Information("Stopping PocketBase due to startup failure...");
                            pocketBaseManager.Stop();
                            pocketBaseManager.Dispose();
                            pocketBaseManager = null;
                        }

                        return 1;
                    }

                    Log.Warning("Schema synchronization completed with warnings. Continuing startup because recreate/restore critical mode is disabled.");
                }
                else
                {
                    Log.Information("Schema synchronization completed successfully");
                }

                // Build DI container
                var services = new ServiceCollection();
                ConfigureServices(services, configManager, pbOptions);
                var serviceProvider = services.BuildServiceProvider();

                // Get the main service
                var videoProcessingService = serviceProvider.GetRequiredService<IVideoProcessingService>();

                // Set up global exception handling
                Application.ThreadException += (sender, e) =>
                {
                    Log.Error(e.Exception, "Unhandled UI thread exception");
                    MessageBox.Show($"An error occurred: {e.Exception.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    Log.Fatal(ex, "Unhandled background thread exception");
                };

                // Initialize Windows Forms application
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Create system tray icon
                using var systemTrayIcon = new SystemTrayIcon(
                    videoProcessingService, 
                    serviceProvider.GetRequiredService<IConfigurationManager>(),
                    pocketBaseManager);

                // Capture the UI synchronization context
                var uiContext = System.Threading.SynchronizationContext.Current;

                // Start the service asynchronously (non-blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await videoProcessingService.StartAsync();
                        
                        if (videoProcessingService.State != ServiceState.Running)
                        {
                            Log.Error("Service failed to start");
                            uiContext?.Post(_ =>
                            {
                                MessageBox.Show("Failed to start video processing service. Check logs for details.", 
                                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }, null);
                        }
                        else
                        {
                            Log.Information("Service started successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error starting service");
                        uiContext?.Post(_ =>
                        {
                            MessageBox.Show($"Error starting service: {ex.Message}", 
                                "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }, null);
                    }
                });

                Log.Information("System tray interface loaded, service starting in background...");

                // Run the Windows Forms message loop
                Application.Run();

                // Graceful shutdown
                Log.Information("Stopping service...");
                await videoProcessingService.StopAsync();
                Log.Information("Service stopped successfully");

                // Stop PocketBase
                if (pocketBaseManager != null)
                {
                    Log.Information("Stopping PocketBase...");
                    pocketBaseManager.Stop();
                    pocketBaseManager.Dispose();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                return 1;
            }
            finally
            {
                // Ensure PocketBase is stopped
                pocketBaseManager?.Dispose();
                Log.CloseAndFlush();
            }
        }

        private static string ResolveConfigPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var devConfigPath = Path.Combine(baseDirectory, "settings.json");

            if (IsDevelopmentMode(baseDirectory))
            {
                Log.Information("Running in Development mode. Using configuration file: {ConfigPath}", devConfigPath);
                return devConfigPath;
            }

            return EnsureLocalAppDataConfig(baseDirectory);
        }

        private static bool IsDevelopmentMode(string baseDirectory)
        {
            var environmentOverride = Environment.GetEnvironmentVariable("FLUXANSWER_ENVIRONMENT");

            if (string.Equals(environmentOverride, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(environmentOverride, "Production", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (Debugger.IsAttached)
            {
                return true;
            }

            var binMarker = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
            return baseDirectory.IndexOf(binMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string EnsureLocalAppDataConfig(string baseDirectory)
        {
            var localConfigDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TikTokManager");
            Directory.CreateDirectory(localConfigDirectory);

            var localConfigPath = Path.Combine(localConfigDirectory, "settings.json");
            if (!File.Exists(localConfigPath))
            {
                var sourceConfigPath = Path.Combine(baseDirectory, "settings.json");
                if (File.Exists(sourceConfigPath))
                {
                    File.Copy(sourceConfigPath, localConfigPath, overwrite: false);
                }
                else
                {
                    var examplePath = Path.Combine(baseDirectory, "settings.example.json");
                    if (File.Exists(examplePath))
                    {
                        File.Copy(examplePath, localConfigPath, overwrite: false);
                    }
                }
            }

            Log.Information("Running in Production mode. Using per-user configuration file: {ConfigPath}", localConfigPath);
            return localConfigPath;
        }

        private static async Task<int> RunTranscriptionSdkTestAsync(IConfigurationManager config)
        {
            Log.Information("========== Running Transcription SDK Test ==========");
            Console.WriteLine("[TEST] Running transcription integration test with official AssemblyAI SDK...");
            Console.WriteLine($"[TEST] Config path: {config.ConfigFilePath}");

            if (string.IsNullOrWhiteSpace(config.AssemblyAIApiKey) ||
                string.Equals(config.AssemblyAIApiKey, "YOUR_ASSEMBLYAI_API_KEY_HERE", StringComparison.OrdinalIgnoreCase))
            {
                const string message = "AssemblyAI API key is not configured in active settings.json.";
                Log.Error("[TEST] {Message}", message);
                Console.WriteLine("[TEST][FAIL] " + message);
                return 1;
            }

            if (string.IsNullOrWhiteSpace(config.TempDirectory) || !Directory.Exists(config.TempDirectory))
            {
                var message = $"Temp directory not found: {config.TempDirectory}";
                Log.Error("[TEST] {Message}", message);
                Console.WriteLine("[TEST][FAIL] " + message);
                return 1;
            }

            var testMp3Path = Directory
                .GetFiles(config.TempDirectory, "*.mp3", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(testMp3Path))
            {
                var message = $"No MP3 files found in temp directory: {config.TempDirectory}";
                Log.Error("[TEST] {Message}", message);
                Console.WriteLine("[TEST][FAIL] " + message);
                return 1;
            }

            Console.WriteLine($"[TEST] Using MP3: {testMp3Path}");
            Log.Information("[TEST] Using MP3: {Mp3Path}", testMp3Path);

            try
            {
                var transcriptionService = new TranscriptionService(config.AssemblyAIApiKey);
                var transcript = await transcriptionService.TranscribeAsync(testMp3Path);

                var preview = transcript.Length > 220 ? transcript.Substring(0, 220) + "..." : transcript;
                Console.WriteLine($"[TEST][OK] Transcript length: {transcript.Length}");
                Console.WriteLine($"[TEST][OK] Transcript preview: {preview}");
                Log.Information("[TEST] Transcription successful. Length={Length}", transcript.Length);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEST][FAIL] {ex.Message}");
                Log.Error(ex, "[TEST] Transcription SDK test failed");
                return 1;
            }
        }

        private static void ConfigureServices(IServiceCollection services, ConfigurationManager configManager, PocketBaseOptions pbOptions)
        {
            // Configuration
            services.AddSingleton<IConfigurationManager>(configManager);
            services.AddSingleton<StartupValidator>();

            // Configure PocketBase Framework
            services.AddSingleton(pbOptions);

            // Repositories
            services.AddSingleton<IVideoRepo>(sp => 
                new VideoRepo(sp.GetRequiredService<PocketBaseOptions>()));
            services.AddSingleton<IBotAccountVideoRepo>(sp =>
                new BotAccountVideoRepo(sp.GetRequiredService<PocketBaseOptions>()));
            services.AddSingleton<IBotAccountRepo>(sp => 
                new BotAccountRepo(sp.GetRequiredService<PocketBaseOptions>()));
            services.AddSingleton<IAccountToFollowRepo>(sp => 
                new AccountToFollowRepo(sp.GetRequiredService<PocketBaseOptions>()));
            services.AddSingleton<ISocialNetworkRepo>(sp =>
                new SocialNetworkRepo(sp.GetRequiredService<PocketBaseOptions>()));

            // Extraction services
            services.AddSingleton<IYtDlpWrapper, YtDlpWrapperV2>();
            services.AddSingleton<IVideoExtractionService>(sp =>
            {
                var videoRepo = sp.GetRequiredService<IVideoRepo>();
                var ytDlp = sp.GetRequiredService<IYtDlpWrapper>();
                var socialNetworkRepo = sp.GetRequiredService<ISocialNetworkRepo>();
                return new VideoExtractionService(videoRepo, ytDlp, socialNetworkRepo);
            });
            services.AddSingleton<IExtractionCycleManager>(sp =>
            {
                var accountRepo = sp.GetRequiredService<IAccountToFollowRepo>();
                var extractionService = sp.GetRequiredService<IVideoExtractionService>();
                var config = sp.GetRequiredService<IConfigurationManager>();
                return new ExtractionCycleManager(accountRepo, extractionService, config);
            });

            // Processing services
            services.AddSingleton<IAudioDownloadService, AudioDownloadService>();
            services.AddSingleton<ICommentsExtractionService, CommentsExtractionService>();
            services.AddSingleton<ITranscriptionService>(sp =>
            {
                var config = sp.GetRequiredService<IConfigurationManager>();
                return new TranscriptionService(config.AssemblyAIApiKey);
            });
            services.AddSingleton<IAudioStageService, AudioStageService>();
            services.AddSingleton<ITranscriptionStageService, TranscriptionStageService>();
            services.AddSingleton<IBotAccountVideoStageService, BotAccountVideoStageService>();
            services.AddSingleton<IResponseGenerationService>(sp =>
            {
                var config = sp.GetRequiredService<IConfigurationManager>();
                var botAccountRepo = sp.GetRequiredService<IBotAccountRepo>();
                var botAccountVideoRepo = sp.GetRequiredService<IBotAccountVideoRepo>();
                return new ResponseGenerationService(
                    config.ResponseApiUrl,
                    config.ModifyCommentApiUrl,
                    botAccountRepo,
                    botAccountVideoRepo);
            });
            services.AddSingleton<IVideoResponseStageService>(sp =>
            {
                var videoRepo = sp.GetRequiredService<IVideoRepo>();
                var responseService = sp.GetRequiredService<IResponseGenerationService>();
                return new VideoResponseStageService(videoRepo, responseService);
            });
            services.AddSingleton<ITikTokPipelineManager>(sp =>
            {
                var videoRepo = sp.GetRequiredService<IVideoRepo>();
                var commentsService = sp.GetRequiredService<ICommentsExtractionService>();
                var audioStageService = sp.GetRequiredService<IAudioStageService>();
                var transcriptionStageService = sp.GetRequiredService<ITranscriptionStageService>();
                var responseStageService = sp.GetRequiredService<IVideoResponseStageService>();
                var botAccountVideoStageService = sp.GetRequiredService<IBotAccountVideoStageService>();
                var config = sp.GetRequiredService<IConfigurationManager>();
                return new TikTokPipelineManager(
                    videoRepo,
                    commentsService,
                    audioStageService,
                    transcriptionStageService,
                    responseStageService,
                    botAccountVideoStageService,
                    config
                );
            });

            // Main service
            services.AddSingleton<IVideoProcessingService>(sp =>
            {
                var validator = sp.GetRequiredService<StartupValidator>();
                var extractionManager = sp.GetRequiredService<IExtractionCycleManager>();
                var processingManager = sp.GetRequiredService<ITikTokPipelineManager>();
                return new VideoProcessingService(validator, extractionManager, processingManager);
            });
        }
    }
}



