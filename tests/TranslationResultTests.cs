using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TranslationByLocalAI;

namespace TranslationByLocalAITests
{
    internal static class TranslationResultTests
    {
        private static int _failures;

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2 && string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                return RenderScreenshot(args[1]);
            }
            if (args.Length == 2
                && string.Equals(args[0], "--settings-screenshot", StringComparison.OrdinalIgnoreCase))
            {
                return RenderSettingsScreenshot(args[1]);
            }
            if (args.Length == 2 && string.Equals(args[0], "--live", StringComparison.OrdinalIgnoreCase))
            {
                return RunLiveSmokeTest(args[1], "Local");
            }
            if (args.Length == 2
                && string.Equals(args[0], "--live-deepseek", StringComparison.OrdinalIgnoreCase))
            {
                return RunLiveSmokeTest(args[1], null);
            }
            if (args.Length == 2
                && string.Equals(args[0], "--live-deepseek-pro", StringComparison.OrdinalIgnoreCase))
            {
                return RunLiveSmokeTest(args[1], "DeepSeekPro");
            }
            if (args.Length == 2
                && string.Equals(args[0], "--live-hybrid", StringComparison.OrdinalIgnoreCase))
            {
                return RunLiveSmokeTest(args[1], "Hybrid");
            }

            AssertKind("was", TranslationContentKind.Word);
            AssertKind("look after", TranslationContentKind.Auto);
            AssertKind("I love you", TranslationContentKind.Auto);
            AssertKind("She has been studying English.", TranslationContentKind.Sentence);
            AssertKind("Mr. Smith is here.", TranslationContentKind.Sentence);
            AssertKind("The U.S. team won.", TranslationContentKind.Sentence);
            AssertKind("Hello. How are you?", TranslationContentKind.PlainText);
            AssertKind("Hello\nHow are you", TranslationContentKind.PlainText);
            AssertKind(
                "This is the first sentence.\r\n\r\nThis is the second sentence.",
                TranslationContentKind.PlainText);

            Assert(
                EnglishInputClassifier.ShouldUseDetailedMode(
                    true,
                    "was",
                    "Simplified Chinese"),
                "English to Simplified Chinese should use detailed mode.");
            Assert(
                !EnglishInputClassifier.ShouldUseDetailedMode(
                    true,
                    "was",
                    "English"),
                "English target should not use detailed mode.");
            Assert(
                !EnglishInputClassifier.ShouldUseDetailedMode(
                    false,
                    "was",
                    "Simplified Chinese"),
                "Disabled setting should not use detailed mode.");

            AssertWordParsing();
            AssertMinimalWordParsing();
            AssertSentenceParsing();
            AssertInvalidParsing();
            AssertApiKeyProtection();
            AssertOfflineDictionary();

