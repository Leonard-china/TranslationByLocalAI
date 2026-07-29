using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace TranslationByLocalAI
{
    internal sealed class AppConfig
    {
        public int ConfigVersion { get; set; }
        public bool Enabled { get; set; }
        public bool DesktopWidgetEnabled { get; set; }
        public int DesktopWidgetX { get; set; }
        public int DesktopWidgetY { get; set; }
        public string ApiBaseUrl { get; set; }
        public bool AutoStartServer { get; set; }
        public bool StopOwnedServerOnExit { get; set; }
        public string ServerExecutable { get; set; }
        public string ModelFile { get; set; }
        public int ContextSize { get; set; }
        public int ButtonTimeoutSeconds { get; set; }
        public string TargetForChinese { get; set; }
        public string TargetForForeign { get; set; }
        public bool DetailedEnglishEnabled { get; set; }
        public string DetailedTranslationProvider { get; set; }
        public string DeepSeekApiBaseUrl { get; set; }
        public string DeepSeekApiKeyProtected { get; set; }

        public static string ConfigDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TranslationByLocalAI");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDirectory, "settings.json"); }
        }

        public static AppConfig CreateDefault()
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var serverExecutable = Path.Combine(appDirectory, "llama-server.exe");
            var modelFile = Path.Combine(
                appDirectory,
                "Models",
                "MiniCPM5-1B-F16-00001-of-00002.gguf");

            return new AppConfig
            {
                ConfigVersion = 6,
                Enabled = true,
                DesktopWidgetEnabled = true,
                DesktopWidgetX = -1,
                DesktopWidgetY = -1,
                ApiBaseUrl = "http://127.0.0.1:8080",
                AutoStartServer = true,
                StopOwnedServerOnExit = true,
                ServerExecutable = serverExecutable,
                ModelFile = modelFile,
                ContextSize = 8192,
                ButtonTimeoutSeconds = 8,
                TargetForChinese = "English",
                TargetForForeign = "简体中文",
                DetailedEnglishEnabled = false,
                DetailedTranslationProvider = "Hybrid",
                DeepSeekApiBaseUrl = "https://api.deepseek.com"
            };
        }

        public static AppConfig Load()
        {
            var defaults = CreateDefault();
            if (!File.Exists(ConfigPath))
            {
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = new JavaScriptSerializer().Deserialize<AppConfig>(json);
                if (loaded == null)
                {
                    return defaults;
                }

                if (loaded.ConfigVersion < 2)
                {
                    loaded.DesktopWidgetEnabled = true;
                    loaded.DesktopWidgetX = -1;
                    loaded.DesktopWidgetY = -1;
                }
                if (loaded.ConfigVersion < 3)
                {
                    loaded.DetailedEnglishEnabled = false;
                }
                if (loaded.ConfigVersion < 4)
                {
                    loaded.DetailedTranslationProvider = defaults.DetailedTranslationProvider;
                    loaded.DeepSeekApiBaseUrl = defaults.DeepSeekApiBaseUrl;
                }
                if (loaded.ConfigVersion < 5
                    && string.Equals(
                        loaded.DetailedTranslationProvider,
                        "DeepSeekFlash",
                        StringComparison.OrdinalIgnoreCase))
                {
                    loaded.DetailedTranslationProvider = "DeepSeekPro";
                }
                if (loaded.ConfigVersion < 6
                    && string.Equals(
                        loaded.DetailedTranslationProvider,
                        "DeepSeekPro",
                        StringComparison.OrdinalIgnoreCase))
                {
                    loaded.DetailedTranslationProvider = "Hybrid";
                }
                loaded.ConfigVersion = 6;
                loaded.ApiBaseUrl = ValueOrDefault(loaded.ApiBaseUrl, defaults.ApiBaseUrl);
                loaded.ServerExecutable = ValueOrDefault(loaded.ServerExecutable, defaults.ServerExecutable);
                loaded.ModelFile = ValueOrDefault(loaded.ModelFile, defaults.ModelFile);
                loaded.TargetForChinese = ValueOrDefault(loaded.TargetForChinese, defaults.TargetForChinese);
                loaded.TargetForForeign = ValueOrDefault(loaded.TargetForForeign, defaults.TargetForForeign);
                loaded.DetailedTranslationProvider = ValueOrDefault(
                    loaded.DetailedTranslationProvider,
                    defaults.DetailedTranslationProvider);
                loaded.DeepSeekApiBaseUrl = ValueOrDefault(
                    loaded.DeepSeekApiBaseUrl,
                    defaults.DeepSeekApiBaseUrl);
                if (loaded.ContextSize < 512)
                {
                    loaded.ContextSize = defaults.ContextSize;
                }
                if (loaded.ButtonTimeoutSeconds < 2)
                {
                    loaded.ButtonTimeoutSeconds = defaults.ButtonTimeoutSeconds;
                }
                return loaded;
            }
            catch
            {
                return defaults;
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(ConfigDirectory);
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(ConfigPath, serializer.Serialize(this));
        }

        public string GetDeepSeekApiKey()
        {
            if (string.IsNullOrWhiteSpace(DeepSeekApiKeyProtected))
            {
                return string.Empty;
            }

            try
            {
                var encrypted = Convert.FromBase64String(DeepSeekApiKeyProtected);
                var clearBytes = ProtectedData.Unprotect(
                    encrypted,
                    GetKeyEntropy(),
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clearBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        public void SetDeepSeekApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                DeepSeekApiKeyProtected = null;
                return;
            }

            var clearBytes = Encoding.UTF8.GetBytes(apiKey.Trim());
            var encrypted = ProtectedData.Protect(
                clearBytes,
                GetKeyEntropy(),
                DataProtectionScope.CurrentUser);
            DeepSeekApiKeyProtected = Convert.ToBase64String(encrypted);
        }

        private static string ValueOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static byte[] GetKeyEntropy()
        {
            return Encoding.UTF8.GetBytes("TranslationByLocalAI.DeepSeek.v1");
        }
    }
}
