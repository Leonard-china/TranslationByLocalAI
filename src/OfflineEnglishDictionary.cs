using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace TranslationByLocalAI
{
    internal sealed class OfflineDictionaryEntry
    {
        internal string Word { get; set; }
        internal string Phonetic { get; set; }
        internal string Translation { get; set; }
        internal string PartOfSpeech { get; set; }
        internal string Exchange { get; set; }
    }

    internal static class OfflineEnglishDictionary
    {
        private static readonly object Sync = new object();
        private static readonly Regex TranslationPartOfSpeech = new Regex(
            @"^(?<pos>[A-Za-z]+(?:\.[A-Za-z]+)?\.)\s*(?<meaning>.+)$",
            RegexOptions.Compiled);
        private static Dictionary<string, OfflineDictionaryEntry> _entries;
        private static bool _loadAttempted;

        internal static TranslationResult CreatePreview(string word)
        {
            var entry = Lookup(word);
            return entry == null ? null : BuildResult(entry);
        }

        internal static TranslationResult Merge(
            TranslationResult dictionaryResult,
            TranslationResult aiResult,
            bool preferDictionaryTranslation)
        {
            if (dictionaryResult == null)
            {
                return aiResult;
            }
            if (aiResult == null || !aiResult.IsStructured)
            {
                return dictionaryResult;
            }

            if (!string.IsNullOrWhiteSpace(dictionaryResult.Heading))
            {
                aiResult.Heading = dictionaryResult.Heading;
            }
            if (!string.IsNullOrWhiteSpace(dictionaryResult.Subheading))
            {
                aiResult.Subheading = dictionaryResult.Subheading;
            }
            if (!string.IsNullOrWhiteSpace(dictionaryResult.Translation)
                && (preferDictionaryTranslation
                    || string.IsNullOrWhiteSpace(aiResult.Translation)))
            {
                aiResult.Translation = dictionaryResult.Translation;
            }

            MergeFormSections(dictionaryResult, aiResult);
            for (var index = dictionaryResult.Sections.Count - 1; index >= 0; index--)
            {
                aiResult.Sections.Insert(0, dictionaryResult.Sections[index]);
            }
            aiResult.IsPartial = false;
            return aiResult;
        }

        private static OfflineDictionaryEntry Lookup(string word)
        {
            EnsureLoaded();
            if (_entries == null || string.IsNullOrWhiteSpace(word))
            {
                return null;
            }

            OfflineDictionaryEntry entry;
            return _entries.TryGetValue(word.Trim(), out entry) ? entry : null;
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

                var dictionaryPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Dictionaries",
                    "ecdict-learning.tsv.gz");
                if (!File.Exists(dictionaryPath))
                {
                    AppLogger.Write("Offline ECDICT subset was not found: " + dictionaryPath);
                    return;
                }

                var loaded = new Dictionary<string, OfflineDictionaryEntry>(
                    StringComparer.OrdinalIgnoreCase);
                try
                {
                    using (var file = File.OpenRead(dictionaryPath))
                    using (var compressed = new GZipStream(file, CompressionMode.Decompress))
                    using (var reader = new StreamReader(compressed, Encoding.UTF8, true, 65536))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0 || line[0] == '#')
                            {
                                continue;
                            }
                            var fields = line.Split('\t');
                            if (fields.Length < 5)
                            {
                                continue;
                            }

                            var entry = new OfflineDictionaryEntry
                            {
                                Word = Unescape(fields[0]),
                                Phonetic = Unescape(fields[1]),
                                Translation = Unescape(fields[2]),
                                PartOfSpeech = Unescape(fields[3]),
                                Exchange = Unescape(fields[4])
                            };
                            if (!string.IsNullOrWhiteSpace(entry.Word)
                                && !loaded.ContainsKey(entry.Word))
                            {
                                loaded.Add(entry.Word, entry);
                            }
                        }
                    }
                    _entries = loaded;
                    AppLogger.Write(
                        "Offline ECDICT subset loaded, entries=" + loaded.Count + ".");
                }
                catch (Exception ex)
                {
                    AppLogger.Write("Unable to load offline ECDICT subset: " + ex.Message);
                    _entries = null;
                }
            }
        }

        private static TranslationResult BuildResult(OfflineDictionaryEntry entry)
        {
            var meanings = ParseMeaningItems(entry.Translation);
            var baseMeanings = GetBaseMeaningItems(entry);
            var summaryMeanings = baseMeanings.Count > 0 ? baseMeanings : meanings;
            var result = new TranslationResult
            {
                Kind = TranslationContentKind.Word,
                Heading = entry.Word,
                Subheading = FormatPhonetic(entry.Phonetic),
                Translation = BuildSummary(summaryMeanings, entry.Translation),
                IsStructured = true,
                IsPartial = true
            };

            var forms = BuildFormsSection(entry);
            if (forms != null)
            {
                result.Sections.Add(forms);
            }
            if (meanings.Count > 0)
            {
                var section = new TranslationSection("本地词典 · 词性与释义");
                foreach (var item in meanings)
                {
                    section.Items.Add(item);
                }
                result.Sections.Add(section);
            }
            if (baseMeanings.Count > 0)
            {
                var baseWord = GetBaseWords(entry);
                var section = new TranslationSection(
                    "原形 " + (baseWord.Count > 0 ? baseWord[0] : string.Empty) + " · 词性与释义");
                foreach (var item in baseMeanings)
                {
                    section.Items.Add(item);
                }
                result.Sections.Add(section);
            }
            return result;
        }

        private static List<TranslationItem> GetBaseMeaningItems(OfflineDictionaryEntry entry)
        {
            foreach (var baseWord in GetBaseWords(entry))
            {
                OfflineDictionaryEntry baseEntry;
                if (!string.Equals(baseWord, entry.Word, StringComparison.OrdinalIgnoreCase)
                    && _entries != null
                    && _entries.TryGetValue(baseWord, out baseEntry))
                {
                    return ParseMeaningItems(baseEntry.Translation);
                }
            }
            return new List<TranslationItem>();
        }

        private static List<string> GetBaseWords(OfflineDictionaryEntry entry)
        {
            var values = ParseExchange(entry.Exchange);
            var bases = GetExchangeValues(values, "0");
            if (bases.Count > 0)
            {
                return bases;
            }

            var inferred = Regex.Match(
                entry.Translation ?? string.Empty,
                @"(?<base>[A-Za-z][A-Za-z' -]*?)的(?:过去式|过去分词|现在分词|"
                + "动名词|第三人称单数|比较级|最高级|复数)");
            var results = new List<string>();
            if (inferred.Success)
            {
                results.Add(inferred.Groups["base"].Value.Trim());
            }
            return results;
        }

        private static void MergeFormSections(
            TranslationResult dictionaryResult,
            TranslationResult aiResult)
        {
            TranslationSection dictionaryForms = null;
            foreach (var section in dictionaryResult.Sections)
            {
                if (section.Title.IndexOf("原形与变形", StringComparison.Ordinal) >= 0)
                {
                    dictionaryForms = section;
                    break;
                }
            }
            if (dictionaryForms == null)
            {
                return;
            }

            for (var index = aiResult.Sections.Count - 1; index >= 0; index--)
            {
                var aiSection = aiResult.Sections[index];
                if (!string.Equals(aiSection.Title, "原形与变形", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (var aiItem in aiSection.Items)
                {
                    var duplicate = false;
                    foreach (var dictionaryItem in dictionaryForms.Items)
                    {
                        if (!string.IsNullOrWhiteSpace(aiItem.Heading)
                            && string.Equals(
                                aiItem.Heading,
                                dictionaryItem.Heading,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (!duplicate)
                    {
                        dictionaryForms.Items.Add(aiItem);
                    }
                }
                aiResult.Sections.RemoveAt(index);
            }
        }

        private static List<TranslationItem> ParseMeaningItems(string translation)
        {
            var items = new List<TranslationItem>();
            foreach (var rawLine in SplitLines(translation))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var match = TranslationPartOfSpeech.Match(line);
                if (match.Success)
                {
                    items.Add(new TranslationItem(
                        FormatPartOfSpeech(match.Groups["pos"].Value),
                        match.Groups["meaning"].Value.Trim(),
                        null));
                }
                else
                {
                    items.Add(new TranslationItem(null, line, null));
                }
            }
            return items;
        }

        private static TranslationSection BuildFormsSection(OfflineDictionaryEntry entry)
        {
            var values = ParseExchange(entry.Exchange);
            var bases = GetExchangeValues(values, "0");
            var currentRelations = GetExchangeValues(values, "1");
            var section = new TranslationSection("本地词典 · 原形与变形");

            if (bases.Count > 0)
            {
                var relationText = BuildCurrentRelation(currentRelations);
                foreach (var baseWord in bases)
                {
                    section.Items.Add(new TranslationItem(
                        baseWord,
                        relationText ?? "当前词条由该原形变化而来",
                        null));
                }
            }
            else
            {
                AddDerivedForms(section, values, "p", "过去式");
                AddDerivedForms(section, values, "d", "过去分词");
                AddDerivedForms(section, values, "i", "现在分词／动名词");
                AddDerivedForms(section, values, "3", "第三人称单数");
                AddDerivedForms(section, values, "r", "比较级");
                AddDerivedForms(section, values, "t", "最高级");
                AddDerivedForms(section, values, "s", "名词复数");
            }

            if (section.Items.Count == 0)
            {
                var inferred = InferFormFromTranslation(entry.Translation);
                if (inferred != null)
                {
                    section.Items.Add(inferred);
                }
            }
            return section.Items.Count == 0 ? null : section;
        }

        private static Dictionary<string, List<string>> ParseExchange(string exchange)
        {
            var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(exchange))
            {
                return values;
            }

            foreach (var part in exchange.Split('/'))
            {
                var colon = part.IndexOf(':');
                if (colon <= 0 || colon >= part.Length - 1)
                {
                    continue;
                }
                var key = part.Substring(0, colon).Trim();
                var value = part.Substring(colon + 1).Trim();
                List<string> items;
                if (!values.TryGetValue(key, out items))
                {
                    items = new List<string>();
                    values.Add(key, items);
                }
                if (value.Length > 0 && !items.Contains(value))
                {
                    items.Add(value);
                }
            }
            return values;
        }

        private static List<string> GetExchangeValues(
            Dictionary<string, List<string>> values,
            string key)
        {
            List<string> found;
            return values.TryGetValue(key, out found)
                ? found
                : new List<string>();
        }

        private static void AddDerivedForms(
            TranslationSection section,
            Dictionary<string, List<string>> values,
            string code,
            string label)
        {
            foreach (var value in GetExchangeValues(values, code))
            {
                section.Items.Add(new TranslationItem(value, label, null));
            }
        }

        private static string BuildCurrentRelation(List<string> codes)
        {
            var labels = new List<string>();
            foreach (var code in codes)
            {
                string label;
                switch (code)
                {
                    case "p":
                        label = "过去式";
                        break;
                    case "d":
                        label = "过去分词";
                        break;
                    case "i":
                        label = "现在分词／动名词";
                        break;
                    case "3":
                        label = "第三人称单数";
                        break;
                    case "r":
                        label = "比较级";
                        break;
                    case "t":
                        label = "最高级";
                        break;
                    case "s":
                        label = "名词复数";
                        break;
                    default:
                        label = code;
                        break;
                }
                if (!labels.Contains(label))
                {
                    labels.Add(label);
                }
            }
            return labels.Count == 0
                ? null
                : "当前单词是其" + string.Join("／", labels.ToArray());
        }

        private static TranslationItem InferFormFromTranslation(string translation)
        {
            var match = Regex.Match(
                translation ?? string.Empty,
                @"(?<base>[A-Za-z][A-Za-z' -]*?)的(?<relation>过去式|过去分词|现在分词|"
                + "动名词|第三人称单数|比较级|最高级|复数)");
            if (!match.Success)
            {
                return null;
            }
            return new TranslationItem(
                match.Groups["base"].Value.Trim(),
                "当前单词是其" + match.Groups["relation"].Value,
                null);
        }

        private static string BuildSummary(
            List<TranslationItem> meanings,
            string fallback)
        {
            var values = new List<string>();
            foreach (var item in meanings)
            {
                if (string.IsNullOrWhiteSpace(item.Body))
                {
                    continue;
                }
                values.Add(item.Body);
                if (values.Count >= 4)
                {
                    break;
                }
            }
            return values.Count > 0
                ? string.Join("；", values.ToArray())
                : (fallback ?? string.Empty).Trim();
        }

        private static string[] SplitLines(string value)
        {
            return (value ?? string.Empty).Replace("\\n", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string FormatPhonetic(string phonetic)
        {
            var value = (phonetic ?? string.Empty).Trim().Trim('/', '[', ']');
            return value.Length == 0 ? null : "/" + value + "/";
        }

        private static string FormatPartOfSpeech(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "n.":
                    return "n. 名词";
                case "v.":
                    return "v. 动词";
                case "vt.":
                    return "vt. 及物动词";
                case "vi.":
                    return "vi. 不及物动词";
                case "a.":
                case "adj.":
                    return "adj. 形容词";
                case "ad.":
                case "adv.":
                    return "adv. 副词";
                case "prep.":
                    return "prep. 介词";
                case "conj.":
                    return "conj. 连词";
                case "pron.":
                    return "pron. 代词";
                case "num.":
                    return "num. 数词";
                case "art.":
                    return "art. 冠词";
                case "int.":
                    return "int. 感叹词";
                default:
                    return value;
            }
        }

        private static string Unescape(string value)
        {
            var builder = new StringBuilder(value.Length);
            var escaped = false;
            foreach (var character in value)
            {
                if (!escaped)
                {
                    if (character == '\\')
                    {
                        escaped = true;
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    continue;
                }

                switch (character)
                {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
                escaped = false;
            }
            if (escaped)
            {
                builder.Append('\\');
            }
            return builder.ToString();
        }
    }
}