            Console.WriteLine(
                _failures == 0
                    ? "All translation result tests passed."
                    : _failures + " translation result test(s) failed.");
            return _failures == 0 ? 0 : 1;
        }

        private static int RenderScreenshot(string outputPath)
        {
            const string json =
                "{\"type\":\"word\",\"headword\":\"was\",\"phonetic\":\"/wɒz; wəz/\","
                + "\"pronunciation\":\"重读近似“沃兹”，弱读 /wəz/\","
                + "\"translation\":\"是；在；处于（be 的过去式）\","
                + "\"forms\":[{\"base\":\"be\",\"relation\":\"第一、第三人称单数过去式\"}],"
                + "\"meanings\":[{\"part_of_speech\":\"v. 动词\","
                + "\"definitions\":[\"表示过去的身份、状态或特征\",\"构成过去进行时或被动语态\"],"
                + "\"examples\":[{\"en\":\"She was very happy yesterday.\","
                + "\"zh\":\"她昨天非常开心。\"},{\"en\":\"The room was cleaned in the morning.\","
                + "\"zh\":\"房间是在早上被打扫的。\"}]}],"
                + "\"phrases\":[{\"phrase\":\"was about to do\",\"meaning\":\"正要做某事\","
                + "\"example_en\":\"I was about to call you.\",\"example_zh\":\"我正要给你打电话。\"}],"
                + "\"synonyms\":[],\"antonyms\":[],"
                + "\"confusables\":[\"were：be 的复数及第二人称过去式\"]}";

            TranslationResult result;
            if (!DetailedTranslationParser.TryParse(json, out result))
            {
                Console.Error.WriteLine("Unable to build screenshot data.");
                return 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var config = AppConfig.CreateDefault();
            config.AutoStartServer = false;
            config.DetailedEnglishEnabled = true;

            using (var client = new TranslationClient(config))
            using (var icon = UiTheme.CreateAppIcon())
            using (var form = new TranslationForm(config, client, icon))
            {
                form.TopMost = false;
                form.Opacity = 0;
                form.Size = new Size(620, 650);
                form.Show();

                SetPrivateFieldText(form, "_sourceBox", "was");
                SetPrivateFieldText(form, "_directionLabel", "英语 →");
                SetPrivateFieldText(form, "_statusLabel", "完成 · DeepSeek V4 Pro");
                var targetBox = GetPrivateField<ComboBox>(form, "_targetBox");
                targetBox.SelectedIndex = 0;

                var renderMethod = typeof(TranslationForm).GetMethod(
                    "RenderResult",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                renderMethod.Invoke(form, new object[] { result });
                form.PerformLayout();
                Application.DoEvents();

                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(outputPath, ImageFormat.Png);
                }
                form.ClosePermanently();
            }
            Console.WriteLine("Screenshot saved: " + outputPath);
            return 0;
        }

        private static int RunLiveSmokeTest(string sourceText, string providerOverride)
        {
            var config = AppConfig.Load();
            config.DetailedEnglishEnabled = true;
            if (!string.IsNullOrWhiteSpace(providerOverride))
            {
                config.DetailedTranslationProvider = providerOverride;
            }
            var stopwatch = Stopwatch.StartNew();
            using (var client = new TranslationClient(config))
            {
                try
                {
                    double previewSeconds = -1;
                    var partialProgress = new ImmediateProgress<TranslationResult>(
                        delegate(TranslationResult value)
                        {
                            if (previewSeconds < 0)
                            {
                                previewSeconds = stopwatch.Elapsed.TotalSeconds;
                            }
                        });
                    var result = client.TranslateAsync(
                        sourceText,
                        "Simplified Chinese",
                        null,
                        partialProgress,
                        CancellationToken.None).GetAwaiter().GetResult();
                    stopwatch.Stop();
                    Console.WriteLine(
                        "Live result: kind="
                        + result.Kind
                        + ", structured="
                        + result.IsStructured
                        + ", parseFailed="
                        + result.StructuredParseFailed
                        + ", elapsed="
                        + stopwatch.Elapsed.TotalSeconds.ToString("0.00")
                        + "s, firstPreview="
                        + (previewSeconds < 0 ? "none" : previewSeconds.ToString("0.000") + "s"));
                    Console.WriteLine(result.ToClipboardText());
                    if (result.StructuredParseFailed)
                    {
                        TranslationResult reparsed;
                        Console.WriteLine(
                            "Raw reparse succeeded="
                            + DetailedTranslationParser.TryParse(
                                result.Translation,
                                out reparsed));
                        Console.WriteLine(
                            "Raw UTF-8 base64="
                            + Convert.ToBase64String(
                                Encoding.UTF8.GetBytes(result.Translation)));
                    }
                    var cacheStopwatch = Stopwatch.StartNew();
                    client.TranslateAsync(
                        sourceText,
                        "Simplified Chinese",
                        null,
                        CancellationToken.None).GetAwaiter().GetResult();
                    cacheStopwatch.Stop();
                    Console.WriteLine(
                        "Same-session cache elapsed="
                        + cacheStopwatch.Elapsed.TotalMilliseconds.ToString("0.0")
                        + "ms");
                    return string.IsNullOrWhiteSpace(result.ToClipboardText()) ? 1 : 0;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Console.Error.WriteLine(
                        "Live smoke test failed after "
                        + stopwatch.Elapsed.TotalSeconds.ToString("0.00")
                        + "s: "
                        + ex.Message);
                    return 1;
                }
            }
        }

        private static void SetPrivateFieldText(
            TranslationForm form,
            string fieldName,
            string text)
        {
            var control = GetPrivateField<Control>(form, fieldName);
            control.Text = text;
        }

        private static T GetPrivateField<T>(TranslationForm form, string fieldName)
            where T : class
        {
            var field = typeof(TranslationForm).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field.GetValue(form) as T;
        }

        private static int RenderSettingsScreenshot(string outputPath)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var config = AppConfig.CreateDefault();
            config.AutoStartServer = false;
            config.DetailedEnglishEnabled = true;

            using (var icon = UiTheme.CreateAppIcon())
            using (var form = new SettingsForm(config, icon))
            {
                form.Opacity = 0;
                form.Show();
                form.PerformLayout();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(outputPath, ImageFormat.Png);
                }
                form.Close();
            }
            Console.WriteLine("Settings screenshot saved: " + outputPath);
            return 0;
        }

        private static void AssertWordParsing()
        {
            const string json =
                "```json\n{\"type\":\"word\",\"headword\":\"was\","
                + "\"phonetic\":\"/wɒz/\",\"pronunciation\":\"弱读 /wəz/\","
                + "\"translation\":\"是；在\",\"forms\":[{\"base\":\"be\","
                + "\"relation\":\"第一、第三人称单数过去式\"}],"
                + "\"meanings\":[{\"part_of_speech\":\"v.\","
                + "\"definitions\":[\"是；处于\"],\"examples\":[{\"en\":\"I was tired.\","
                + "\"zh\":\"我当时很累。\"}]}],\"phrases\":[{\"phrase\":\"was about to\","
                + "\"meaning\":\"正要\",\"example_en\":\"I was about to leave.\","
                + "\"example_zh\":\"我正要离开。\"}],\"synonyms\":[],\"antonyms\":[],"
                + "\"confusables\":[]}\n```";

            TranslationResult result;
            Assert(
                DetailedTranslationParser.TryParse(json, out result),
                "Word JSON should parse.");
            Assert(result != null && result.Kind == TranslationContentKind.Word, "Word kind should be preserved.");
            Assert(result != null && result.Sections.Count >= 3, "Word cards should include forms, meanings, and phrases.");
            Assert(
                result != null && result.ToClipboardText().Contains("第一、第三人称单数过去式"),
                "Clipboard text should contain inflection details.");
        }

        private static void AssertSentenceParsing()
        {
            const string json =
                "{\"type\":\"sentence\",\"translation\":\"她一直在学习英语。\","
                + "\"sentence_core\":\"She + has been studying + English\","
                + "\"tense_voice\":\"现在完成进行时，主动语态\","
                + "\"clauses\":[\"简单句\"],\"grammar_points\":[\"强调动作持续\"],"
                + "\"key_phrases\":[],\"translation_note\":\"突出持续性\"}";

            TranslationResult result;
            Assert(
                DetailedTranslationParser.TryParse(json, out result),
                "Sentence JSON should parse.");
            Assert(
                result != null && result.Kind == TranslationContentKind.Sentence,
                "Sentence kind should be preserved.");
            Assert(
                result != null && result.Sections.Count >= 4,
                "Sentence cards should include grammar sections.");
        }

        private static void AssertMinimalWordParsing()
        {
            const string json =
                "{\"type\":\"word\",\"headword\":\"was\",\"phonetic\":\"/wɔs/\","
                + "\"pronunciation\":\"wa-s\",\"translation\":\"曾，曾经，是\","
                + "\"forms\":[],\"meanings\":[{\"part_of_speech\":\"verb\","
                + "\"definitions\":[\"曾，曾经\",\"是\"]},{\"part_of_speech\":\"modal verb\","
                + "\"definitions\":[\"是\"]},{\"part_of_speech\":\"auxiliary verb\","
                + "\"definitions\":[\"是\"]}],\"phrases\":[],\"synonyms\":[],\"antonyms\":[]}";

            TranslationResult result;
            Assert(
                DetailedTranslationParser.TryParse(json, out result),
                "A valid compact word response with omitted optional fields should parse.");
            Assert(
                DetailedTranslationParser.TryParse(json + "}", out result),
                "A complete first JSON object should survive an extra closing brace from a small model.");
        }

        private static void AssertInvalidParsing()
        {
            TranslationResult result;
            Assert(
                !DetailedTranslationParser.TryParse("普通模型输出，不是 JSON。", out result),
                "Non-JSON output should trigger the raw-output fallback.");
        }

        private static void AssertApiKeyProtection()
        {
            const string sampleKey = "test-only-key-not-a-real-secret";
            var config = AppConfig.CreateDefault();
            config.SetDeepSeekApiKey(sampleKey);
            Assert(
                !string.IsNullOrWhiteSpace(config.DeepSeekApiKeyProtected)
                    && !config.DeepSeekApiKeyProtected.Contains(sampleKey),
                "The persisted DeepSeek key must be encrypted.");
            Assert(
                string.Equals(config.GetDeepSeekApiKey(), sampleKey, StringComparison.Ordinal),
                "The current Windows user should be able to decrypt the saved key.");
            config.SetDeepSeekApiKey(string.Empty);
            Assert(
                string.IsNullOrWhiteSpace(config.DeepSeekApiKeyProtected),
                "Clearing the key should remove the protected value.");
        }

        private static void AssertOfflineDictionary()
        {
            var was = OfflineEnglishDictionary.CreatePreview("was");
            Assert(was != null, "The offline dictionary should contain 'was'.");
            Assert(
                was != null && was.IsPartial && was.Subheading.Contains("wɒz"),
                "The offline 'was' entry should provide its phonetic form.");
            Assert(
                was != null && was.ToClipboardText().Contains("be")
                    && was.ToClipboardText().Contains("过去式"),
                "The offline 'was' entry should identify be and the past form.");
            Assert(
                was != null && was.Translation.Contains("是"),
                "The offline 'was' summary should use the base word meaning.");

            var localAiResult = new TranslationResult
            {
                Kind = TranslationContentKind.Word,
                Heading = "was",
                Translation = "过去式（过去时）",
                IsStructured = true
            };
            var mergedLocal = OfflineEnglishDictionary.Merge(
                OfflineEnglishDictionary.CreatePreview("was"),
                localAiResult,
                true);
            Assert(
                mergedLocal.Translation.Contains("是"),
                "Local detailed mode should prefer the dictionary's core meaning.");

            const string remoteSummary = "是（be 的过去式）";
            var remoteAiResult = new TranslationResult
            {
                Kind = TranslationContentKind.Word,
                Heading = "was",
                Translation = remoteSummary,
                IsStructured = true
            };
            var mergedRemote = OfflineEnglishDictionary.Merge(
                OfflineEnglishDictionary.CreatePreview("was"),
                remoteAiResult,
                false);
            Assert(
                string.Equals(
                    mergedRemote.Translation,
                    remoteSummary,
                    StringComparison.Ordinal),
                "Hybrid mode should preserve the remote model's contextual summary.");

            var better = OfflineEnglishDictionary.CreatePreview("better");
            Assert(
                better != null && better.ToClipboardText().Contains("good")
                    && better.ToClipboardText().Contains("比较级"),
                "The offline 'better' entry should identify its comparative lemma.");

            Assert(
                OfflineEnglishDictionary.CreatePreview("not-a-real-ecdict-word-xyz") == null,
                "Unknown words should fall back to AI without a dictionary card.");
        }

        private static void AssertKind(string text, TranslationContentKind expected)
        {
            var actual = EnglishInputClassifier.Classify(text);
            Assert(
                actual == expected,
                "Expected " + expected + " for \"" + text + "\", got " + actual + ".");
        }

        private static void Assert(bool condition, string message)
        {
            if (condition)
            {
                return;
            }
            _failures++;
            Console.Error.WriteLine("FAIL: " + message);
        }

        private sealed class ImmediateProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            internal ImmediateProgress(Action<T> report)
            {
                _report = report;
            }

            public void Report(T value)
            {
                _report(value);
            }
        }
    }
}
