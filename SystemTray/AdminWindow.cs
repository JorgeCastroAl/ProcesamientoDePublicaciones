using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FluxAnswer.Configuration;
using FluxAnswer.Services;
using Serilog;

namespace FluxAnswer.SystemTray
{
    public class AdminWindow : Form
    {
        private readonly IVideoProcessingService _service;
        private readonly IConfigurationManager _config;
        private readonly PocketBaseManager _pocketBaseManager;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly ToolTip _toolTip = new ToolTip();
        
        // Status labels
        private Label _lastExtractionLabel = null!;
        private Label _pendingLabel = null!;
        private Label _processingLabel = null!;
        private Label _completedLabel = null!;
        private Label _failedLabel = null!;
        
        // Service control buttons
        private Button _startServiceBtn = null!;
        private Button _stopServiceBtn = null!;
        private Button _restartServiceBtn = null!;
        private Button _startPbBtn = null!;
        private Button _stopPbBtn = null!;
        private Button _restartPbBtn = null!;
        
        // Configuration controls
        private NumericUpDown _commentsLimitInput = null!;
        private CheckBox _skipTranscriptionCheckbox = null!;
        private TextBox _tempDirectoryInput = null!;
        private TextBox _apiKeyInput = null!;
        private TextBox _responseApiUrlInput = null!;
        private TextBox _modifyCommentApiUrlInput = null!;

        public AdminWindow(
            IVideoProcessingService service, 
            IConfigurationManager config,
            PocketBaseManager pocketBaseManager)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _pocketBaseManager = pocketBaseManager ?? throw new ArgumentNullException(nameof(pocketBaseManager));
            
            InitializeComponents();
            LoadConfiguration();
            
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();
            
            _ = UpdateStatistics();
        }

        private void InitializeComponents()
        {
            Text = "FluxAnswer - Administration";
            Size = new Size(585, 578);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = BrandingAssets.GetApplicationIcon();

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10, 4, 10, 10)
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Branding Header
            mainPanel.Controls.Add(CreateBrandingHeader(), 0, 0);

            // Status Section
            mainPanel.Controls.Add(CreateStatusSection(), 0, 1);
            
            // Service Control Section
            mainPanel.Controls.Add(CreateServiceControlSection(), 0, 2);
            
            // Configuration Section
            mainPanel.Controls.Add(CreateConfigurationSection(), 0, 3);
            
            // Data Management Section
            mainPanel.Controls.Add(CreateDataManagementSection(), 0, 4);

