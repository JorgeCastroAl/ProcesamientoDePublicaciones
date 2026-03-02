using Serilog;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketBase.Framework;
using PocketBase.Framework.SchemaSync;
using VideoProcessingSystemV2.Configuration;
using VideoProcessingSystemV2.Repositories;
using VideoProcessingSystemV2.Extraction;
using VideoProcessingSystemV2.Processing;
using VideoProcessingSystemV2.Services;
using VideoProcessingSystemV2.SystemTray;

namespace VideoProcessingSystemV2
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

                // Start PocketBase
                Log.Information("Initializing PocketBase...");
                pocketBaseManager = new PocketBaseManager();
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
                
                // Load configuration first to get PocketBase credentials
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                var configManager = new ConfigurationManager(configPath);
                
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
                    if (shouldBlockStartupOnSyncError)
                    {
                        Log.Error("Schema synchronization or seed data restoration failed. Application startup aborted.");
                        MessageBox.Show(
                            "Schema synchronization or seed data restoration failed.\n\n" +
                            "The service will NOT start to avoid processing with incomplete data.\n" +
                            "Check logs and fix restore errors before retrying.",
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

                    if (pbOptions.RecreateDatabase && pbOptions.EnableSeedDataRestore)
                    {
                        TryDisableRecreateDatabaseFlag(configPath);
                    }
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
            services.AddSingleton<ICommentRepo>(sp => 
                new CommentRepo(sp.GetRequiredService<PocketBaseOptions>()));
            services.AddSingleton<IResponseRepo>(sp => 
                new ResponseRepo(sp.GetRequiredService<PocketBaseOptions>()));
            services.AddSingleton<IBotAccountRepo>(sp => 
                new BotAccountRepo(sp.GetRequiredService<PocketBaseOptions>()));
            services.AddSingleton<IAccountToFollowRepo>(sp => 
                new AccountToFollowRepo(sp.GetRequiredService<PocketBaseOptions>()));

            // Extraction services
            services.AddSingleton<IYtDlpWrapper, YtDlpWrapperV2>();
            services.AddSingleton<IVideoExtractionService>(sp =>
            {
                var videoRepo = sp.GetRequiredService<IVideoRepo>();
                var ytDlp = sp.GetRequiredService<IYtDlpWrapper>();
                return new VideoExtractionService(videoRepo, ytDlp);
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
            services.AddSingleton<IResponseGenerationService>(sp =>
            {
                var config = sp.GetRequiredService<IConfigurationManager>();
                return new ResponseGenerationService(config.ResponseApiUrl);
            });
            services.AddSingleton<IProcessingPipelineManager>(sp =>
            {
                var videoRepo = sp.GetRequiredService<IVideoRepo>();
                var commentRepo = sp.GetRequiredService<ICommentRepo>();
                var responseRepo = sp.GetRequiredService<IResponseRepo>();
                var audioService = sp.GetRequiredService<IAudioDownloadService>();
                var commentsService = sp.GetRequiredService<ICommentsExtractionService>();
                var transcriptionService = sp.GetRequiredService<ITranscriptionService>();
                var responseService = sp.GetRequiredService<IResponseGenerationService>();
                var config = sp.GetRequiredService<IConfigurationManager>();
                return new ProcessingPipelineManager(
                    videoRepo,
                    commentRepo,
                    responseRepo,
                    audioService,
                    commentsService,
                    transcriptionService,
                    responseService,
                    config
                );
            });

            // Main service
            services.AddSingleton<IVideoProcessingService>(sp =>
            {
                var validator = sp.GetRequiredService<StartupValidator>();
                var extractionManager = sp.GetRequiredService<IExtractionCycleManager>();
                var processingManager = sp.GetRequiredService<IProcessingPipelineManager>();
                return new VideoProcessingService(validator, extractionManager, processingManager);
            });
        }

        private static void TryDisableRecreateDatabaseFlag(string configPath)
        {
            try
            {
                var updatedAny = false;

                if (UpdateRecreateFlagInSettingsFile(configPath))
                {
                    updatedAny = true;
                    Log.Information("Auto-updated runtime settings.json: recreate_database set to false ({ConfigPath})", configPath);
                }

                var projectSettingsPath = FindProjectSettingsPath(configPath);
                if (!string.IsNullOrWhiteSpace(projectSettingsPath) &&
                    !string.Equals(projectSettingsPath, configPath, StringComparison.OrdinalIgnoreCase) &&
                    UpdateRecreateFlagInSettingsFile(projectSettingsPath))
                {
                    updatedAny = true;
                    Log.Information("Auto-updated project settings.json: recreate_database set to false ({ProjectSettingsPath})", projectSettingsPath);
                }

                if (!updatedAny)
                {
                    Log.Information("recreate_database already false or settings file not found; no auto-update needed");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to auto-update recreate_database in settings.json");
            }
        }

        private static bool UpdateRecreateFlagInSettingsFile(string settingsPath)
        {
            if (!File.Exists(settingsPath))
                return false;

            var json = File.ReadAllText(settingsPath);
            var settings = JObject.Parse(json);
            var currentValue = settings["recreate_database"]?.Value<bool>() ?? false;
            if (!currentValue)
                return false;

            settings["recreate_database"] = false;
            File.WriteAllText(settingsPath, settings.ToString(Formatting.Indented));
            return true;
        }

        private static string? FindProjectSettingsPath(string runtimeConfigPath)
        {
            var currentDirectory = Path.GetDirectoryName(runtimeConfigPath);
            while (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                var csprojPath = Path.Combine(currentDirectory, "ProcesamientoDePublicaciones.csproj");
                if (File.Exists(csprojPath))
                {
                    var settingsPath = Path.Combine(currentDirectory, "settings.json");
                    return settingsPath;
                }

                currentDirectory = Path.GetDirectoryName(currentDirectory);
            }

            return null;
        }
    }
}
