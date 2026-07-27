using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal sealed class SettingsForm : Form
    {
        private readonly TextBox _apiBox;
        private readonly TextBox _serverBox;
        private readonly ComboBox _modelBox;
        private readonly CheckBox _autoStartBox;
        private readonly CheckBox _stopServerBox;
        private readonly NumericUpDown _contextBox;
        private readonly NumericUpDown _timeoutBox;
        private readonly ComboBox _chineseTargetBox;
        private readonly ComboBox _foreignTargetBox;
        private readonly CheckBox _detailedEnglishBox;
        private readonly ComboBox _detailedProviderBox;
        private readonly TextBox _deepSeekKeyBox;
        private readonly Label _providerHintLabel;

        internal SettingsForm(AppConfig config, Icon appIcon)
        {
            Text = "设置 · 本地 AI 划词翻译";
            Icon = appIcon;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(720, 665);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9.5f, FontStyle.Regular);

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 16, 24, 14);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = "翻译设置";
            title.AutoSize = true;
            title.Font = UiTheme.Font(16f, FontStyle.Bold);
            title.ForeColor = UiTheme.Text;
            title.Margin = new Padding(0, 0, 0, 12);
            root.Controls.Add(title, 0, 0);

            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 3;
            table.RowCount = 9;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72f));
            for (var row = 0; row < table.RowCount; row++)
            {
                table.RowStyles.Add(new RowStyle(
                    SizeType.Absolute,
                    row == 7 ? 54f : (row == 8 ? 145f : 43f)));
            }
            root.Controls.Add(table, 0, 1);

            _apiBox = CreateTextBox(config.ApiBaseUrl);
            AddLabel(table, "API 地址", 0);
            table.Controls.Add(_apiBox, 1, 0);
            table.SetColumnSpan(_apiBox, 2);

            _serverBox = CreateTextBox(config.ServerExecutable);
            AddLabel(table, "llama-server.exe", 1);
            table.Controls.Add(_serverBox, 1, 1);
            table.Controls.Add(CreateBrowseButton(_serverBox, "程序文件|*.exe|所有文件|*.*"), 2, 1);

            _modelBox = CreateModelCombo(config.ModelFile, config.ServerExecutable);
            AddLabel(table, "GGUF 模型", 2);
            table.Controls.Add(_modelBox, 1, 2);
            table.Controls.Add(CreateModelBrowseButton(), 2, 2);

            AddLabel(table, "中文默认译为", 3);
            _chineseTargetBox = CreateLanguageCombo();
            table.Controls.Add(_chineseTargetBox, 1, 3);
            table.SetColumnSpan(_chineseTargetBox, 2);
            SelectCombo(_chineseTargetBox, config.TargetForChinese);

            AddLabel(table, "外文默认译为", 4);
            _foreignTargetBox = CreateLanguageCombo();
            table.Controls.Add(_foreignTargetBox, 1, 4);
            table.SetColumnSpan(_foreignTargetBox, 2);
            SelectCombo(_foreignTargetBox, config.TargetForForeign);

            AddLabel(table, "模型上下文", 5);
            _contextBox = new NumericUpDown();
            _contextBox.Minimum = 512;
            _contextBox.Maximum = 131072;
            _contextBox.Increment = 512;
            _contextBox.Value = Math.Max(512, Math.Min(131072, config.ContextSize));
            _contextBox.Width = 140;
            _contextBox.Margin = new Padding(0, 7, 0, 0);
            table.Controls.Add(_contextBox, 1, 5);

            AddLabel(table, "悬浮按钮停留", 6);
            _timeoutBox = new NumericUpDown();
            _timeoutBox.Minimum = 2;
            _timeoutBox.Maximum = 60;
            _timeoutBox.Value = Math.Max(2, Math.Min(60, config.ButtonTimeoutSeconds));
            _timeoutBox.Width = 140;
            _timeoutBox.Margin = new Padding(0, 7, 0, 0);
            table.Controls.Add(_timeoutBox, 1, 6);
            var seconds = new Label();
            seconds.Text = "秒";
            seconds.AutoSize = true;
            seconds.ForeColor = UiTheme.Muted;
            seconds.Margin = new Padding(8, 11, 0, 0);
            table.Controls.Add(seconds, 2, 6);

            var checks = new FlowLayoutPanel();
            checks.Dock = DockStyle.Fill;
            checks.FlowDirection = FlowDirection.TopDown;
            checks.WrapContents = false;
            checks.Margin = new Padding(0, 4, 0, 0);
            _autoStartBox = new CheckBox();
            _autoStartBox.Text = "启动软件时同时启动本地模型服务";
            _autoStartBox.AutoSize = true;
            _autoStartBox.Checked = config.AutoStartServer;
            _stopServerBox = new CheckBox();
            _stopServerBox.Text = "退出软件时关闭由本软件启动的模型服务";
            _stopServerBox.AutoSize = true;
            _stopServerBox.Checked = config.StopOwnedServerOnExit;
            checks.Controls.Add(_autoStartBox);
            checks.Controls.Add(_stopServerBox);
            AddLabel(table, "模型服务", 7);
            table.Controls.Add(checks, 1, 7);
            table.SetColumnSpan(checks, 2);

            AddLabel(table, "英语学习模式", 8);
            var detailedPanel = new TableLayoutPanel();
            detailedPanel.Dock = DockStyle.Fill;
            detailedPanel.ColumnCount = 2;
            detailedPanel.RowCount = 4;
            detailedPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86f));
            detailedPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            detailedPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            detailedPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 37f));
            detailedPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 37f));
            detailedPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            detailedPanel.Margin = new Padding(0, 5, 0, 0);

            _detailedEnglishBox = new CheckBox();
            _detailedEnglishBox.Text = "开启英文详细翻译";
            _detailedEnglishBox.AutoSize = true;
            _detailedEnglishBox.Checked = config.DetailedEnglishEnabled;
            _detailedEnglishBox.Font = UiTheme.Font(9.5f, FontStyle.Bold);
            _detailedEnglishBox.Margin = new Padding(0, 2, 0, 0);
            detailedPanel.Controls.Add(_detailedEnglishBox, 0, 0);
            detailedPanel.SetColumnSpan(_detailedEnglishBox, 2);

            var providerLabel = CreateInlineLabel("处理引擎");
            detailedPanel.Controls.Add(providerLabel, 0, 1);
            _detailedProviderBox = new ComboBox();
            _detailedProviderBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _detailedProviderBox.Dock = DockStyle.Fill;
            _detailedProviderBox.Margin = new Padding(0, 4, 0, 4);
            _detailedProviderBox.Items.AddRange(new object[]
            {
                new ProviderOption("智能混合（推荐：先显示本地结果）", "Hybrid"),
                new ProviderOption("DeepSeek V4 Pro（推荐：完整学习）", "DeepSeekPro"),
                new ProviderOption("DeepSeek V4 Flash（速度优先）", "DeepSeekFlash"),
                new ProviderOption("本地词典 + 本地 AI（完全离线）", "Local")
            });
            SelectProvider(_detailedProviderBox, config.DetailedTranslationProvider);
            detailedPanel.Controls.Add(_detailedProviderBox, 1, 1);

            var keyLabel = CreateInlineLabel("API 密钥");
            detailedPanel.Controls.Add(keyLabel, 0, 2);
            _deepSeekKeyBox = new TextBox();
            _deepSeekKeyBox.Dock = DockStyle.Fill;
            _deepSeekKeyBox.Margin = new Padding(0, 5, 0, 5);
            _deepSeekKeyBox.Font = UiTheme.Font(9f, FontStyle.Regular);
            _deepSeekKeyBox.UseSystemPasswordChar = true;
            _deepSeekKeyBox.Text = config.GetDeepSeekApiKey();
            detailedPanel.Controls.Add(_deepSeekKeyBox, 1, 2);

            _providerHintLabel = new Label();
            _providerHintLabel.AutoSize = true;
            _providerHintLabel.MaximumSize = new Size(475, 0);
            _providerHintLabel.ForeColor = UiTheme.Muted;
            _providerHintLabel.Margin = new Padding(0, 5, 0, 0);
            detailedPanel.Controls.Add(_providerHintLabel, 0, 3);
            detailedPanel.SetColumnSpan(_providerHintLabel, 2);

            _detailedProviderBox.SelectedIndexChanged += delegate { UpdateDeepSeekControls(); };
            UpdateDeepSeekControls();
            table.Controls.Add(detailedPanel, 1, 8);
            table.SetColumnSpan(detailedPanel, 2);

            var configHint = new Label();
            configHint.Text = "配置保存在：" + AppConfig.ConfigPath;
            configHint.AutoSize = true;
            configHint.ForeColor = UiTheme.Muted;
            configHint.Margin = new Padding(0, 8, 0, 14);
            root.Controls.Add(configHint, 0, 2);

            var footer = new FlowLayoutPanel();
            footer.AutoSize = true;
            footer.Dock = DockStyle.Fill;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.WrapContents = false;
            footer.Margin = new Padding(0);

            var cancelButton = UiTheme.CreateSecondaryButton("取消");
            cancelButton.Width = 88;
            cancelButton.Margin = new Padding(10, 0, 0, 0);
            cancelButton.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(cancelButton);

            var saveButton = UiTheme.CreatePrimaryButton("保存");
            saveButton.Width = 88;
            saveButton.Margin = new Padding(0);
            saveButton.Click += SaveClicked;
            footer.Controls.Add(saveButton);
            root.Controls.Add(footer, 0, 3);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        internal void ApplyTo(AppConfig config)
        {
            config.ApiBaseUrl = _apiBox.Text.Trim().TrimEnd('/');
            config.ServerExecutable = _serverBox.Text.Trim();
            config.ModelFile = GetSelectedModelPath();
            config.AutoStartServer = _autoStartBox.Checked;
            config.StopOwnedServerOnExit = _stopServerBox.Checked;
            config.ContextSize = (int)_contextBox.Value;
            config.ButtonTimeoutSeconds = (int)_timeoutBox.Value;
            config.TargetForChinese = Convert.ToString(_chineseTargetBox.SelectedItem);
            config.TargetForForeign = Convert.ToString(_foreignTargetBox.SelectedItem);
            config.DetailedEnglishEnabled = _detailedEnglishBox.Checked;
            var provider = _detailedProviderBox.SelectedItem as ProviderOption;
            config.DetailedTranslationProvider = provider == null
                ? "Hybrid"
                : provider.Value;
            config.SetDeepSeekApiKey(_deepSeekKeyBox.Text);
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            Uri uri;
            if (!Uri.TryCreate(_apiBox.Text.Trim(), UriKind.Absolute, out uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show(this, "请输入有效的 HTTP API 地址。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _apiBox.Focus();
                return;
            }

            if (_autoStartBox.Checked && !File.Exists(_serverBox.Text.Trim()))
            {
                MessageBox.Show(this, "找不到 llama-server.exe。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _serverBox.Focus();
                return;
            }
            if (_autoStartBox.Checked && !File.Exists(GetSelectedModelPath()))
            {
                MessageBox.Show(this, "找不到 GGUF 模型文件。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _modelBox.Focus();
                return;
            }

            var selectedProvider = _detailedProviderBox.SelectedItem as ProviderOption;
            if (_detailedEnglishBox.Checked
                && selectedProvider != null
                && !string.Equals(selectedProvider.Value, "Local", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(_deepSeekKeyBox.Text))
            {
                MessageBox.Show(
                    this,
                    "使用 DeepSeek 详细翻译前，请输入 API 密钥；也可以改选“本地模型”。",
                    "设置",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _deepSeekKeyBox.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static TextBox CreateTextBox(string text)
        {
            var box = new TextBox();
            box.Text = text;
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(0, 7, 8, 7);
            box.Font = UiTheme.Font(9f, FontStyle.Regular);
            return box;
        }

        private static ComboBox CreateModelCombo(string selectedModel, string serverExecutable)
        {
            var combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(0, 6, 8, 6);
            combo.Font = UiTheme.Font(9f, FontStyle.Regular);
            combo.DropDownWidth = 510;

            var modelPaths = FindModelFiles(selectedModel, serverExecutable);
            foreach (var modelPath in modelPaths)
            {
                combo.Items.Add(new ModelOption(modelPath));
            }

            SelectModel(combo, selectedModel);
            return combo;
        }

        private static List<string> FindModelFiles(string selectedModel, string serverExecutable)
        {
            var directories = new List<string>();
            AddDirectory(directories, Path.GetDirectoryName(selectedModel));

            var serverDirectory = Path.GetDirectoryName(serverExecutable);
            if (!string.IsNullOrWhiteSpace(serverDirectory))
            {
                AddDirectory(directories, Path.Combine(serverDirectory, "Models"));
            }

            var paths = new List<string>();
            foreach (var directory in directories)
            {
                try
                {
                    foreach (var path in Directory.GetFiles(directory, "*.gguf", SearchOption.TopDirectoryOnly))
                    {
                        AddPath(paths, path);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedModel))
            {
                AddPath(paths, selectedModel);
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static void AddDirectory(List<string> directories, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }
            foreach (var existing in directories)
            {
                if (string.Equals(existing, directory, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            directories.Add(directory);
        }

        private static void AddPath(List<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
            foreach (var existing in paths)
            {
                if (string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            paths.Add(path);
        }

        private static void SelectModel(ComboBox combo, string modelPath)
        {
            for (var index = 0; index < combo.Items.Count; index++)
            {
                var option = combo.Items[index] as ModelOption;
                if (option != null
                    && string.Equals(option.FilePath, modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = index;
                    return;
                }
            }
            combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
        }

        private string GetSelectedModelPath()
        {
            var option = _modelBox.SelectedItem as ModelOption;
            return option == null ? string.Empty : option.FilePath;
        }

        private static void AddLabel(TableLayoutPanel table, string text, int row)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = UiTheme.Text;
            label.Margin = new Padding(0, 11, 8, 0);
            table.Controls.Add(label, 0, row);
        }

        private static Label CreateInlineLabel(string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = UiTheme.Text;
            label.Margin = new Padding(0, 9, 8, 0);
            return label;
        }

        private void UpdateDeepSeekControls()
        {
            var provider = _detailedProviderBox.SelectedItem as ProviderOption;
            var value = provider == null ? "Hybrid" : provider.Value;
            _deepSeekKeyBox.Enabled = !string.Equals(
                value,
                "Local",
                StringComparison.OrdinalIgnoreCase);
            if (string.Equals(value, "Local", StringComparison.OrdinalIgnoreCase))
            {
                _providerHintLabel.Text =
                    "完全离线：单词先查本地词典，再由本地 AI 补充；不会发送任何文本或密钥。";
            }
            else if (string.Equals(value, "Hybrid", StringComparison.OrdinalIgnoreCase))
            {
                _providerHintLabel.Text =
                    "单词会立即显示本地词典结果，再由 V4 Flash 补充；密钥由 Windows 当前用户加密保存。";
            }
            else if (string.Equals(value, "DeepSeekFlash", StringComparison.OrdinalIgnoreCase))
            {
                _providerHintLabel.Text =
                    "速度优先：使用 V4 Flash；单词仍会先显示本地词典结果。";
            }
            else
            {
                _providerHintLabel.Text =
                    "完整度优先：使用 V4 Pro；单词仍会先显示本地词典结果。";
            }
        }

        private static void SelectProvider(ComboBox combo, string value)
        {
            for (var index = 0; index < combo.Items.Count; index++)
            {
                var option = combo.Items[index] as ProviderOption;
                if (option != null
                    && string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = index;
                    return;
                }
            }
            combo.SelectedIndex = 0;
        }

        private Button CreateBrowseButton(TextBox target, string filter)
        {
            var button = UiTheme.CreateSecondaryButton("浏览");
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 5, 0, 5);
            button.Click += delegate
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = filter;
                    dialog.CheckFileExists = true;
                    if (!string.IsNullOrWhiteSpace(target.Text))
                    {
                        var directory = Path.GetDirectoryName(target.Text);
                        if (Directory.Exists(directory))
                        {
                            dialog.InitialDirectory = directory;
                        }
                    }
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        target.Text = dialog.FileName;
                    }
                }
            };
            return button;
        }

        private Button CreateModelBrowseButton()
        {
            var button = UiTheme.CreateSecondaryButton("浏览");
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 5, 0, 5);
            button.Click += delegate
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "GGUF 模型|*.gguf|所有文件|*.*";
                    dialog.CheckFileExists = true;

                    var selectedPath = GetSelectedModelPath();
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        var directory = Path.GetDirectoryName(selectedPath);
                        if (Directory.Exists(directory))
                        {
                            dialog.InitialDirectory = directory;
                        }
                    }

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        var option = new ModelOption(dialog.FileName);
                        _modelBox.Items.Add(option);
                        _modelBox.SelectedItem = option;
                    }
                }
            };
            return button;
        }

        private static ComboBox CreateLanguageCombo()
        {
            var combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(0, 6, 8, 6);
            combo.Items.AddRange(new object[]
            {
                "简体中文",
                "English",
                "日本語",
                "한국어",
                "Français",
                "Deutsch",
                "Español",
                "Русский",
                "Português",
                "Italiano",
                "繁體中文"
            });
            return combo;
        }

        private static void SelectCombo(ComboBox combo, string value)
        {
            var index = combo.FindStringExact(value);
            combo.SelectedIndex = index >= 0 ? index : 0;
        }

        private sealed class ModelOption
        {
            internal ModelOption(string filePath)
            {
                FilePath = filePath;
            }

            internal string FilePath { get; private set; }

            public override string ToString()
            {
                var fileName = Path.GetFileName(FilePath);
                try
                {
                    var sizeInGb = new FileInfo(FilePath).Length / (1024d * 1024d * 1024d);
                    return fileName + "  (" + sizeInGb.ToString("0.00") + " GB)";
                }
                catch
                {
                    return fileName + "  (文件不存在)";
                }
            }
        }

        private sealed class ProviderOption
        {
            internal ProviderOption(string displayName, string value)
            {
                DisplayName = displayName;
                Value = value;
            }

            internal string DisplayName { get; private set; }
            internal string Value { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
