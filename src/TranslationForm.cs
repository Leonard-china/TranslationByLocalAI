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
        private readonly Panel _resultViewport;
        private readonly TableLayoutPanel _resultCards;
        private readonly ComboBox _targetBox;
        private readonly Label _directionLabel;
        private readonly Label _resultModeLabel;
        private readonly Label _statusLabel;
        private readonly Button _translateButton;
        private readonly Button _copyButton;
        private readonly System.Windows.Forms.Timer _manualTranslationTimer;
        private readonly RowStyle _sourceRowStyle;
        private CancellationTokenSource _cancellation;
        private TranslationResult _currentResult;
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
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(440, 320);
            Size = new Size(560, 470);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9f, FontStyle.Regular);

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16, 14, 16, 12);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            _sourceRowStyle = new RowStyle(SizeType.Absolute, 70f);
            root.RowStyles.Add(_sourceRowStyle);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
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
            title.Font = UiTheme.Font(12.5f, FontStyle.Bold);
            title.ForeColor = UiTheme.Text;
            title.Margin = new Padding(0, 3, 10, 0);
            header.Controls.Add(title, 0, 0);

            _directionLabel = new Label();
            _directionLabel.AutoSize = true;
            _directionLabel.ForeColor = UiTheme.Muted;
            _directionLabel.Margin = new Padding(0, 7, 8, 0);
            header.Controls.Add(_directionLabel, 1, 0);

            _targetBox = new ComboBox();
            _targetBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _targetBox.Font = UiTheme.Font(8.5f, FontStyle.Regular);
            _targetBox.Width = 126;
            _targetBox.Items.AddRange(Languages);
            _targetBox.Margin = new Padding(0, 2, 0, 4);
            _targetBox.SelectionChangeCommitted += delegate
            {
                _targetChangedByUser = true;
                QueueManualTranslation();
            };
            header.Controls.Add(_targetBox, 2, 0);
            root.Controls.Add(header, 0, 0);

            var sourceSurface = CreateSurfacePanel(UiTheme.Card);
            sourceSurface.Padding = new Padding(12, 9, 12, 9);
            sourceSurface.Margin = new Padding(0, 0, 0, 8);

            _sourceBox = CreateTextBox(10f);
            _sourceBox.ReadOnly = true;
            _sourceBox.BorderStyle = BorderStyle.None;
            _sourceBox.BackColor = UiTheme.Card;
            _sourceBox.ScrollBars = ScrollBars.Vertical;
            _sourceBox.TextChanged += ManualSourceTextChanged;
            sourceSurface.Controls.Add(_sourceBox);
            root.Controls.Add(sourceSurface, 0, 1);

            var resultHeader = new TableLayoutPanel();
            resultHeader.Dock = DockStyle.Fill;
            resultHeader.ColumnCount = 4;
            resultHeader.RowCount = 1;
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resultHeader.Margin = new Padding(0);

            var resultLabel = new Label();
            resultLabel.Text = "翻译结果";
            resultLabel.AutoSize = true;
            resultLabel.Font = UiTheme.Font(9.5f, FontStyle.Bold);
            resultLabel.ForeColor = UiTheme.Text;
            resultLabel.Margin = new Padding(0, 11, 9, 0);
            resultHeader.Controls.Add(resultLabel, 0, 0);

            _resultModeLabel = new Label();
            _resultModeLabel.AutoSize = true;
            _resultModeLabel.Font = UiTheme.Font(8f, FontStyle.Bold);
            _resultModeLabel.ForeColor = UiTheme.Primary;
            _resultModeLabel.BackColor = Color.FromArgb(235, 242, 255);
            _resultModeLabel.Padding = new Padding(7, 3, 7, 3);
            _resultModeLabel.Margin = new Padding(0, 7, 8, 0);
            _resultModeLabel.Visible = false;
            resultHeader.Controls.Add(_resultModeLabel, 1, 0);

            _translateButton = CreateCompactButton("重译", 56);
            _translateButton.Margin = new Padding(0, 6, 7, 4);
            _translateButton.Click += async delegate { await TranslateButtonClickedAsync(); };
            resultHeader.Controls.Add(_translateButton, 2, 0);

            _copyButton = CreateCompactButton("复制", 56);
            _copyButton.Enabled = false;
            _copyButton.Margin = new Padding(0, 6, 0, 4);
            _copyButton.Click += CopyResult;
            resultHeader.Controls.Add(_copyButton, 3, 0);
            root.Controls.Add(resultHeader, 0, 2);

            _resultViewport = new Panel();
            _resultViewport.Dock = DockStyle.Fill;
            _resultViewport.AutoScroll = true;
            _resultViewport.BackColor = UiTheme.Background;
            _resultViewport.Margin = new Padding(0);

            _resultCards = new TableLayoutPanel();
            _resultCards.AutoSize = true;
            _resultCards.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _resultCards.ColumnCount = 1;
            _resultCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _resultCards.Dock = DockStyle.Top;
            _resultCards.Margin = new Padding(0);
            _resultViewport.Controls.Add(_resultCards);
            _resultViewport.SizeChanged += delegate { UpdateResultCardWidths(); };
            root.Controls.Add(_resultViewport, 0, 3);

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
            _statusLabel.Margin = new Padding(0, 11, 8, 0);
            footer.Controls.Add(_statusLabel, 0, 0);

            var hint = new Label();
            hint.Text = "Esc 关闭";
            hint.AutoSize = true;
            hint.ForeColor = UiTheme.Muted;
            hint.Margin = new Padding(0, 11, 0, 0);
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
            _sourceBox.BackColor = UiTheme.Card;
            _translateButton.Text = "重译";
            ClearResult();
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
            ClearResult();
            _copyButton.Enabled = false;
            _translateButton.Text = "翻译";
            _directionLabel.Text = "输入文本 →";
            _statusLabel.Text = "输入或粘贴文本，停止输入后将自动翻译";
            _statusLabel.ForeColor = UiTheme.Muted;
            SelectTarget(_config.TargetForForeign);

            _sourceRowStyle.Height = 104f;
            Size = _config.DetailedEnglishEnabled
                ? new Size(620, 620)
                : new Size(580, 500);
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
            ClearResult();

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
            ClearResult();

            var progress = new Progress<string>(delegate(string value)
            {
                if (requestVersion == _requestVersion)
                {
                    if (_currentResult != null && _currentResult.IsPartial)
                    {
                        _statusLabel.Text = _currentResult.Kind == TranslationContentKind.Word
                            ? "本地词典已显示 · " + value
                            : "本地译文已显示 · " + value;
                    }
                    else
                    {
                        _statusLabel.Text = value;
                    }
                    _statusLabel.ForeColor = UiTheme.Muted;
                }
            });
            var partialResultProgress = new Progress<TranslationResult>(
                delegate(TranslationResult value)
                {
                    if (requestVersion != _requestVersion || value == null)
                    {
                        return;
                    }
                    RenderResult(value);
                    _statusLabel.Text = value.Kind == TranslationContentKind.Word
                        ? "本地词典已显示 · AI 正在补充短语与例句…"
                        : "本地译文已显示 · AI 正在补充语法解析…";
                    _statusLabel.ForeColor = UiTheme.Primary;
                    _copyButton.Enabled = true;
                });

            try
            {
                var result = await _client.TranslateAsync(
                    _sourceText,
                    target.PromptName,
                    progress,
                    partialResultProgress,
                    cancellation.Token);
                if (requestVersion != _requestVersion)
                {
                    return;
                }

                RenderResult(result);
                if (result.StructuredParseFailed)
                {
                    _statusLabel.Text = result.RepeatedFormatFailure
                        ? "模型格式连续异常 · 已显示原始输出"
                        : "格式整理失败 · 已显示原始输出";
                    _statusLabel.ForeColor = Color.FromArgb(180, 83, 9);
                }
                else
                {
                    _statusLabel.Text = GetCompletionStatus(target.PromptName);
                    _statusLabel.ForeColor = Color.FromArgb(22, 163, 74);
                }
                _copyButton.Enabled = true;
            }
            catch (OperationCanceledException)
            {
                if (requestVersion == _requestVersion)
                {
                    if (!cancellation.IsCancellationRequested
                        && _currentResult != null
                        && _currentResult.IsPartial)
                    {
                        _statusLabel.Text = _currentResult.Kind == TranslationContentKind.Word
                            ? "AI 响应超时 · 已保留本地词典结果"
                            : "AI 响应超时 · 已保留本地译文";
                        _statusLabel.ForeColor = Color.FromArgb(180, 83, 9);
                        _copyButton.Enabled = true;
                    }
                    else
                    {
                        _statusLabel.Text = "已取消";
                        _statusLabel.ForeColor = UiTheme.Muted;
                    }
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
                if (_currentResult != null && _currentResult.IsPartial)
                {
                    _statusLabel.Text = _currentResult.Kind == TranslationContentKind.Word
                        ? "AI 补充失败 · 已保留本地词典结果"
                        : "AI 解析失败 · 已保留本地译文";
                    _statusLabel.ForeColor = Color.FromArgb(180, 83, 9);
                    _copyButton.Enabled = true;
                    return;
                }
                ShowMessageCard("无法完成翻译", ex.Message, true);
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
            if (_currentResult == null)
            {
                return;
            }
            try
            {
                var copyText = _currentResult.ToClipboardText();
                if (string.IsNullOrWhiteSpace(copyText))
                {
                    return;
                }
                Clipboard.SetText(copyText);
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
            _sourceRowStyle.Height = sourceText.Length <= 180
                && sourceText.IndexOf('\n') < 0
                ? 70f
                : 104f;

            var target = _targetBox.SelectedItem as LanguageOption;
            var detailed = target != null
                && EnglishInputClassifier.ShouldUseDetailedMode(
                    _config.DetailedEnglishEnabled,
                    sourceText,
                    target.PromptName);
            if (detailed)
            {
                var kind = EnglishInputClassifier.Classify(sourceText);
                Size = kind == TranslationContentKind.Sentence
                    ? new Size(640, 600)
                    : new Size(620, 650);
            }
            else if (sourceText.Length <= 100 && sourceText.IndexOf('\n') < 0)
            {
                Size = new Size(560, 440);
            }
            else
            {
                Size = new Size(620, 520);
            }
        }

        private void ClearResult()
        {
            _currentResult = null;
            _resultModeLabel.Visible = false;
            _resultCards.SuspendLayout();
            while (_resultCards.Controls.Count > 0)
            {
                var control = _resultCards.Controls[0];
                _resultCards.Controls.RemoveAt(0);
                control.Dispose();
            }
            _resultCards.RowStyles.Clear();
            _resultCards.RowCount = 0;
            _resultCards.ResumeLayout(true);
        }

        private void RenderResult(TranslationResult result)
        {
            var preserveScroll = _currentResult != null
                && _currentResult.IsPartial
                && !result.IsPartial;
            var scrollY = preserveScroll
                ? -_resultViewport.AutoScrollPosition.Y
                : 0;
            ClearResult();
            _currentResult = result;
            _resultModeLabel.Text = GetModeText(result);
            _resultModeLabel.Visible = true;

            _resultCards.SuspendLayout();
            if (result.IsStructured)
            {
                if (!string.IsNullOrWhiteSpace(result.Heading)
                    || !string.IsNullOrWhiteSpace(result.Subheading))
                {
                    AddHeroCard(result.Heading, result.Subheading);
                }

                if (!string.IsNullOrWhiteSpace(result.Translation))
                {
                    AddResultCard(
                        result.Kind == TranslationContentKind.Sentence ? "自然翻译" : "核心释义",
                        new[]
                        {
                            new TranslationItem(null, result.Translation, null)
                        },
                        true,
                        false);
                }

                foreach (var section in result.Sections)
                {
                    AddResultCard(section.Title, section.Items, false, false);
                }
            }
            else
            {
                var warning = result.StructuredParseFailed
                    ? (result.RepeatedFormatFailure
                        ? "模型连续多次没有返回可整理的格式，以下保留其原始输出。"
                        : "模型返回格式无法整理，以下保留其原始输出。")
                    : null;
                AddResultCard(
                    result.StructuredParseFailed ? "模型原始输出" : "译文",
                    new[]
                    {
                        new TranslationItem(null, result.Translation, warning)
                    },
                    !result.StructuredParseFailed,
                    result.StructuredParseFailed);
            }
            _resultCards.ResumeLayout(true);
            UpdateResultCardWidths();
            _resultViewport.AutoScrollPosition = preserveScroll
                ? new Point(0, scrollY)
                : Point.Empty;
        }

        private void ShowMessageCard(string title, string message, bool danger)
        {
            ClearResult();
            _resultModeLabel.Text = "错误";
            _resultModeLabel.Visible = true;
            AddResultCard(
                title,
                new[] { new TranslationItem(null, message, null) },
                false,
                danger);
            UpdateResultCardWidths();
        }

        private void AddHeroCard(string heading, string subheading)
        {
            var card = CreateResultCard(Color.FromArgb(239, 246, 255));
            var content = CreateCardLayout();

            if (!string.IsNullOrWhiteSpace(heading))
            {
                AddCardControl(
                    content,
                    CreateWrappedLabel(
                        heading,
                        UiTheme.Font(18f, FontStyle.Bold),
                        UiTheme.Text,
                        new Padding(0, 0, 0, 3)));
            }
            if (!string.IsNullOrWhiteSpace(subheading))
            {
                AddCardControl(
                    content,
                    CreateWrappedLabel(
                        subheading,
                        UiTheme.Font(9.5f, FontStyle.Regular),
                        UiTheme.Muted,
                        new Padding(0, 0, 0, 0)));
            }

            card.Controls.Add(content);
            AddCardToResults(card);
        }

        private void AddResultCard(
            string title,
            System.Collections.Generic.IEnumerable<TranslationItem> items,
            bool featured,
            bool warning)
        {
            var background = warning
                ? Color.FromArgb(255, 251, 235)
                : (featured ? Color.FromArgb(239, 246, 255) : UiTheme.Card);
            var card = CreateResultCard(background);
            var content = CreateCardLayout();

            if (!string.IsNullOrWhiteSpace(title))
            {
                AddCardControl(
                    content,
                    CreateWrappedLabel(
                        title,
                        UiTheme.Font(9f, FontStyle.Bold),
                        warning ? Color.FromArgb(180, 83, 9) : UiTheme.Primary,
                        new Padding(0, 0, 0, 10)));
            }

            var firstItem = true;
            foreach (var item in items)
            {
                if (!firstItem)
                {
                    var divider = new Panel();
                    divider.Height = 1;
                    divider.Dock = DockStyle.Fill;
                    divider.BackColor = UiTheme.Border;
                    divider.Margin = new Padding(0, 10, 0, 10);
                    AddCardControl(content, divider);
                }
                firstItem = false;

                if (!string.IsNullOrWhiteSpace(item.Heading))
                {
                    AddCardControl(
                        content,
                        CreateWrappedLabel(
                            item.Heading,
                            UiTheme.Font(10f, FontStyle.Bold),
                            UiTheme.Text,
                            new Padding(0, 0, 0, 5)));
                }
                if (!string.IsNullOrWhiteSpace(item.Body))
                {
                    AddCardControl(
                        content,
                        CreateWrappedLabel(
                            item.Body,
                            UiTheme.Font(featured ? 11.5f : 10.25f, FontStyle.Regular),
                            UiTheme.Text,
                            new Padding(0, 0, 0, string.IsNullOrWhiteSpace(item.Note) ? 0 : 7)));
                }
                if (!string.IsNullOrWhiteSpace(item.Note))
                {
                    AddCardControl(
                        content,
                        CreateWrappedLabel(
                            item.Note,
                            UiTheme.Font(9.25f, FontStyle.Regular),
                            warning ? Color.FromArgb(146, 64, 14) : UiTheme.Muted,
                            new Padding(0)));
                }
            }

            card.Controls.Add(content);
            AddCardToResults(card);
        }

        private Panel CreateResultCard(Color background)
        {
            var card = CreateSurfacePanel(background);
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.Padding = new Padding(16, 14, 16, 14);
            card.Margin = new Padding(0, 0, 0, 10);
            return card;
        }

        private static TableLayoutPanel CreateCardLayout()
        {
            var layout = new TableLayoutPanel();
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.Dock = DockStyle.Top;
            layout.Margin = new Padding(0);
            return layout;
        }

        private static void AddCardControl(TableLayoutPanel layout, Control control)
        {
            var row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(control, 0, row);
        }

        private void AddCardToResults(Panel card)
        {
            var row = _resultCards.RowCount++;
            _resultCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _resultCards.Controls.Add(card, 0, row);
        }

        private Label CreateWrappedLabel(
            string text,
            Font font,
            Color color,
            Padding margin)
        {
            var label = new Label();
            label.Text = text.Trim();
            label.AutoSize = true;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Margin = margin;
            label.Tag = "wrap";
            label.MaximumSize = new Size(
                Math.Max(240, _resultViewport.ClientSize.Width - 58),
                0);
            return label;
        }

        private void UpdateResultCardWidths()
        {
            if (_resultViewport == null || _resultCards == null)
            {
                return;
            }
            var width = Math.Max(
                280,
                _resultViewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            _resultCards.Width = width;

            foreach (Control card in _resultCards.Controls)
            {
                card.MinimumSize = new Size(width, 0);
                card.MaximumSize = new Size(width, 0);
                UpdateWrappedLabelWidths(card, Math.Max(230, width - 36));
            }
        }

        private static void UpdateWrappedLabelWidths(Control parent, int width)
        {
            foreach (Control control in parent.Controls)
            {
                var label = control as Label;
                if (label != null && string.Equals(Convert.ToString(label.Tag), "wrap"))
                {
                    label.MaximumSize = new Size(width, 0);
                }
                if (control.HasChildren)
                {
                    UpdateWrappedLabelWidths(control, width);
                }
            }
        }

        private static string GetModeText(TranslationResult result)
        {
            if (result.StructuredParseFailed)
            {
                return "原始输出";
            }
            switch (result.Kind)
            {
                case TranslationContentKind.Word:
                    return "单词详解";
                case TranslationContentKind.Phrase:
                    return "短语详解";
                case TranslationContentKind.Sentence:
                    return "句子解析";
                default:
                    return "快速翻译";
            }
        }

        private string GetCompletionStatus(string targetLanguage)
        {
            var detailed = EnglishInputClassifier.ShouldUseDetailedMode(
                _config.DetailedEnglishEnabled,
                _sourceText,
                targetLanguage);
            if (!detailed
                || string.Equals(
                    _config.DetailedTranslationProvider,
                    "Local",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "完成 · 本机处理";
            }
            if (string.Equals(
                _config.DetailedTranslationProvider,
                "Hybrid",
                StringComparison.OrdinalIgnoreCase))
            {
                return "完成 · 智能混合";
            }
            return string.Equals(
                _config.DetailedTranslationProvider,
                "DeepSeekPro",
                StringComparison.OrdinalIgnoreCase)
                ? "完成 · DeepSeek V4 Pro"
                : "完成 · DeepSeek V4 Flash";
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

        private static Panel CreateSurfacePanel(Color background)
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = background;
            panel.BorderStyle = BorderStyle.FixedSingle;
            return panel;
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