            Controls.Add(mainPanel);
        }

        private Panel CreateBrandingHeader()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(6, 0, 6, 4)
            };

            var logo = new PictureBox
            {
                Width = 44,
                Height = 44,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = BrandingAssets.GetLogoImage(44, 44),
                Left = 2,
                Top = 3
            };

            var title = new Label
            {
                Text = "FluxAnswer",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Left = 54,
                Top = 7
            };

            var owner = new Label
            {
                Text = "Owner: " + BrandingAssets.OwnerName,
                AutoSize = true,
                ForeColor = Color.DimGray,
                Left = 54,
                Top = 26
            };

            panel.Controls.Add(logo);
            panel.Controls.Add(title);
            panel.Controls.Add(owner);
            return panel;
        }

        private GroupBox CreateStatusSection()
        {
            var group = new GroupBox
            {
                Text = "System Status",
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                Height = 85
            };

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                AutoSize = true
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            // Row 0
            _pendingLabel = AddStatusRow(panel, 0, 0, "Pending:");
            _processingLabel = AddStatusRow(panel, 0, 2, "Processing:");
            
            // Row 1
            _completedLabel = AddStatusRow(panel, 1, 0, "Completed:");
            _failedLabel = AddStatusRow(panel, 1, 2, "Failed:");
            
            // Row 2
            _lastExtractionLabel = AddStatusRow(panel, 2, 0, "Last Extract:");
            panel.SetColumnSpan(_lastExtractionLabel, 2);

            group.Controls.Add(panel);
            return group;
        }

        private Label AddStatusRow(TableLayoutPanel panel, int row, int col, string labelText)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold),
                Padding = new Padding(3)
            };

            var valueLabel = new Label
            {
                Text = "...",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(3)
            };

            panel.Controls.Add(label, col, row);
            panel.Controls.Add(valueLabel, col + 1, row);

            return valueLabel;
        }

        private GroupBox CreateServiceControlSection()
        {
            var group = new GroupBox
            {
                Text = "Service Control",
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                Height = 60
            };

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));

            // Processing controls
            var serviceLabel = new Label
            {
                Text = "Processing:",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(5, 6, 5, 5)
            };
            var serviceFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _startServiceBtn = CreateIconButton("▶", OnStartService, Color.FromArgb(0, 120, 0), "Start processing service");
            _stopServiceBtn = CreateIconButton("■", OnStopService, Color.FromArgb(180, 0, 0), "Stop processing service");
            _restartServiceBtn = CreateIconButton("↻", OnRestartService, Color.FromArgb(200, 120, 0), "Restart processing service");
            
            serviceFlow.Controls.Add(_startServiceBtn);
            serviceFlow.Controls.Add(_stopServiceBtn);
            serviceFlow.Controls.Add(_restartServiceBtn);
            
            panel.Controls.Add(serviceLabel, 0, 0);
            panel.Controls.Add(serviceFlow, 1, 0);

            // Database controls
            var pbLabel = new Label
            {
                Text = "Database:",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(5, 6, 5, 5)
            };
            var pbFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _startPbBtn = CreateIconButton("▶", OnStartPocketBase, Color.FromArgb(0, 100, 0), "Start database");
            _stopPbBtn = CreateIconButton("■", OnStopPocketBase, Color.FromArgb(150, 0, 0), "Stop database");
            _restartPbBtn = CreateIconButton("↻", OnRestartPocketBase, Color.FromArgb(180, 100, 0), "Restart database");
            
            pbFlow.Controls.Add(_startPbBtn);
            pbFlow.Controls.Add(_stopPbBtn);
            pbFlow.Controls.Add(_restartPbBtn);
            
            panel.Controls.Add(pbLabel, 2, 0);
            panel.Controls.Add(pbFlow, 3, 0);

            group.Controls.Add(panel);
            return group;
        }

        private Button CreateIconButton(string icon, EventHandler onClick, Color iconColor, string tooltip)
        {
            var btn = new Button
            {
                Text = icon,
                Width = 32,
                Height = 28,
                Margin = new Padding(2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = iconColor,
                Font = new Font("Segoe UI Symbol", 11F, FontStyle.Bold),
                TabStop = true,
                UseVisualStyleBackColor = false
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 0, 0, 0);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(35, 0, 0, 0);
            _toolTip.SetToolTip(btn, tooltip);
            btn.Click += onClick;
            return btn;
        }

        private GroupBox CreateConfigurationSection()
        {
            var group = new GroupBox
            {
                Text = "Configuration",
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                Height = 180
            };

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 5,
                AutoSize = true
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Row 0: Comments + Skip Transcription
            AddConfigLabel(panel, 0, 0, "Comments:");
            _commentsLimitInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 50,
                Value = 12,
                Width = 60,
                Anchor = AnchorStyles.Left
            };
            panel.Controls.Add(_commentsLimitInput, 1, 0);

            var skipLabel = new Label
            {
                Text = "Skip Transcription:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Regular),
                Padding = new Padding(5, 8, 5, 5)
            };
            panel.Controls.Add(skipLabel, 2, 0);
            
            _skipTranscriptionCheckbox = new CheckBox
            {
                Text = "Yes",
                Anchor = AnchorStyles.Left,
                AutoSize = true
            };
            panel.Controls.Add(_skipTranscriptionCheckbox, 3, 0);

            // Row 1: Audio Folder
            AddConfigLabel(panel, 1, 0, "Audio Folder:");
            var tempDirPanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            _tempDirectoryInput = new TextBox { Width = 340 };
            var browseTempBtn = new Button { Text = "...", Width = 35, Height = 22 };
            browseTempBtn.Click += OnBrowseTempDirectory;
            tempDirPanel.Controls.Add(_tempDirectoryInput);
            tempDirPanel.Controls.Add(browseTempBtn);
            panel.Controls.Add(tempDirPanel, 1, 1);
            panel.SetColumnSpan(tempDirPanel, 3);

            // Row 2: API Key
            AddConfigLabel(panel, 2, 0, "API Key:");
            _apiKeyInput = new TextBox
            {
                Width = 380,
                Anchor = AnchorStyles.Left,
                PasswordChar = '*'
            };
            panel.Controls.Add(_apiKeyInput, 1, 2);
            panel.SetColumnSpan(_apiKeyInput, 3);

            // Row 3: Response URL
            AddConfigLabel(panel, 3, 0, "Response URL:");
            _responseApiUrlInput = new TextBox
            {
                Width = 380,
                Anchor = AnchorStyles.Left
            };
            panel.Controls.Add(_responseApiUrlInput, 1, 3);
            panel.SetColumnSpan(_responseApiUrlInput, 3);

            // Row 4: Modify Comment URL
            AddConfigLabel(panel, 4, 0, "Modify URL:");
            _modifyCommentApiUrlInput = new TextBox
            {
                Width = 380,
                Anchor = AnchorStyles.Left
            };
            panel.Controls.Add(_modifyCommentApiUrlInput, 1, 4);
            panel.SetColumnSpan(_modifyCommentApiUrlInput, 3);

            group.Controls.Add(panel);
            return group;
        }

        private void AddConfigLabel(TableLayoutPanel panel, int row, int col, string text)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Regular),
                Padding = new Padding(5, 8, 5, 5)
            };
            panel.Controls.Add(label, col, row);
        }

        private GroupBox CreateDataManagementSection()
        {
            var group = new GroupBox
            {
                Text = "Data Management",
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                Height = 60
            };

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var clearDataBtn = CreateButton("Clear Data", OnClearData, Color.FromArgb(180, 0, 0));
            clearDataBtn.Width = 120;
            clearDataBtn.Anchor = AnchorStyles.Left;
            
            var openAdminBtn = CreateButton("Admin Panel", OnOpenPocketBaseAdmin, Color.FromArgb(0, 80, 150));
            openAdminBtn.Width = 120;
            openAdminBtn.Anchor = AnchorStyles.Left;

            // Save button in the right corner
            var saveBtn = new Button
            {
                Text = "Save",
                Width = 100,
                Height = 28,
                BackColor = Color.DodgerBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0)
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += OnSaveConfiguration;

            panel.Controls.Add(clearDataBtn, 0, 0);
            panel.Controls.Add(openAdminBtn, 1, 0);
            panel.Controls.Add(saveBtn, 2, 0);

            group.Controls.Add(panel);
            return group;
        }

        private Button CreateButton(string text, EventHandler onClick, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Width = 85,
                Height = 28,
                Margin = new Padding(2),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 8.5F)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private void LoadConfiguration()
        {
            _commentsLimitInput.Value = _config.CommentsExtractionLimit;
            _skipTranscriptionCheckbox.Checked = _config.SkipTranscription;
            _tempDirectoryInput.Text = _config.TempDirectory;
            _apiKeyInput.Text = _config.AssemblyAIApiKey;
            _responseApiUrlInput.Text = _config.ResponseApiUrl;
            _modifyCommentApiUrlInput.Text = _config.ModifyCommentApiUrl;
        }

        private async void OnRefreshTick(object? sender, EventArgs e)
        {
            await UpdateStatistics();
        }

        private async Task UpdateStatistics()
        {
            try
            {
                // Service state - update buttons
                var serviceState = _service.State;
                UpdateServiceButtons(serviceState == ServiceState.Running);

                // PocketBase state - update buttons
                var pbRunning = await IsPocketBaseRunningAsync();
                UpdateDatabaseButtons(pbRunning);

                // Statistics
                var stats = await _service.GetStatisticsAsync();
                _lastExtractionLabel.Text = stats.LastExtractionTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";
                _pendingLabel.Text = stats.PendingCount.ToString();
                _processingLabel.Text = stats.ProcessingCount.ToString();
                _completedLabel.Text = stats.CompletedCount.ToString();
                _failedLabel.Text = stats.FailedCount.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating statistics");
            }
        }

        private void UpdateServiceButtons(bool isRunning)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateServiceButtons(isRunning)));
                return;
            }

            if (isRunning)
            {
                // Service is running - disable Start, enable Stop and Restart
                SetIconButtonState(_startServiceBtn, false, Color.FromArgb(0, 120, 0));
                SetIconButtonState(_stopServiceBtn, true, Color.FromArgb(180, 0, 0));
                SetIconButtonState(_restartServiceBtn, true, Color.FromArgb(200, 120, 0));
            }
            else
            {
                // Service is stopped - enable Start, disable Stop and Restart
                SetIconButtonState(_startServiceBtn, true, Color.FromArgb(0, 120, 0));
                SetIconButtonState(_stopServiceBtn, false, Color.FromArgb(180, 0, 0));
                SetIconButtonState(_restartServiceBtn, false, Color.FromArgb(200, 120, 0));
            }
        }

        private void UpdateDatabaseButtons(bool isRunning)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateDatabaseButtons(isRunning)));
                return;
            }

            if (isRunning)
            {
                // Database is running - disable Start, enable Stop and Restart
                SetIconButtonState(_startPbBtn, false, Color.FromArgb(0, 100, 0));
                SetIconButtonState(_stopPbBtn, true, Color.FromArgb(150, 0, 0));
                SetIconButtonState(_restartPbBtn, true, Color.FromArgb(180, 100, 0));
            }
            else
            {
                // Database is stopped - enable Start, disable Stop and Restart
                SetIconButtonState(_startPbBtn, true, Color.FromArgb(0, 100, 0));
                SetIconButtonState(_stopPbBtn, false, Color.FromArgb(150, 0, 0));
                SetIconButtonState(_restartPbBtn, false, Color.FromArgb(180, 100, 0));
            }
        }

        private static void SetIconButtonState(Button button, bool enabled, Color activeColor)
        {
            button.Enabled = enabled;
            button.ForeColor = enabled ? activeColor : Color.FromArgb(130, 130, 130);
            button.BackColor = Color.Transparent;
        }

        private async Task<bool> IsPocketBaseRunningAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(1);
                await client.GetAsync(GetPocketBaseBaseUrl() + "/");
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Event Handlers
        private async void OnStartService(object? sender, EventArgs e)
        {
            try
            {
                await _service.StartAsync();
                MessageBox.Show("Service started successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnStopService(object? sender, EventArgs e)
        {
            try
            {
                await _service.StopAsync();
                MessageBox.Show("Service stopped successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnRestartService(object? sender, EventArgs e)
        {
            try
            {
                await _service.StopAsync();
                await Task.Delay(2000);
                await _service.StartAsync();
                MessageBox.Show("Service restarted successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnStartPocketBase(object? sender, EventArgs e)
        {
            try
            {
                var started = await _pocketBaseManager.StartAsync();
                if (started)
                    MessageBox.Show("PocketBase started successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("Failed to start PocketBase", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnStopPocketBase(object? sender, EventArgs e)
        {
            try
            {
                _pocketBaseManager.Stop();
                MessageBox.Show("PocketBase stopped successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnRestartPocketBase(object? sender, EventArgs e)
        {
            try
            {
                _pocketBaseManager.Stop();
                await Task.Delay(2000);
                var started = await _pocketBaseManager.StartAsync();
                if (started)
                    MessageBox.Show("PocketBase restarted successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("Failed to restart PocketBase", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnSaveConfiguration(object? sender, EventArgs e)
        {
            try
            {
                var configPath = GetRuntimeSettingsPath();
                var settings = new System.Collections.Generic.Dictionary<string, object>();

                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    settings = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(json)
                        ?? new System.Collections.Generic.Dictionary<string, object>();
                }
                
                var previousCommentsLimit = ReadIntSetting(settings, "comments_extraction_limit", _config.CommentsExtractionLimit);
                var previousSkipTranscription = ReadBoolSetting(settings, "skip_transcription", _config.SkipTranscription);
                var previousTempDirectory = ReadStringSetting(settings, "temp_directory", _config.TempDirectory);
                var previousApiKey = ReadStringSetting(settings, "assemblyai_api_key", _config.AssemblyAIApiKey);
                var previousResponseApiUrl = ReadStringSetting(settings, "response_api_url", _config.ResponseApiUrl);
                var previousModifyCommentApiUrl = ReadStringSetting(settings, "modify_comment_api_url", _config.ModifyCommentApiUrl);

                var newCommentsLimit = (int)_commentsLimitInput.Value;
                var newSkipTranscription = _skipTranscriptionCheckbox.Checked;
                var newTempDirectory = _tempDirectoryInput.Text;
                var newApiKey = _apiKeyInput.Text;
                var newResponseApiUrl = _responseApiUrlInput.Text;
                var newModifyCommentApiUrl = _modifyCommentApiUrlInput.Text;

                var hasConfigChanges =
                    previousCommentsLimit != newCommentsLimit ||
                    previousSkipTranscription != newSkipTranscription ||
                    !string.Equals(previousTempDirectory, newTempDirectory, StringComparison.Ordinal) ||
                    !string.Equals(previousApiKey, newApiKey, StringComparison.Ordinal) ||
                    !string.Equals(previousResponseApiUrl, newResponseApiUrl, StringComparison.Ordinal) ||
                    !string.Equals(previousModifyCommentApiUrl, newModifyCommentApiUrl, StringComparison.Ordinal);

                settings["comments_extraction_limit"] = newCommentsLimit;
                settings["skip_transcription"] = newSkipTranscription;
                settings["temp_directory"] = newTempDirectory;
                settings["assemblyai_api_key"] = newApiKey;
                settings["response_api_url"] = newResponseApiUrl;
                settings["modify_comment_api_url"] = newModifyCommentApiUrl;

                var newJson = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(configPath, newJson);

                _config.Reload();

                if (hasConfigChanges)
                {
                    var isRunning = _service.State == ServiceState.Running;
                    if (isRunning)
                    {
                        await _service.StopAsync();
                        await Task.Delay(2000);
                        await _service.StartAsync();

                        MessageBox.Show(
                            "Configuration saved and service restarted successfully.",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    MessageBox.Show(
                        "Configuration saved successfully. Service is stopped, so no restart was needed.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                MessageBox.Show(
                    "No configuration changes detected.",
                    "Information",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static int ReadIntSetting(System.Collections.Generic.IDictionary<string, object> settings, string key, int fallback)
        {
            if (!settings.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            if (value is Newtonsoft.Json.Linq.JToken token)
            {
                return token.Type == Newtonsoft.Json.Linq.JTokenType.Integer
                    ? token.ToObject<int>()
                    : fallback;
            }

            return int.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private static bool ReadBoolSetting(System.Collections.Generic.IDictionary<string, object> settings, string key, bool fallback)
        {
            if (!settings.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            if (value is Newtonsoft.Json.Linq.JToken token)
            {
                return token.Type == Newtonsoft.Json.Linq.JTokenType.Boolean
                    ? token.ToObject<bool>()
                    : fallback;
            }

            return bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private static string ReadStringSetting(System.Collections.Generic.IDictionary<string, object> settings, string key, string fallback)
        {
            if (!settings.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            if (value is Newtonsoft.Json.Linq.JToken token)
            {
                return token.Type == Newtonsoft.Json.Linq.JTokenType.String
                    ? token.ToObject<string>() ?? fallback
                    : token.ToString();
            }

            return value.ToString() ?? fallback;
        }

        private string GetRuntimeSettingsPath()
        {
            return _config.ConfigFilePath;
        }

        private void OnBrowseTempDirectory(object? sender, EventArgs e)
        {
            try
            {
                // Use a thread to avoid blocking the UI
                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        using var dialog = new FolderBrowserDialog
                        {
                            Description = "Select audio temporary directory",
                            SelectedPath = string.IsNullOrEmpty(_tempDirectoryInput.Text) 
                                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                                : _tempDirectoryInput.Text,
                            ShowNewFolderButton = true
                        };

                        var result = dialog.ShowDialog();
                        
                        if (result == DialogResult.OK)
                        {
                            // Update UI on the UI thread
                            if (InvokeRequired)
                            {
                                Invoke(new Action(() => _tempDirectoryInput.Text = dialog.SelectedPath));
                            }
                            else
                            {
                                _tempDirectoryInput.Text = dialog.SelectedPath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in folder browser dialog thread");
                        Invoke(new Action(() =>
                        {
                            MessageBox.Show($"Error opening folder browser: {ex.Message}", "Error", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                });
                
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error starting folder browser thread");
                MessageBox.Show($"Error opening folder browser: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnClearData(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will delete ALL videos, comments, and responses from the database.\n\n" +
                "Are you sure you want to continue?",
                "Confirm Data Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    var dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TikTokManager",
                        "pocketbase_data",
                        "data.db"
                    );

                    var sqlite3Path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TikTokManager",
                        "sqlite3.exe"
                    );

                    if (File.Exists(sqlite3Path) && File.Exists(dbPath))
                    {
                        var process = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = sqlite3Path,
                                Arguments = $"\"{dbPath}\" \"DELETE FROM comments; DELETE FROM responses; DELETE FROM videos; VACUUM;\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        process.Start();
                        process.WaitForExit();

                        MessageBox.Show("Data cleared successfully", "Success", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        await UpdateStatistics();
                    }
                    else
                    {
                        MessageBox.Show("Database tools not found", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear data: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnOpenPocketBaseAdmin(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = GetPocketBaseBaseUrl() + "/_/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open PocketBase Admin: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            base.OnFormClosing(e);
        }

        private string GetPocketBaseBaseUrl()
        {
            var configuredUrl = _config.PocketBaseUrl;
            if (string.IsNullOrWhiteSpace(configuredUrl))
            {
                return "http://127.0.0.1:8090";
            }

            return configuredUrl.TrimEnd('/');
        }
    }
}

