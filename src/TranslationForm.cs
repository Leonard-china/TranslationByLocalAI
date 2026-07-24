using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal sealed class LanguageOption
    {
        internal LanguageOption(string displayName, string promptName)
        {
            DisplayName = displayName;
            PromptName = promptName;
        }

        internal string DisplayName { get; private set; }
        internal string PromptName { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal sealed class TranslationForm : Form
    {
        private readonly AppConfig _config;
        private readonly TranslationClient _client;
        private readonly TextBox _sourceBox;
        private readonly TextBox _resultBox;
        private readonly ComboBox _targetBox;
        private readonly Label _directionLabel;
        private readonly Label _statusLabel;
        private readonly Button _translateButton;
        private readonly Button _copyButton;
        private readonly System.Windows.Forms.Timer _manualTranslationTimer;
        private CancellationTokenSource _cancellation;
        private string _sourceText;
        private int _requestVersion;
        private bool _allowClose;
        private bool _manualMode;
        private bool _targetChangedByUser;

        private static readonly LanguageOption[] Languages =
        {
            new LanguageOption("简体中文", "Simplified Chinese"),
            new LanguageOption("English", "English"),
            new LanguageOption("日本語", "Japanese"),
            new LanguageOption("한국어", "Korean"),
            new LanguageOption("Français", "French"),
            new LanguageOption("Deutsch", "German"),
            new LanguageOption("Español", "Spanish"),
            new LanguageOption("Русский", "Russian"),
            new LanguageOption("Português", "Portuguese"),
            new LanguageOption("Italiano", "Italian"),
            new LanguageOption("繁體中文", "Traditional Chinese")
        };

        internal TranslationForm(
            AppConfig config,
            TranslationClient client,
            Icon appIcon)
        {
            _config = config;
            _client = client;
            _manualTranslationTimer = new System.Windows.Forms.Timer();
            _manualTranslationTimer.Interval = 600;
            _manualTranslationTimer.Tick += ManualTranslationTimerTick;

            Text = "本地 AI 翻译 · Esc 关闭";
            Icon = appIcon;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            MinimumSize = new Size(360, 220);
            Size = new Size(440, 270);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9f, FontStyle.Regular);

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12, 10, 12, 10);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35f));
            Controls.Add(root);

            var header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 3;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Margin = new Padding(0);

            var title = new Label();
            title.Text = "AI 翻译";
            title.AutoSize = true;
            title.Font = UiTheme.Font(11f, FontStyle.Bold);
            title.ForeColor = UiTheme.Text;
            title.Margin = new Padding(0, 4, 8, 0);
            header.Controls.Add(title, 0, 0);

            _directionLabel = new Label();
            _directionLabel.AutoSize = true;
            _directionLabel.ForeColor = UiTheme.Muted;
            _directionLabel.Margin = new Padding(0, 6, 8, 0);
            header.Controls.Add(_directionLabel, 1, 0);

            _targetBox = new ComboBox();
            _targetBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _targetBox.Font = UiTheme.Font(8.5f, FontStyle.Regular);
            _targetBox.Width = 118;
            _targetBox.Items.AddRange(Languages);
            _targetBox.Margin = new Padding(0, 1, 0, 2);
            _targetBox.SelectionChangeCommitted += delegate
            {
                _targetChangedByUser = true;
                QueueManualTranslation();
            };
            header.Controls.Add(_targetBox, 2, 0);
            root.Controls.Add(header, 0, 0);

            _sourceBox = CreateTextBox(9.5f);
            _sourceBox.ReadOnly = true;
            _sourceBox.BackColor = Color.FromArgb(241, 245, 249);
            _sourceBox.ScrollBars = ScrollBars.Vertical;
            _sourceBox.TextChanged += ManualSourceTextChanged;
            root.Controls.Add(_sourceBox, 0, 1);

            var resultHeader = new TableLayoutPanel();
            resultHeader.Dock = DockStyle.Fill;
            resultHeader.ColumnCount = 3;
            resultHeader.RowCount = 1;
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resultHeader.Margin = new Padding(0);

            var resultLabel = new Label();
            resultLabel.Text = "译文";
            resultLabel.AutoSize = true;
            resultLabel.Font = UiTheme.Font(9f, FontStyle.Bold);
            resultLabel.ForeColor = UiTheme.Text;
            resultLabel.Margin = new Padding(0, 9, 0, 0);
            resultHeader.Controls.Add(resultLabel, 0, 0);

            _translateButton = CreateCompactButton("重译", 56);
            _translateButton.Margin = new Padding(0, 4, 6, 3);
            _translateButton.Click += async delegate { await TranslateButtonClickedAsync(); };
            resultHeader.Controls.Add(_translateButton, 1, 0);

            _copyButton = CreateCompactButton("复制", 56);
            _copyButton.Enabled = false;
            _copyButton.Margin = new Padding(0, 4, 0, 3);
            _copyButton.Click += CopyResult;
            resultHeader.Controls.Add(_copyButton, 2, 0);
            root.Controls.Add(resultHeader, 0, 2);

            _resultBox = CreateTextBox(11f);
            _resultBox.ReadOnly = true;
            _resultBox.BackColor = UiTheme.Card;
            _resultBox.ScrollBars = ScrollBars.Vertical;
            root.Controls.Add(_resultBox, 0, 3);

            var footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.ColumnCount = 2;
            footer.RowCount = 1;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Margin = new Padding(0);

            _statusLabel = new Label();
            _statusLabel.Text = "准备翻译";
            _statusLabel.AutoEllipsis = true;
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.ForeColor = UiTheme.Muted;
            _statusLabel.Margin = new Padding(0, 9, 8, 0);
            footer.Controls.Add(_statusLabel, 0, 0);

            var hint = new Label();
            hint.Text = "Esc 关闭";
            hint.AutoSize = true;
            hint.ForeColor = UiTheme.Muted;
            hint.Margin = new Padding(0, 9, 0, 0);
            footer.Controls.Add(hint, 1, 0);
            root.Controls.Add(footer, 0, 4);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    HideBubble(true);
                    e.Handled = true;
                }
            };
            Deactivate += delegate { HideBubble(false); };
            FormClosing += TranslationFormClosing;
        }

        internal void ShowTranslation(string sourceText, Point anchor)
        {
            AppLogger.Write("Compact translation form updating.");
            _manualTranslationTimer.Stop();
            _manualMode = false;
            _targetChangedByUser = false;
            _sourceText = sourceText.Trim();
            _sourceBox.Text = _sourceText;
            _sourceBox.ReadOnly = true;
            _sourceBox.BackColor = Color.FromArgb(241, 245, 249);
            _translateButton.Text = "重译";
            _resultBox.Clear();
            _copyButton.Enabled = false;

            var containsChinese = ContainsChinese(_sourceText);
            _directionLabel.Text = containsChinese ? "中文 →" : "外文 →";
            SelectTarget(containsChinese ? _config.TargetForChinese : _config.TargetForForeign);

            ResizeForSource(_sourceText);
            PositionNear(anchor);

            if (!Visible)
            {
                Show();
                AppLogger.Write("Compact translation form shown.");
            }
            else
            {
                BringToFront();
            }
            Activate();
            BeginTranslation();
        }

        internal void ShowManual(Point anchor)
        {
            AppLogger.Write("Manual translation form requested.");
            _manualTranslationTimer.Stop();
            CancelCurrentTranslation();
            _manualMode = true;
            _targetChangedByUser = false;
            _sourceText = string.Empty;
            _sourceBox.ReadOnly = false;
            _sourceBox.BackColor = UiTheme.Card;
            _sourceBox.Clear();
            _resultBox.Clear();
            _copyButton.Enabled = false;
            _translateButton.Text = "翻译";
            _directionLabel.Text = "输入文本 →";
            _statusLabel.Text = "输入或粘贴文本，停止输入后将自动翻译";
            _statusLabel.ForeColor = UiTheme.Muted;
            SelectTarget(_config.TargetForForeign);

            Size = new Size(500, 330);
            PositionNear(anchor);
            if (!Visible)
            {
                Show();
            }
            else
            {
                BringToFront();
            }
            Activate();
            _sourceBox.Focus();
        }

        internal void ClosePermanently()
        {
            _allowClose = true;
            _manualTranslationTimer.Stop();
            _manualTranslationTimer.Dispose();
            CancelCurrentTranslation();
            Close();
        }

        private async void BeginTranslation()
        {
            await StartTranslationAsync();
        }

        private async Task TranslateButtonClickedAsync()
        {
            if (_manualMode)
            {
                _manualTranslationTimer.Stop();
                _sourceText = _sourceBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(_sourceText))
                {
                    _statusLabel.Text = "请先输入或粘贴需要翻译的文本";
                    _statusLabel.ForeColor = Color.FromArgb(180, 83, 9);
                    _sourceBox.Focus();
                    return;
                }

                var containsChinese = ContainsChinese(_sourceText);
                _directionLabel.Text = containsChinese ? "中文 →" : "外文 →";
                if (!_targetChangedByUser)
                {
                    SelectTarget(containsChinese ? _config.TargetForChinese : _config.TargetForForeign);
                }
            }
            await StartTranslationAsync();
        }

        private void ManualSourceTextChanged(object sender, EventArgs e)
        {
            QueueManualTranslation();
        }

        private void QueueManualTranslation()
        {
            if (!_manualMode)
            {
                return;
            }

            _manualTranslationTimer.Stop();
            CancelCurrentTranslation();
            _translateButton.Enabled = true;
            _copyButton.Enabled = false;
            _resultBox.Clear();

            _sourceText = _sourceBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(_sourceText))
            {
                _directionLabel.Text = "输入文本 →";
                _statusLabel.Text = "输入或粘贴文本，停止输入后将自动翻译";
                _statusLabel.ForeColor = UiTheme.Muted;
                return;
            }

            var containsChinese = ContainsChinese(_sourceText);
            _directionLabel.Text = containsChinese ? "中文 →" : "外文 →";
            if (!_targetChangedByUser)
            {
                SelectTarget(containsChinese ? _config.TargetForChinese : _config.TargetForForeign);
            }

            _statusLabel.Text = "等待输入完成…";
            _statusLabel.ForeColor = UiTheme.Muted;
            _manualTranslationTimer.Start();
        }

        private async void ManualTranslationTimerTick(object sender, EventArgs e)
        {
            _manualTranslationTimer.Stop();
            if (!_manualMode || string.IsNullOrWhiteSpace(_sourceText))
            {
                return;
            }

            await StartTranslationAsync();
        }

        private async Task StartTranslationAsync()
        {
            var target = _targetBox.SelectedItem as LanguageOption;
            if (target == null || string.IsNullOrWhiteSpace(_sourceText))
            {
                return;
            }

            CancelCurrentTranslation();
            var requestVersion = ++_requestVersion;
            _cancellation = new CancellationTokenSource();
            var cancellation = _cancellation;

            _translateButton.Enabled = false;
            _copyButton.Enabled = false;
            _resultBox.Clear();

            var progress = new Progress<string>(delegate(string value)
            {
                if (requestVersion == _requestVersion)
                {
                    _statusLabel.Text = value;
                    _statusLabel.ForeColor = UiTheme.Muted;
                }
            });

            try
            {
                var result = await _client.TranslateAsync(
                    _sourceText,
                    target.PromptName,
                    progress,
                    cancellation.Token);
                if (requestVersion != _requestVersion)
                {
                    return;
                }

                _resultBox.Text = result;
                _statusLabel.Text = "完成 · 本机处理";
                _statusLabel.ForeColor = Color.FromArgb(22, 163, 74);
                _copyButton.Enabled = true;
            }
            catch (OperationCanceledException)
            {
                if (requestVersion == _requestVersion)
                {
                    _statusLabel.Text = "已取消";
                    _statusLabel.ForeColor = UiTheme.Muted;
                }
            }
            catch (Exception ex)
            {
                if (requestVersion != _requestVersion)
                {
                    return;
                }
                _statusLabel.Text = "翻译失败";
                _statusLabel.ForeColor = Color.FromArgb(220, 38, 38);
                _resultBox.Text = ex.Message;
            }
            finally
            {
                if (requestVersion == _requestVersion)
                {
                    _translateButton.Enabled = true;
                }
            }
        }

        private void CopyResult(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_resultBox.Text))
            {
                return;
            }
            try
            {
                Clipboard.SetText(_resultBox.Text);
                _statusLabel.Text = "译文已复制";
                _statusLabel.ForeColor = Color.FromArgb(22, 163, 74);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "复制失败：" + ex.Message;
                _statusLabel.ForeColor = Color.FromArgb(220, 38, 38);
            }
        }

        private void HideBubble(bool cancelTranslation)
        {
            _manualTranslationTimer.Stop();
            if (cancelTranslation)
            {
                CancelCurrentTranslation();
            }
            if (Visible)
            {
                Hide();
            }
        }

        private void CancelCurrentTranslation()
        {
            if (_cancellation == null)
            {
                return;
            }
            _requestVersion++;
            _cancellation.Cancel();
            _cancellation.Dispose();
            _cancellation = null;
        }

        private void TranslationFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideBubble(true);
            }
        }

        private void ResizeForSource(string sourceText)
        {
            if (sourceText.Length <= 80 && sourceText.IndexOf('\n') < 0)
            {
                Size = new Size(440, 270);
            }
            else
            {
                Size = new Size(500, 330);
            }
        }

        private void PositionNear(Point anchor)
        {
            var screen = Screen.FromPoint(anchor);
            var area = screen.WorkingArea;
            var x = anchor.X + 18;
            var y = anchor.Y + 22;

            if (x + Width > area.Right)
            {
                x = anchor.X - Width - 18;
            }
            if (y + Height > area.Bottom)
            {
                y = anchor.Y - Height - 18;
            }

            x = Math.Max(area.Left, Math.Min(x, area.Right - Width));
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - Height));
            Location = new Point(x, y);
        }

        private static TextBox CreateTextBox(float fontSize)
        {
            var textBox = new TextBox();
            textBox.Multiline = true;
            textBox.Dock = DockStyle.Fill;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Font = UiTheme.Font(fontSize, FontStyle.Regular);
            textBox.ForeColor = UiTheme.Text;
            textBox.Margin = new Padding(0);
            return textBox;
        }

        private static Button CreateCompactButton(string text, int width)
        {
            var button = UiTheme.CreateSecondaryButton(text);
            button.Font = UiTheme.Font(8.5f, FontStyle.Regular);
            button.Width = width;
            button.Height = 27;
            return button;
        }

        private void SelectTarget(string configuredValue)
        {
            for (var index = 0; index < _targetBox.Items.Count; index++)
            {
                var option = _targetBox.Items[index] as LanguageOption;
                if (option != null
                    && (string.Equals(option.DisplayName, configuredValue, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(option.PromptName, configuredValue, StringComparison.OrdinalIgnoreCase)))
                {
                    _targetBox.SelectedIndex = index;
                    return;
                }
            }
            _targetBox.SelectedIndex = 0;
        }

        internal static bool ContainsChinese(string text)
        {
            foreach (var character in text)
            {
                if ((character >= '\u3400' && character <= '\u4DBF')
                    || (character >= '\u4E00' && character <= '\u9FFF')
                    || (character >= '\uF900' && character <= '\uFAFF'))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
