using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal sealed class SentenceAnalysisRequestedEventArgs : EventArgs
    {
        internal SentenceAnalysisRequestedEventArgs(string sentence, Point anchor)
        {
            Sentence = sentence;
            Anchor = anchor;
        }

        internal string Sentence { get; private set; }
        internal Point Anchor { get; private set; }
    }

    internal sealed class ArticleReaderForm : Form
    {
        private readonly TextBox _searchBox;
        private readonly ComboBox _sourceBox;
        private readonly ComboBox _topicBox;
        private readonly ComboBox _difficultyBox;
        private readonly ComboBox _lengthBox;
        private readonly Label _countLabel;
        private readonly ListBox _articleList;
        private readonly Label _titleLabel;
        private readonly Label _metadataLabel;
        private readonly WebBrowser _articleBrowser;
        private readonly LinkLabel _sourceLink;
        private readonly Label _dictionaryLabel;
        private readonly Button _analyzeButton;
        private readonly Button _openInBrowserButton;
        private readonly List<OfflineArticle> _allArticles;
        private OfflineArticle _currentArticle;

        internal event EventHandler<SentenceAnalysisRequestedEventArgs>
            SentenceAnalysisRequested;

        internal ArticleReaderForm(Icon appIcon)
        {
            Text = "高考趋势 · 离线英语阅读库";
            Icon = appIcon;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(1000, 680);
            Size = new Size(1280, 820);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9f, FontStyle.Regular);

            _allArticles = new List<OfflineArticle>(
                OfflineArticleRepository.GetArticles());

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            var navigation = new TableLayoutPanel();
            navigation.Dock = DockStyle.Fill;
            navigation.Padding = new Padding(14);
            navigation.BackColor = Color.White;
            navigation.ColumnCount = 1;
            navigation.RowCount = 5;
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Controls.Add(navigation, 0, 0);

            var libraryTitle = new Label();
            libraryTitle.Text = "离线英语阅读库";
            libraryTitle.Dock = DockStyle.Fill;
            libraryTitle.Font = UiTheme.Font(13f, FontStyle.Bold);
            libraryTitle.ForeColor = UiTheme.Text;
            libraryTitle.TextAlign = ContentAlignment.MiddleLeft;
            navigation.Controls.Add(libraryTitle, 0, 0);

            _searchBox = new TextBox();
            _searchBox.Dock = DockStyle.Fill;
            _searchBox.Font = UiTheme.Font(10.5f, FontStyle.Regular);
            _searchBox.Margin = new Padding(0, 4, 0, 6);
            _searchBox.TextChanged += delegate { ApplyFilters(); };
            navigation.Controls.Add(_searchBox, 0, 1);

            var filters = new TableLayoutPanel();
            filters.Dock = DockStyle.Fill;
            filters.ColumnCount = 2;
            filters.RowCount = 2;
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            filters.Margin = new Padding(0);
            _sourceBox = CreateFilterBox();
            _topicBox = CreateFilterBox();
            _difficultyBox = CreateFilterBox();
            _lengthBox = CreateFilterBox();
            filters.Controls.Add(_sourceBox, 0, 0);
            filters.Controls.Add(_topicBox, 1, 0);
            filters.Controls.Add(_difficultyBox, 0, 1);
            filters.Controls.Add(_lengthBox, 1, 1);
            navigation.Controls.Add(filters, 0, 2);

            _countLabel = new Label();
            _countLabel.Dock = DockStyle.Fill;
            _countLabel.ForeColor = UiTheme.Muted;
            _countLabel.TextAlign = ContentAlignment.MiddleLeft;
            navigation.Controls.Add(_countLabel, 0, 3);

            _articleList = new ListBox();
            _articleList.Dock = DockStyle.Fill;
            _articleList.BorderStyle = BorderStyle.None;
            _articleList.DrawMode = DrawMode.OwnerDrawFixed;
            _articleList.ItemHeight = 64;
            _articleList.IntegralHeight = false;
            _articleList.BackColor = Color.White;
            _articleList.DrawItem += DrawArticleItem;
            _articleList.SelectedIndexChanged += ArticleSelectionChanged;
            navigation.Controls.Add(_articleList, 0, 4);

            var reading = new TableLayoutPanel();
            reading.Dock = DockStyle.Fill;
            reading.Padding = new Padding(22, 16, 22, 16);
            reading.ColumnCount = 1;
            reading.RowCount = 5;
            reading.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
            reading.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            reading.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            reading.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            reading.RowStyles.Add(new RowStyle(SizeType.Absolute, 142f));
            root.Controls.Add(reading, 1, 0);

            _titleLabel = new Label();
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.Font = UiTheme.Font(16f, FontStyle.Bold);
            _titleLabel.ForeColor = UiTheme.Text;
            _titleLabel.AutoEllipsis = true;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            reading.Controls.Add(_titleLabel, 0, 0);

            _metadataLabel = new Label();
            _metadataLabel.Dock = DockStyle.Fill;
            _metadataLabel.ForeColor = UiTheme.Muted;
            _metadataLabel.Font = UiTheme.Font(9.5f, FontStyle.Regular);
            _metadataLabel.AutoEllipsis = true;
            _metadataLabel.TextAlign = ContentAlignment.MiddleLeft;
            reading.Controls.Add(_metadataLabel, 0, 1);

            _articleBrowser = new WebBrowser();
            _articleBrowser.Dock = DockStyle.Fill;
            _articleBrowser.AllowNavigation = false;
            _articleBrowser.AllowWebBrowserDrop = false;
            _articleBrowser.IsWebBrowserContextMenuEnabled = false;
            _articleBrowser.ScriptErrorsSuppressed = true;
            _articleBrowser.WebBrowserShortcutsEnabled = true;
            _articleBrowser.Margin = new Padding(0, 6, 0, 8);
            _articleBrowser.DocumentCompleted += ArticleDocumentCompleted;
            reading.Controls.Add(_articleBrowser, 0, 2);

            var actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 4;
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.Margin = new Padding(0);
            _analyzeButton = UiTheme.CreatePrimaryButton("按原模式翻译选中句");
            _analyzeButton.Width = 168;
            _analyzeButton.Margin = new Padding(0, 4, 10, 4);
            _analyzeButton.Click += AnalyzeSentenceClicked;
            actions.Controls.Add(_analyzeButton, 0, 0);

            _openInBrowserButton = UiTheme.CreateSecondaryButton("浏览器打开");
            _openInBrowserButton.Width = 108;
            _openInBrowserButton.Margin = new Padding(0, 4, 10, 4);
            _openInBrowserButton.Click += OpenInBrowserClicked;
            actions.Controls.Add(_openInBrowserButton, 1, 0);

            var hint = new Label();
            hint.Text = "单击单词：本地瞬时查词 · 句子：沿用原有翻译设置";
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = UiTheme.Muted;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            actions.Controls.Add(hint, 2, 0);

            _sourceLink = new LinkLabel();
            _sourceLink.Text = "查看原始来源";
            _sourceLink.AutoSize = true;
            _sourceLink.Margin = new Padding(8, 13, 0, 0);
            _sourceLink.LinkClicked += OpenSource;
            actions.Controls.Add(_sourceLink, 3, 0);
            reading.Controls.Add(actions, 0, 3);

            var dictionarySurface = new Panel();
            dictionarySurface.Dock = DockStyle.Fill;
            dictionarySurface.BackColor = Color.FromArgb(238, 244, 255);
            dictionarySurface.Padding = new Padding(14, 10, 14, 10);
            dictionarySurface.Margin = new Padding(0);
            _dictionaryLabel = new Label();
            _dictionaryLabel.Dock = DockStyle.Fill;
            _dictionaryLabel.ForeColor = UiTheme.Text;
            _dictionaryLabel.Font = UiTheme.Font(10.5f, FontStyle.Regular);
            _dictionaryLabel.AutoEllipsis = true;
            _dictionaryLabel.Text =
                "本地词典已就绪。单击正文中的英文单词即可查看中文释义。";
            dictionarySurface.Controls.Add(_dictionaryLabel);
            reading.Controls.Add(dictionarySurface, 0, 4);

            PopulateFilters();
            ApplyFilters();

            if (_allArticles.Count == 0)
            {
                _titleLabel.Text = "文章库未载入";
                _metadataLabel.Text = OfflineArticleRepository.GetLoadError();
                _articleBrowser.DocumentText = BuildEmptyDocument(
                    "请重新构建软件，并确认 Articles\\offline-articles.json.gz 已随程序复制。");
            }
        }

        private ComboBox CreateFilterBox()
        {
            var box = new ComboBox();
            box.Dock = DockStyle.Fill;
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.Margin = new Padding(0, 2, 7, 2);
            box.SelectedIndexChanged += delegate { ApplyFilters(); };
            return box;
        }

        private void PopulateFilters()
        {
            PopulateFilter(
                _sourceBox, "全部来源",
                _allArticles.Select(item => item.Source).Distinct().OrderBy(x => x));
            PopulateFilter(
                _topicBox, "全部主题",
                _allArticles.Select(item => item.Topic).Distinct().OrderBy(x => x));
            PopulateFilter(
                _difficultyBox, "全部难度",
                new[] { "基础", "进阶", "挑战" });
            PopulateFilter(
                _lengthBox, "全部长度",
                new[] { "短篇", "中篇", "长篇" });
        }

        private static void PopulateFilter(
            ComboBox box,
            string allText,
            IEnumerable<string> values)
        {
            box.Items.Add(allText);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    box.Items.Add(value);
                }
            }
            box.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (_articleList == null)
            {
                return;
            }

            var query = (_searchBox.Text ?? string.Empty).Trim();
            var source = SelectedSpecificValue(_sourceBox);
            var topic = SelectedSpecificValue(_topicBox);
            var difficulty = SelectedSpecificValue(_difficultyBox);
            var length = SelectedSpecificValue(_lengthBox);
            var selectedId = _currentArticle == null ? null : _currentArticle.Id;

            var filtered = _allArticles.Where(item =>
                Matches(source, item.Source)
                && Matches(topic, item.Topic)
                && Matches(difficulty, item.Difficulty)
                && Matches(length, item.LengthBand)
                && (query.Length == 0
                    || Contains(item.Title, query)
                    || Contains(item.Topic, query)
                    || Contains(item.Section, query)
                    || Contains(item.Content, query)))
                .OrderByDescending(item => item.PublishedDate)
                .ThenBy(item => item.Title)
                .ToList();

            _articleList.BeginUpdate();
            _articleList.Items.Clear();
            foreach (var article in filtered)
            {
                _articleList.Items.Add(article);
            }
            _articleList.EndUpdate();
            _countLabel.Text = "共 " + filtered.Count + " 篇（本地离线）";

            if (filtered.Count == 0)
            {
                ShowArticle(null);
                return;
            }

            var selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                for (var index = 0; index < filtered.Count; index++)
                {
                    if (string.Equals(filtered[index].Id, selectedId, StringComparison.Ordinal))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }
            _articleList.SelectedIndex = selectedIndex;
        }

        private static string SelectedSpecificValue(ComboBox box)
        {
            return box.SelectedIndex <= 0 ? null : Convert.ToString(box.SelectedItem);
        }

        private static bool Matches(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected)
                || string.Equals(expected, actual, StringComparison.Ordinal);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawArticleItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= _articleList.Items.Count)
            {
                return;
            }

            var article = _articleList.Items[e.Index] as OfflineArticle;
            if (article == null)
            {
                return;
            }
            var selected = (e.State & DrawItemState.Selected) != 0;
            var titleColor = selected ? Color.White : UiTheme.Text;
            var metaColor = selected ? Color.FromArgb(220, 232, 255) : UiTheme.Muted;
            var bounds = e.Bounds;
            using (var titleFont = UiTheme.Font(10.2f, FontStyle.Bold))
            using (var metaFont = UiTheme.Font(8.6f, FontStyle.Regular))
            using (var titleBrush = new SolidBrush(titleColor))
            using (var metaBrush = new SolidBrush(metaColor))
            {
                var titleBounds = new Rectangle(
                    bounds.Left + 8, bounds.Top + 6, bounds.Width - 15, 34);
                e.Graphics.DrawString(
                    article.Title,
                    titleFont,
                    titleBrush,
                    titleBounds,
                    new StringFormat
                    {
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.LineLimit
                    });
                e.Graphics.DrawString(
                    article.Source + " · " + article.Difficulty + " · "
                    + article.WordCount + " 词",
                    metaFont,
                    metaBrush,
                    bounds.Left + 8,
                    bounds.Top + 44);
            }
            e.DrawFocusRectangle();
        }

        private void ArticleSelectionChanged(object sender, EventArgs e)
        {
            ShowArticle(_articleList.SelectedItem as OfflineArticle);
        }

        private void ShowArticle(OfflineArticle article)
        {
            _currentArticle = article;
            if (article == null)
            {
                _titleLabel.Text = "没有符合筛选条件的文章";
                _metadataLabel.Text = string.Empty;
                _articleBrowser.DocumentText = BuildEmptyDocument(
                    "没有符合当前筛选条件的文章。");
                _sourceLink.Enabled = false;
                _analyzeButton.Enabled = false;
                _openInBrowserButton.Enabled = false;
                return;
            }

            _titleLabel.Text = article.Title;
            _metadataLabel.Text =
                article.Source + " · " + article.Section + " · " + article.Topic
                + " · " + article.Difficulty + " · " + article.LengthBand
                + " · " + article.WordCount + " 词 · " + article.PublishedDate;
            _articleBrowser.DocumentText = BuildArticleDocument(article);
            _sourceLink.Enabled = !string.IsNullOrWhiteSpace(article.Url);
            _analyzeButton.Enabled = true;
            _openInBrowserButton.Enabled = true;
            _dictionaryLabel.Text =
                "来源许可：" + article.License
                + "\r\n单击正文中的英文单词，即刻显示本地中文释义。";
        }

        private void ArticleDocumentCompleted(
            object sender,
            WebBrowserDocumentCompletedEventArgs e)
        {
            if (_articleBrowser.Document == null)
            {
                return;
            }
            foreach (HtmlElement element in
                _articleBrowser.Document.GetElementsByTagName("span"))
            {
                if (string.Equals(
                    element.GetAttribute("className"),
                    "word",
                    StringComparison.OrdinalIgnoreCase))
                {
                    element.Click += ArticleWordElementClick;
                }
            }
        }

        private void ArticleWordElementClick(object sender, HtmlElementEventArgs e)
        {
            var element = sender as HtmlElement;
            if (element == null)
            {
                element = e.ToElement;
            }
            if (element == null)
            {
                return;
            }

            var word = element.GetAttribute("data-word");
            if (string.IsNullOrWhiteSpace(word))
            {
                word = element.InnerText;
            }
            var result = OfflineEnglishDictionary.CreatePreview(word);
            if (result == null)
            {
                _dictionaryLabel.Text =
                    word + "\r\n文章词汇 · 本地词典暂未找到释义";
                return;
            }
            _dictionaryLabel.Text = FormatDictionaryResult(result);
        }

        private static string BuildArticleDocument(OfflineArticle article)
        {
            var body = article == null ? string.Empty : article.Html;
            if (string.IsNullOrWhiteSpace(body) && article != null)
            {
                body = BuildFallbackParagraphs(article.Content);
            }
            return BuildDocument(body, null);
        }

        private static string BuildEmptyDocument(string message)
        {
            return BuildDocument(
                "<p class=\"empty\">" + EscapeHtml(message) + "</p>",
                "empty");
        }

        private static string BuildDocument(string body, string bodyClass)
        {
            return "<!doctype html><html><head>"
                + "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">"
                + "<meta charset=\"utf-8\">"
                + "<style>"
                + "html,body{margin:0;padding:0;background:#fff;color:#19202e;}"
                + "body{box-sizing:border-box;padding:28px 38px 64px;"
                + "font-family:Georgia,'Times New Roman','Segoe UI',serif;"
                + "font-size:21px;line-height:1.82;letter-spacing:.01em;}"
                + "p{margin:0 0 1.35em 0;max-width:920px;}"
                + "h2,h3{max-width:920px;margin:1.6em 0 .72em;"
                + "font-family:'Segoe UI','Microsoft YaHei UI',sans-serif;"
                + "line-height:1.35;color:#17243d;}"
                + "h2{font-size:1.28em;}h3{font-size:1.12em;}"
                + ".list-item{padding-left:1.25em;position:relative;}"
                + ".list-item:before{content:'•';position:absolute;left:.2em;color:#2563eb;}"
                + ".word{border-radius:4px;cursor:pointer;padding:1px 0;}"
                + ".word:hover{background:#e8f0ff;color:#174ea6;}"
                + ".empty{font-family:'Segoe UI','Microsoft YaHei UI',sans-serif;"
                + "color:#64748b;text-align:center;margin:6em auto;}"
                + "::selection{background:#cfe0ff;color:#111827;}"
                + "</style>"
                + "<script>function getSelectedText(){"
                + "if(window.getSelection){return window.getSelection().toString();}"
                + "if(document.selection){return document.selection.createRange().text;}"
                + "return '';}</script>"
                + "</head><body"
                + (string.IsNullOrWhiteSpace(bodyClass)
                    ? string.Empty
                    : " class=\"" + bodyClass + "\"")
                + ">"
                + (body ?? string.Empty)
                + "</body></html>";
        }

        private static string BuildFallbackParagraphs(string content)
        {
            var builder = new StringBuilder();
            foreach (var paragraph in (content ?? string.Empty).Split(
                new[] { "\r\n\r\n", "\n\n" },
                StringSplitOptions.RemoveEmptyEntries))
            {
                builder.Append("<p>");
                builder.Append(EscapeHtml(paragraph).Replace("\r\n", "<br>")
                    .Replace("\n", "<br>"));
                builder.Append("</p>");
            }
            return builder.ToString();
        }

        private static string EscapeHtml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string FormatDictionaryResult(TranslationResult result)
        {
            var builder = new StringBuilder();
            builder.Append(result.Heading);
            if (!string.IsNullOrWhiteSpace(result.Subheading))
            {
                builder.Append("  ");
                builder.Append(result.Subheading);
            }
            if (!string.IsNullOrWhiteSpace(result.Translation))
            {
                builder.AppendLine();
                builder.Append(result.Translation);
            }
            var itemCount = 0;
            foreach (var section in result.Sections)
            {
                foreach (var item in section.Items)
                {
                    var detail = JoinNonEmpty(" ", item.Heading, item.Body);
                    if (string.IsNullOrWhiteSpace(detail)
                        || builder.ToString().IndexOf(detail, StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }
                    builder.AppendLine();
                    builder.Append(detail);
                    itemCount++;
                    if (itemCount >= 2)
                    {
                        return builder.ToString();
                    }
                }
            }
            return builder.ToString();
        }

        private static string JoinNonEmpty(
            string separator,
            params string[] values)
        {
            return string.Join(
                separator,
                values.Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToArray());
        }

        private void AnalyzeSentenceClicked(object sender, EventArgs e)
        {
            var sentence = GetSelectedBrowserText();
            if (string.IsNullOrWhiteSpace(sentence))
            {
                MessageBox.Show(
                    "请先在正文中选中一个英文句子。",
                    "翻译选中句",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            if (sentence.Length > 800)
            {
                MessageBox.Show(
                    "当前选择过长。请只选中一个需要解析的句子。",
                    "翻译选中句",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var requested = SentenceAnalysisRequested;
            if (requested != null)
            {
                requested(
                    this,
                    new SentenceAnalysisRequestedEventArgs(
                        sentence,
                        PointToScreen(new Point(
                            Math.Max(0, _articleBrowser.Width / 2),
                            Math.Max(0, _articleBrowser.Height / 2)))));
            }
        }

        private string GetSelectedBrowserText()
        {
            if (_articleBrowser.Document == null)
            {
                return string.Empty;
            }
            try
            {
                var value = _articleBrowser.Document.InvokeScript("getSelectedText");
                return value == null ? string.Empty : Convert.ToString(value).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private void OpenSource(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_currentArticle == null || string.IsNullOrWhiteSpace(_currentArticle.Url))
            {
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _currentArticle.Url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "无法打开来源链接：\r\n" + ex.Message,
                    "离线阅读库",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void OpenInBrowserClicked(object sender, EventArgs e)
        {
            if (_currentArticle == null)
            {
                return;
            }
            try
            {
                var path = OfflineArticleHtmlExporter.Save(_currentArticle);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "无法保存或打开 HTML 文件：\r\n" + ex.Message,
                    "离线阅读库",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

    }
}
