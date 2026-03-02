using System;
using System.Drawing;
using System.Windows.Forms;
using VideoProcessingSystemV2.Services;

namespace VideoProcessingSystemV2.SystemTray
{
    public class StatusWindow : Form
    {
        private readonly IVideoProcessingService _service;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private Label _serviceStateLabel = null!;
        private Label _lastExtractionLabel = null!;
        private Label _pendingLabel = null!;
        private Label _processingLabel = null!;
        private Label _completedLabel = null!;
        private Label _failedLabel = null!;
        private Label _lastErrorLabel = null!;

        public StatusWindow(IVideoProcessingService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            
            InitializeComponents();
            
            _refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000
            };
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();
            
            UpdateStatistics();
        }

        private void InitializeComponents()
        {
            Text = "Video Processing System V2 - Status";
            Size = new Size(500, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(10),
                AutoSize = true
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            _serviceStateLabel = AddRow(panel, 0, "Service State:");
            _lastExtractionLabel = AddRow(panel, 1, "Last Extraction:");
            _pendingLabel = AddRow(panel, 2, "Videos Pending:");
            _processingLabel = AddRow(panel, 3, "Videos Processing:");
            _completedLabel = AddRow(panel, 4, "Videos Completed:");
            _failedLabel = AddRow(panel, 5, "Videos Failed:");
            _lastErrorLabel = AddRow(panel, 6, "Last Error:");

            var closeButton = new Button
            {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = 30,
                Margin = new Padding(10)
            };
            closeButton.Click += (s, e) => Close();

            Controls.Add(panel);
            Controls.Add(closeButton);
        }

        private Label AddRow(TableLayoutPanel panel, int row, string labelText)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            };

            var valueLabel = new Label
            {
                Text = "Loading...",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(valueLabel, 1, row);

            return valueLabel;
        }

        private async void OnRefreshTick(object? sender, EventArgs e)
        {
            UpdateStatistics();
        }

        private async void UpdateStatistics()
        {
            try
            {
                var state = _service.State;
                _serviceStateLabel.Text = state.ToString();
                _serviceStateLabel.ForeColor = state switch
                {
                    ServiceState.Running => Color.Green,
                    ServiceState.Stopped => Color.Red,
                    ServiceState.Error => Color.DarkRed,
                    _ => Color.Orange
                };

                var stats = await _service.GetStatisticsAsync();
                
                _lastExtractionLabel.Text = stats.LastExtractionTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";
                _pendingLabel.Text = stats.PendingCount.ToString();
                _processingLabel.Text = stats.ProcessingCount.ToString();
                _completedLabel.Text = stats.CompletedCount.ToString();
                _failedLabel.Text = stats.FailedCount.ToString();
                _lastErrorLabel.Text = string.IsNullOrEmpty(stats.LastError) ? "None" : stats.LastError;
                _lastErrorLabel.ForeColor = string.IsNullOrEmpty(stats.LastError) ? Color.Green : Color.Red;
            }
            catch (Exception ex)
            {
                _lastErrorLabel.Text = $"Error updating statistics: {ex.Message}";
                _lastErrorLabel.ForeColor = Color.Red;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
