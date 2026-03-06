using System;
using System.Drawing;
using System.Windows.Forms;
using FluxAnswer.Configuration;
using FluxAnswer.Services;

namespace FluxAnswer.SystemTray
{
    public class SystemTrayIcon : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _appIcon;
        private readonly IVideoProcessingService _service;
        private readonly IConfigurationManager _config;
        private readonly PocketBaseManager _pocketBaseManager;
        private readonly ContextMenuStrip _contextMenu;
        private AdminWindow? _adminWindow;

        public SystemTrayIcon(
            IVideoProcessingService service,
            IConfigurationManager config,
            PocketBaseManager pocketBaseManager)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _pocketBaseManager = pocketBaseManager ?? throw new ArgumentNullException(nameof(pocketBaseManager));
            _appIcon = BrandingAssets.GetApplicationIcon();
            
            _contextMenu = CreateContextMenu();
            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Text = "FluxAnswer",
                Icon = _appIcon,
                Visible = true
            };

            _notifyIcon.DoubleClick += OnDoubleClick;
            _service.StateChanged += OnServiceStateChanged;
            _service.ErrorOccurred += OnServiceErrorOccurred;
            
            UpdateIcon();
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            
            menu.Items.Add("Open Admin Panel", null, OnViewStatus);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Start Service", null, OnStartService);
            menu.Items.Add("Stop Service", null, OnStopService);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit Application", null, OnExitApplication);
            
            return menu;
        }

        private void UpdateIcon()
        {
            var state = _service.State;
            _notifyIcon.Icon = _appIcon;
            _notifyIcon.Text = BuildTrayText(state);
            
            // Update menu item states
            if (_contextMenu.Items.Count >= 4)
            {
                _contextMenu.Items[2].Enabled = state == ServiceState.Stopped;
                _contextMenu.Items[3].Enabled = state == ServiceState.Running;
            }
        }

        private static string BuildTrayText(ServiceState state)
        {
            var text = $"FluxAnswer - {state} - {BrandingAssets.OwnerName}";
            return text.Length <= 63 ? text : text.Substring(0, 63);
        }

        private void OnServiceStateChanged(object? sender, ServiceStateChangedEventArgs e)
        {
            if (_notifyIcon.ContextMenuStrip?.InvokeRequired == true)
            {
                _notifyIcon.ContextMenuStrip.Invoke(UpdateIcon);
            }
            else
            {
                UpdateIcon();
            }
        }

        private void OnServiceErrorOccurred(object? sender, Services.ErrorEventArgs e)
        {
            ShowErrorNotification(e.Message);
        }

        private void OnDoubleClick(object? sender, EventArgs e)
        {
            ShowStatusWindow();
        }

        private void OnViewStatus(object? sender, EventArgs e)
        {
            ShowStatusWindow();
        }

        private void ShowStatusWindow()
        {
            if (_adminWindow == null || _adminWindow.IsDisposed)
            {
                _adminWindow = new AdminWindow(_service, _config, _pocketBaseManager);
                _adminWindow.Show();
            }
            else
            {
                _adminWindow.BringToFront();
                _adminWindow.Activate();
            }
        }

        private async void OnStartService(object? sender, EventArgs e)
        {
            try
            {
                await _service.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start service: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnStopService(object? sender, EventArgs e)
        {
            try
            {
                await _service.StopAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop service: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnExitApplication(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit? The service will be stopped.",
                "Confirm Exit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    await _service.StopAsync();
                }
                catch
                {
                    // Ignore errors during shutdown
                }
                
                Application.Exit();
            }
        }

        public void ShowErrorNotification(string message)
        {
            _notifyIcon.ShowBalloonTip(
                5000,
                "Video Processing System Error",
                message,
                ToolTipIcon.Error);
        }

        public void Dispose()
        {
            _service.StateChanged -= OnServiceStateChanged;
            _service.ErrorOccurred -= OnServiceErrorOccurred;
            _notifyIcon.DoubleClick -= OnDoubleClick;
            _adminWindow?.Dispose();
            _contextMenu?.Dispose();
            _notifyIcon?.Dispose();
            _appIcon?.Dispose();
        }
    }
}

