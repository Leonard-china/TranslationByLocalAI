using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace TranslationByLocalAI
{
    internal sealed class OfflineArticle
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Source { get; set; }
        public string Section { get; set; }
        public string Topic { get; set; }
        public string Difficulty { get; set; }
        public string LengthBand { get; set; }
        public int WordCount { get; set; }
        public string PublishedDate { get; set; }
        public string Author { get; set; }
        public string Url { get; set; }
        public string License { get; set; }
        public string LicenseUrl { get; set; }
        public string Content { get; set; }
        public string Html { get; set; }
    }

    internal sealed class OfflineArticleLibrary
    {
        public int SchemaVersion { get; set; }
        public string GeneratedDate { get; set; }
        public int ArticleCount { get; set; }
        public List<OfflineArticle> Articles { get; set; }
    }

    internal static class OfflineArticleRepository
    {
        private static readonly object Sync = new object();
        private static OfflineArticleLibrary _library;
        private static bool _loadAttempted;
        private static string _loadError;

        internal static IList<OfflineArticle> GetArticles()
        {
            EnsureLoaded();
            if (_library == null || _library.Articles == null)
            {
                return new List<OfflineArticle>();
            }
            return _library.Articles.AsReadOnly();
        }

        internal static string GetLoadError()
        {
            EnsureLoaded();
            return _loadError;
        }

        internal static void WarmUp()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    EnsureLoaded();
                }
                catch
                {
                }
            });
        }

        private static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (_loadAttempted)
                {
                    return;
                }
                _loadAttempted = true;

                var libraryPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Articles",
                    "offline-articles.json.gz");
                if (!File.Exists(libraryPath))
                {
                    _loadError = "找不到离线文章库：" + libraryPath;
                    AppLogger.Write(_loadError);
                    return;
                }

                try
                {
                    string json;
                    using (var file = File.OpenRead(libraryPath))
                    using (var compressed = new GZipStream(file, CompressionMode.Decompress))
                    using (var reader = new StreamReader(
                        compressed, Encoding.UTF8, true, 65536))
                    {
                        json = reader.ReadToEnd();
                    }

                    var serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    serializer.RecursionLimit = 32;
                    var loaded = serializer.Deserialize<OfflineArticleLibrary>(json);
                    if (loaded == null || loaded.Articles == null
                        || loaded.Articles.Count == 0)
                    {
                        throw new InvalidDataException("文章库没有可读取的文章。");
                    }
                    if (loaded.ArticleCount != loaded.Articles.Count)
                    {
                        throw new InvalidDataException("文章库数量校验失败。");
                    }
                    _library = loaded;
                    AppLogger.Write(
                        "Offline article library loaded, articles="
                        + loaded.Articles.Count
                        + ".");
                }
                catch (Exception ex)
                {
                    _loadError = "无法载入离线文章库：" + ex.Message;
                    AppLogger.Write(_loadError);
                    _library = null;
                }
            }
        }
    }
}
