using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TranslationByLocalAI
{
    internal static class OfflineArticleHtmlExporter
    {
        private static readonly UTF8Encoding Utf8WithoutBom =
            new UTF8Encoding(false);

        internal static string ExportDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "TranslationByLocalAI",
                    "Articles");
            }
        }

        internal static string Save(OfflineArticle article)
        {
            return Save(article, ExportDirectory);
        }

        internal static string Save(OfflineArticle article, string directory)
        {
            if (article == null)
            {
                throw new ArgumentNullException("article");
            }
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("保存目录不能为空。", "directory");
            }

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, CreateFileName(article));
            File.WriteAllText(path, BuildDocument(article), Utf8WithoutBom);
            return path;
        }

        internal static string CreateFileName(OfflineArticle article)
        {
            if (article == null)
            {
                throw new ArgumentNullException("article");
            }

            var title = SanitizeFileName(article.Title, 92);
            var id = SanitizeFileName(article.Id, 48);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "offline-article";
            }
            if (!string.IsNullOrWhiteSpace(id)
                && !string.Equals(title, id, StringComparison.OrdinalIgnoreCase))
            {
                title += "-" + id;
            }
            if (IsReservedWindowsFileName(title))
            {
                title = "article-" + title;
            }
            return title + ".html";
        }

        internal static string BuildDocument(OfflineArticle article)
        {
            if (article == null)
            {
                throw new ArgumentNullException("article");
            }

            var body = article.Html;
            if (string.IsNullOrWhiteSpace(body))
            {
                body = BuildFallbackParagraphs(article.Content);
            }

            var metadata = JoinNonEmpty(
                " · ",
                article.Source,
                article.Section,
                article.Topic,
                article.Difficulty,
                article.LengthBand,
                article.WordCount > 0 ? article.WordCount + " 词" : null,
                article.PublishedDate);
            var sourceLink = BuildLink(article.Url, "查看原始来源");
            var licenseLink = BuildLink(article.LicenseUrl, article.License);

            var builder = new StringBuilder();
            builder.Append("<!doctype html>\r\n<html lang=\"en\">\r\n<head>\r\n");
            builder.Append("  <meta charset=\"utf-8\">\r\n");
            builder.Append(
                "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n");
            builder.Append("  <meta name=\"referrer\" content=\"no-referrer\">\r\n");
            builder.Append("  <title>");
            builder.Append(EscapeHtml(article.Title));
            builder.Append("</title>\r\n");
            builder.Append("  <style>\r\n");
            builder.Append(
                "    :root{color-scheme:light;font-family:Georgia,'Times New Roman',serif;}\r\n");
            builder.Append(
                "    *{box-sizing:border-box}html,body{margin:0;background:#f6f8fc;color:#19202e;}\r\n");
            builder.Append(
                "    body{padding:32px 20px 64px;}article{max-width:980px;margin:0 auto;background:#fff;");
            builder.Append(
                "border:1px solid #dae0ea;border-radius:16px;padding:clamp(24px,5vw,56px);");
            builder.Append("box-shadow:0 16px 42px rgba(30,41,59,.08);}\r\n");
            builder.Append(
                "    h1,h2,h3{font-family:'Segoe UI','Microsoft YaHei UI',sans-serif;");
            builder.Append("line-height:1.35;color:#17243d;}h1{font-size:clamp(2rem,5vw,3rem);");
            builder.Append("margin:0 0 .45em;}h2{font-size:1.45em;margin:1.8em 0 .75em;}");
            builder.Append("h3{font-size:1.2em;margin:1.6em 0 .7em;}\r\n");
            builder.Append(
                "    .meta,.footer{font-family:'Segoe UI','Microsoft YaHei UI',sans-serif;");
            builder.Append("font-size:.92rem;line-height:1.65;color:#64748b;}\r\n");
            builder.Append(
                "    .content{font-size:clamp(1.1rem,2.2vw,1.32rem);line-height:1.82;");
            builder.Append("letter-spacing:.01em;margin-top:2.4em;}.content p{margin:0 0 1.35em;}");
            builder.Append(".list-item{padding-left:1.25em;position:relative;}");
            builder.Append(".list-item:before{content:'•';position:absolute;left:.2em;color:#2563eb;}");
            builder.Append(".word{border-radius:4px;padding:1px 0;}\r\n");
            builder.Append(
                "    .footer{border-top:1px solid #e2e8f0;margin-top:3em;padding-top:1.25em;}");
            builder.Append("a{color:#1d4ed8;text-decoration:none}a:hover{text-decoration:underline;}");
            builder.Append("::selection{background:#cfe0ff;color:#111827;}\r\n");
            builder.Append(
                "    @media(max-width:620px){body{padding:0;}article{border:0;border-radius:0;");
            builder.Append("padding:24px 20px;box-shadow:none;}}\r\n");
            builder.Append("  </style>\r\n</head>\r\n<body>\r\n<article>\r\n<header>\r\n");
            builder.Append("  <h1>");
            builder.Append(EscapeHtml(article.Title));
            builder.Append("</h1>\r\n");
            if (!string.IsNullOrWhiteSpace(metadata))
            {
                builder.Append("  <div class=\"meta\">");
                builder.Append(EscapeHtml(metadata));
                builder.Append("</div>\r\n");
            }
            if (!string.IsNullOrWhiteSpace(article.Author))
            {
                builder.Append("  <div class=\"meta\">作者：");
                builder.Append(EscapeHtml(article.Author));
                builder.Append("</div>\r\n");
            }
            builder.Append("</header>\r\n<main class=\"content\">\r\n");
            builder.Append(body ?? string.Empty);
            builder.Append("\r\n</main>\r\n<footer class=\"footer\">\r\n");
            if (!string.IsNullOrWhiteSpace(sourceLink))
            {
                builder.Append("  <div>来源：");
                builder.Append(sourceLink);
                builder.Append("</div>\r\n");
            }
            if (!string.IsNullOrWhiteSpace(article.License))
            {
                builder.Append("  <div>许可：");
                builder.Append(
                    string.IsNullOrWhiteSpace(licenseLink)
                        ? EscapeHtml(article.License)
                        : licenseLink);
                builder.Append("</div>\r\n");
            }
            builder.Append(
                "  <div>由 TranslationByLocalAI 离线英语阅读库导出。</div>\r\n");
            builder.Append("</footer>\r\n</article>\r\n</body>\r\n</html>\r\n");
            return builder.ToString();
        }

        private static string BuildFallbackParagraphs(string content)
        {
            var builder = new StringBuilder();
            foreach (var paragraph in (content ?? string.Empty).Split(
                new[] { "\r\n\r\n", "\n\n" },
                StringSplitOptions.RemoveEmptyEntries))
            {
                builder.Append("<p>");
                builder.Append(
                    EscapeHtml(paragraph)
                        .Replace("\r\n", "<br>")
                        .Replace("\n", "<br>"));
                builder.Append("</p>");
            }
            return builder.ToString();
        }

        private static string BuildLink(string value, string label)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(value)
                || string.IsNullOrWhiteSpace(label)
                || !Uri.TryCreate(value, UriKind.Absolute, out uri)
                || (uri.Scheme != Uri.UriSchemeHttp
                    && uri.Scheme != Uri.UriSchemeHttps))
            {
                return string.Empty;
            }
            return "<a href=\"" + EscapeHtml(uri.AbsoluteUri)
                + "\" target=\"_blank\" rel=\"noopener noreferrer\">"
                + EscapeHtml(label) + "</a>";
        }

        private static string SanitizeFileName(string value, int maximumLength)
        {
            var invalidCharacters = new HashSet<char>(
                Path.GetInvalidFileNameChars());
            var builder = new StringBuilder();
            foreach (var character in (value ?? string.Empty).Trim())
            {
                if (invalidCharacters.Contains(character)
                    || char.IsControl(character))
                {
                    builder.Append('-');
                }
                else
                {
                    builder.Append(character);
                }
                if (builder.Length >= maximumLength)
                {
                    break;
                }
            }

            var sanitized = builder.ToString().Trim(' ', '.', '-');
            while (sanitized.Contains("--"))
            {
                sanitized = sanitized.Replace("--", "-");
            }
            return sanitized;
        }

        private static bool IsReservedWindowsFileName(string value)
        {
            var name = (value ?? string.Empty).Split('.')[0];
            if (string.Equals(name, "CON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "PRN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "AUX", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "NUL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (name.Length == 4
                && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
            {
                var suffix = name[3];
                return suffix >= '1' && suffix <= '9';
            }
            return false;
        }

        private static string EscapeHtml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
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
    }
}
