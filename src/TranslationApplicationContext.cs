using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal sealed class TranslationApplicationContext : ApplicationContext, IDisposable
    {
        private readonly Icon _appIcon;
        private readonly NotifyIcon _trayIcon;
        private readonly ToolStripMenuItem _enabledMenuItem;
        private readonly ToolStripMenuItem _widgetMenuItem;
        private readonly FloatingButtonForm _floatingButton;
        private readonly DesktopWidgetForm _desktopWidget;
        private readonly SelectionMonitor _selectionMonitor;
        private readonly TranslationClient _translationClient;
        private readonly CancellationTokenSource _lifetimeCancellation;
        private AppConfig _config;
        private TranslationForm _translationForm;
        private string _selectedText;
        private Point _selectedPoint;
        private bool _disposed;

        internal TranslationApplicationContext()
        {
            _config = AppConfig.Load();
            AppLogger.Write("Application starting.");
            AppLogger.Write(
                "Desktop widget enabled="
                + _config.DesktopWidgetEnabled
                + ", savedLocation="
                + _config.DesktopWidgetX
                + ","
                + _config.DesktopWidgetY
                + ".");
            _appIcon = UiTheme.CreateAppIcon();
            _translationClient = new TranslationClient(_config);
            _lifetimeCancellation = new CancellationTokenSource();
            _floatingButton = new FloatingButtonForm();
            var unusedHandle = _floatingButton.Handle;
            _desktopWidget = new DesktopWidgetForm();
            var unusedWidgetHandle = _desktopWidget.Handle;

            _enabledMenuItem = new ToolStripMenuItem("启用划词翻译");
            _enabledMenuItem.Checked = _config.Enabled;
            _enabledMenuItem.CheckOnClick = true;
            _enabledMenuItem.Click += ToggleEnabled;

            _widgetMenuItem = new ToolStripMenuItem("显示桌面悬浮窗");
            _widgetMenuItem.Checked = _config.DesktopWidgetEnabled;
            _widgetMenuItem.CheckOnClick = true;
            _widgetMenuItem.Click += ToggleDesktopWidget;

            var testItem = new ToolStripMenuItem("测试本地 AI");
            testItem.Click += async delegate { await TestConnectionAsync(); };
            var settingsItem = new ToolStripMenuItem("设置…");
            settingsItem.Click += delegate { ShowSettings(); };
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitThread(); };

            var menu = new ContextMenuStrip();
            menu.Font = UiTheme.Font(9f, FontStyle.Regular);
            menu.Items.Add(_enabledMenuItem);
            menu.Items.Add(_widgetMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(testItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = _appIcon;
            _trayIcon.Text = "本地 AI 划词翻译";
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += delegate { ShowSettings(); };

            _selectionMonitor = new SelectionMonitor(
                _floatingButton,
                _floatingButton,
                _desktopWidget);
            _selectionMonitor.Enabled = _config.Enabled;
            _selectionMonitor.TextSelected += SelectionMonitorTextSelected;
            _floatingButton.TranslateRequested += FloatingButtonTranslateRequested;
            _desktopWidget.ManualTranslationRequested += DesktopWidgetManualTranslationRequested;
            _desktopWidget.WidgetMoved += DesktopWidgetMoved;

            try
            {
                _selectionMonitor.Start();
                _trayIcon.ShowBalloonTip(
                    2500,
                    "本地 AI 划词翻译",
                    _config.AutoStartServer
                        ? "软件已启动，正在同时加载本地 AI…"
                        : "已启动。用鼠标划选文本，点击光标旁的“译”按钮。",
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "启动全局划词监听失败：\r\n" + ex.Message,
                    "本地 AI 划词翻译",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            if (_config.DesktopWidgetEnabled)
            {
                _desktopWidget.ShowAtSavedPosition(
                    _config.DesktopWidgetX,
                    _config.DesktopWidgetY);
                AppLogger.Write(
                    "Desktop widget shown, visible="
                    + _desktopWidget.Visible
                    + ", bounds="
                    + _desktopWidget.Bounds
                    + ".");
            }

            Application.Idle += StartLocalAiOnApplicationIdle;
        }

        private void StartLocalAiOnApplicationIdle(object sender, EventArgs e)
        {
            Application.Idle -= StartLocalAiOnApplicationIdle;
            StartLocalAiOnLaunchAsync();
        }

        private async void StartLocalAiOnLaunchAsync()
        {
            if (!_config.AutoStartServer || _disposed)
            {
                return;
            }

            AppLogger.Write("Starting local AI together with the application.");
            var progress = new Progress<string>(delegate(string status)
            {
                if (!_disposed)
                {
                    _trayIcon.Text = ShortTrayText(status);
                }
            });

            try
            {
                await _translationClient.StartServerAsync(
                    progress,
                    _lifetimeCancellation.Token);
                if (_disposed)
                {
                    return;
                }

                AppLogger.Write("Local AI is ready.");
                _trayIcon.Text = "本地 AI 划词翻译";
                _trayIcon.ShowBalloonTip(
                    2200,
                    "本地 AI 已就绪",
                    "软件和本地模型服务均已启动。",
                    ToolTipIcon.Info);
            }
            catch (OperationCanceledException)
            {
                AppLogger.Write("Local AI startup was canceled during application exit.");
            }
            catch (ObjectDisposedException)
            {
                AppLogger.Write("Local AI startup ended because the application is exiting.");
            }
            catch (Exception ex)
            {
                AppLogger.Write("Local AI startup failed: " + ex);
                if (!_disposed)
                {
                    _trayIcon.Text = "本地 AI 划词翻译";
                    _trayIcon.ShowBalloonTip(
                        4000,
                        "本地 AI 启动失败",
                        ShortTrayText(ex.Message),
                        ToolTipIcon.Error);
                }
            }
        }

        private void SelectionMonitorTextSelected(object sender, TextSelectedEventArgs e)
        {
            _selectedText = e.Text;
            _selectedPoint = e.CursorPosition;
            AppLogger.Write("Showing floating button.");
            _floatingButton.ShowNear(e.CursorPosition, _config.ButtonTimeoutSeconds);
        }

        private void FloatingButtonTranslateRequested(object sender, EventArgs e)
        {
            _floatingButton.Hide();
            if (string.IsNullOrWhiteSpace(_selectedText))
            {
                return;
            }

            AppLogger.Write("Translation window requested.");
            EnsureTranslationForm();
            _translationForm.ShowTranslation(_selectedText, _selectedPoint);
        }

        private void DesktopWidgetManualTranslationRequested(object sender, EventArgs e)
        {
            EnsureTranslationForm();
            var anchor = new Point(
                _desktopWidget.Left + _desktopWidget.Width / 2,
                _desktopWidget.Top + _desktopWidget.Height / 2);
            _translationForm.ShowManual(anchor);
        }

        private void DesktopWidgetMoved(object sender, EventArgs e)
        {
            _config.DesktopWidgetX = _desktopWidget.Left;
            _config.DesktopWidgetY = _desktopWidget.Top;
            SaveConfig();
        }

        private void EnsureTranslationForm()
        {
            if (_translationForm == null || _translationForm.IsDisposed)
            {
                _translationForm = new TranslationForm(_config, _translationClient, _appIcon);
            }
        }

        private void ToggleEnabled(object sender, EventArgs e)
        {
            _config.Enabled = _enabledMenuItem.Checked;
            _selectionMonitor.Enabled = _config.Enabled;
            if (!_config.Enabled)
            {
                _floatingButton.Hide();
            }
            SaveConfig();
        }

        private void ToggleDesktopWidget(object sender, EventArgs e)
        {
            _config.DesktopWidgetEnabled = _widgetMenuItem.Checked;
            if (_config.DesktopWidgetEnabled)
            {
                _desktopWidget.ShowAtSavedPosition(
                    _config.DesktopWidgetX,
                    _config.DesktopWidgetY);
            }
            else
            {
                _desktopWidget.Hide();
            }
            SaveConfig();
        }

        private async Task TestConnectionAsync()
        {
            var progress = new Progress<string>(delegate(string status)
            {
                _trayIcon.Text = ShortTrayText(status);
            });
            try
            {
                await _translationClient.TestConnectionAsync(progress, CancellationToken.None);
                _trayIcon.Text = "本地 AI 划词翻译";
                _trayIcon.ShowBalloonTip(
                    3000,
                    "连接成功",
                    "本地模型服务已就绪。",
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _trayIcon.Text = "本地 AI 划词翻译";
                MessageBox.Show(
                    ex.Message,
                    "无法连接本地 AI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ShowSettings()
        {
            using (var form = new SettingsForm(_config, _appIcon))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    form.ApplyTo(_config);
                    SaveConfig();
                    _translationClient.UpdateConfig(_config);
                    _trayIcon.ShowBalloonTip(
                        1800,
                        "设置已保存",
                        "新的翻译设置将在下次翻译时生效。",
                        ToolTipIcon.Info);
                }
            }
        }

        private void SaveConfig()
        {
            try
            {
                _config.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "保存设置失败：\r\n" + ex.Message,
                    "本地 AI 划词翻译",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static string ShortTrayText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "本地 AI 划词翻译";
            }
            return value.Length <= 63 ? value : value.Substring(0, 63);
        }

        protected override void ExitThreadCore()
        {
            Dispose();
            base.ExitThreadCore();
        }

        public new void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            Application.Idle -= StartLocalAiOnApplicationIdle;
            _lifetimeCancellation.Cancel();
            _floatingButton.Hide();
            _desktopWidget.Hide();
            if (_translationForm != null && !_translationForm.IsDisposed)
            {
                _translationForm.ClosePermanently();
                _translationForm = null;
            }
            _selectionMonitor.Dispose();
            _translationClient.Dispose();
            _lifetimeCancellation.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _floatingButton.Dispose();
            _desktopWidget.Dispose();
            _appIcon.Dispose();
            base.Dispose();
        }
    }
}
