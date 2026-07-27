using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace TranslationByLocalAI
{
    internal enum TranslationContentKind
    {
        PlainText,
        Word,
        Phrase,
        Sentence,
        Auto
    }

    internal sealed class TranslationResult
    {
        internal TranslationResult()
        {
            Sections = new List<TranslationSection>();
        }

        internal TranslationContentKind Kind { get; set; }
        internal string Heading { get; set; }
        internal string Subheading { get; set; }
        internal string Translation { get; set; }
        internal string RawText { get; set; }
        internal bool IsStructured { get; set; }
        internal bool StructuredParseFailed { get; set; }
        internal bool RepeatedFormatFailure { get; set; }
        internal bool IsPartial { get; set; }
        internal List<TranslationSection> Sections { get; private set; }

        internal static TranslationResult CreatePlain(string translatedText)
        {
            return new TranslationResult
            {
                Kind = TranslationContentKind.PlainText,
                Translation = translatedText,
                IsStructured = false
            };
        }

        internal static TranslationResult CreateRaw(
            string modelOutput,
            bool parseFailed,
            bool repeatedFailure)
        {
            return new TranslationResult
            {
                Kind = TranslationContentKind.PlainText,
                Translation = modelOutput,
                RawText = modelOutput,
                IsStructured = false,
                StructuredParseFailed = parseFailed,
                RepeatedFormatFailure = repeatedFailure
            };
        }

        internal string ToClipboardText()
        {
            if (!IsStructured)
            {
                return Translation ?? RawText ?? string.Empty;
            }

            var builder = new StringBuilder();
            AppendBlock(builder, Heading);
            AppendBlock(builder, Subheading);
            if (!string.IsNullOrWhiteSpace(Translation))
            {
                AppendLabeledBlock(builder, "翻译", Translation);
            }

            foreach (var section in Sections)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }
                builder.AppendLine(section.Title);
                foreach (var item in section.Items)
                {
                    if (!string.IsNullOrWhiteSpace(item.Heading))
                    {
                        builder.AppendLine(item.Heading);
                    }
                    if (!string.IsNullOrWhiteSpace(item.Body))
                    {
                        builder.AppendLine(item.Body);
                    }
                    if (!string.IsNullOrWhiteSpace(item.Note))
                    {
                        builder.AppendLine(item.Note);
                    }
                }
            }
            return builder.ToString().Trim();
        }

        private static void AppendBlock(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }
            builder.AppendLine(value.Trim());
        }

        private static void AppendLabeledBlock(StringBuilder builder, string label, string value)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }
            builder.AppendLine(label);
            builder.AppendLine(value.Trim());
        }
    }

    internal sealed class TranslationSection
    {
        internal TranslationSection(string title)
        {
            Title = title;
            Items = new List<TranslationItem>();
        }

        internal string Title { get; private set; }
        internal List<TranslationItem> Items { get; private set; }
    }

    internal sealed class TranslationItem
    {
        internal TranslationItem(string heading, string body, string note)
        {
            Heading = heading;
            Body = body;
            Note = note;
        }

        internal string Heading { get; private set; }
        internal string Body { get; private set; }
        internal string Note { get; private set; }
    }

    internal static class EnglishInputClassifier
    {
        private static readonly Regex WordPattern = new Regex(
            @"^[A-Za-z]+(?:['’\-][A-Za-z]+)*$",
            RegexOptions.Compiled);

        private static readonly Regex EnglishWordPattern = new Regex(
            @"[A-Za-z]+(?:['’\-][A-Za-z]+)*",
            RegexOptions.Compiled);

        private static readonly HashSet<string> PeriodAbbreviations =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "st",
                "vs", "etc", "e.g", "i.e", "a.m", "p.m", "no", "fig"
            };

        internal static bool ShouldUseDetailedMode(
            bool detailedEnglishEnabled,
            string sourceText,
            string targetLanguage)
        {
            return detailedEnglishEnabled
                && IsSimplifiedChinese(targetLanguage)
                && LooksLikeEnglish(sourceText);
        }

        internal static TranslationContentKind Classify(string sourceText)
        {
            var text = (sourceText ?? string.Empty).Trim();
            if (WordPattern.IsMatch(text))
            {
                return TranslationContentKind.Word;
            }

            if (IsMultipleSentences(text))
            {
                return TranslationContentKind.PlainText;
            }

            var words = EnglishWordPattern.Matches(text).Count;
            if (words <= 0)
            {
                return TranslationContentKind.PlainText;
            }

            if (HasSentenceEnding(text) || words > 8)
            {
                return TranslationContentKind.Sentence;
            }

            // A short unpunctuated input may be a phrase ("look after") or a
            // sentence ("time flies"). Let the same translation request decide.
            return TranslationContentKind.Auto;
        }

        internal static bool LooksLikeEnglish(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText)
                || TranslationForm.ContainsChinese(sourceText))
            {
                return false;
            }

            var latinLetters = 0;
            var otherLetters = 0;
            foreach (var character in sourceText)
            {
                if (!char.IsLetter(character))
                {
                    continue;
                }
                if ((character >= 'A' && character <= 'Z')
                    || (character >= 'a' && character <= 'z'))
                {
                    latinLetters++;
                }
                else
                {
                    otherLetters++;
                }
            }
            return latinLetters > 0 && latinLetters >= otherLetters * 3;
        }

        private static bool IsSimplifiedChinese(string targetLanguage)
        {
            return string.Equals(targetLanguage, "Simplified Chinese", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetLanguage, "简体中文", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMultipleSentences(string text)
        {
            var nonEmptyLines = 0;
            foreach (var line in Regex.Split(text, @"\r?\n"))
            {
                if (EnglishWordPattern.IsMatch(line))
                {
                    nonEmptyLines++;
                    if (nonEmptyLines >= 2)
                    {
                        return true;
                    }
                }
            }

            var boundaries = 0;
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character != '.' && character != '!' && character != '?')
                {
                    continue;
                }

                if (character == '.' && IsNonBoundaryPeriod(text, index))
                {
                    continue;
                }

                while (index + 1 < text.Length
                    && (text[index + 1] == '.' || text[index + 1] == '!'
                        || text[index + 1] == '?'))
                {
                    index++;
                }
                boundaries++;
                if (boundaries >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSentenceEnding(string text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '!' || text[index] == '?')
                {
                    return true;
                }
                if (text[index] == '.' && !IsNonBoundaryPeriod(text, index))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsNonBoundaryPeriod(string text, int index)
        {
            if (index > 0 && index + 1 < text.Length
                && char.IsDigit(text[index - 1]) && char.IsDigit(text[index + 1]))
            {
                return true;
            }

            if (index + 1 < text.Length && char.IsLetter(text[index + 1]))
            {
                return true;
            }

            var start = index - 1;
            while (start >= 0 && (char.IsLetter(text[start]) || text[start] == '.'))
            {
                start--;
            }
            var token = text.Substring(start + 1, index - start - 1).Trim('.');
            if (PeriodAbbreviations.Contains(token))
            {
                return true;
            }

            return (token.Length == 1 && char.IsLetter(token[0]))
                || Regex.IsMatch(token, @"^(?:[A-Za-z]\.)+[A-Za-z]$");
        }
    }

    internal static class DetailedTranslationParser
    {
        internal static bool TryParse(string modelOutput, out TranslationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(modelOutput))
            {
                return false;
            }

            var cleaned = StripThinking(modelOutput).Trim();
            cleaned = Regex.Replace(cleaned, @"^\s*```(?:json)?\s*", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*```\s*$", string.Empty);
            cleaned = ExtractFirstJsonObject(cleaned);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return false;
            }

            Dictionary<string, object> root;
            try
            {
                root = new JavaScriptSerializer().DeserializeObject(cleaned)
                    as Dictionary<string, object>;
            }
            catch
            {
                return false;
            }
            if (root == null)
            {
                return false;
            }

            var kind = ParseKind(GetString(root, "type", "kind", "input_type"));
            if (kind == TranslationContentKind.PlainText)
            {
                var plainTranslation = GetString(root, "translation", "translated_text", "译文");
                if (string.IsNullOrWhiteSpace(plainTranslation))
                {
                    return false;
                }
                result = TranslationResult.CreatePlain(plainTranslation);
                return true;
            }

            if (kind == TranslationContentKind.Word || kind == TranslationContentKind.Phrase)
            {
                result = ParseVocabulary(root, kind);
            }
            else if (kind == TranslationContentKind.Sentence)
            {
                result = ParseSentence(root);
            }

            return result != null
                && (!string.IsNullOrWhiteSpace(result.Translation)
                    || result.Sections.Count > 0);
        }

        internal static string StripThinking(string text)
        {
            return Regex.Replace(
                text ?? string.Empty,
                @"<think>.*?</think>",
                string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();
        }

        private static string ExtractFirstJsonObject(string text)
        {
            var start = text.IndexOf('{');
            if (start < 0)
            {
                return null;
            }

            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var index = start; index < text.Length; index++)
            {
                var character = text[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == '{')
                {
                    depth++;
                }
                else if (character == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(start, index - start + 1);
                    }
                }
            }
            return null;
        }

        private static TranslationResult ParseVocabulary(
            Dictionary<string, object> root,
            TranslationContentKind kind)
        {
            var headword = GetString(root, "headword", "word", "phrase", "title");
            var translation = GetString(root, "translation", "summary", "overview", "中文释义");
            var phonetic = GetString(root, "phonetic", "ipa", "音标");
            var pronunciation = GetString(root, "pronunciation", "pronunciation_tip", "发音提示");
            var subheading = JoinNonEmpty("  ·  ", phonetic, pronunciation);

            var output = new TranslationResult
            {
                Kind = kind,
                Heading = headword,
                Subheading = subheading,
                Translation = translation,
                IsStructured = true
            };

            AddFormsSection(output, root);
            AddMeaningsSection(output, root);
            AddPhrasesSection(output, root);
            AddWordRelationsSection(output, root);
            return output;
        }

        private static TranslationResult ParseSentence(Dictionary<string, object> root)
        {
            var output = new TranslationResult
            {
                Kind = TranslationContentKind.Sentence,
                Heading = "句子解析",
                Translation = GetString(root, "translation", "translated_text", "译文"),
                IsStructured = true
            };

            AddSimpleSection(output, "句子主干", root, "sentence_core", "core", "主干");
            AddSimpleSection(output, "时态与语态", root, "tense_voice", "tense_and_voice", "时态语态");
            AddListSection(output, "从句与结构", root, "clauses", "structures", "从句");
            AddListSection(output, "关键语法", root, "grammar_points", "grammar", "语法");
            AddListSection(output, "关键表达", root, "key_phrases", "expressions", "重点短语");
            AddSimpleSection(output, "翻译提示", root, "translation_note", "note", "翻译提示");
            return output;
        }

        private static void AddFormsSection(
            TranslationResult output,
            Dictionary<string, object> root)
        {
            var forms = GetList(root, "forms", "base_forms", "inflections", "变形");
            if (forms.Count == 0)
            {
                return;
            }

            var section = new TranslationSection("原形与变形");
            foreach (var value in forms)
            {
                var dictionary = value as Dictionary<string, object>;
                if (dictionary == null)
                {
                    var text = Convert.ToString(value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        section.Items.Add(new TranslationItem(null, text, null));
                    }
                    continue;
                }

                var baseWord = GetString(dictionary, "base", "lemma", "original", "原形");
                var relation = GetString(dictionary, "relation", "form", "description", "变形说明");
                if (!string.IsNullOrWhiteSpace(baseWord) || !string.IsNullOrWhiteSpace(relation))
                {
                    section.Items.Add(new TranslationItem(baseWord, relation, null));
                }
            }
            AddIfNotEmpty(output, section);
        }

        private static void AddMeaningsSection(
            TranslationResult output,
            Dictionary<string, object> root)
        {
            var meanings = GetList(root, "meanings", "senses", "definitions", "释义");
            if (meanings.Count == 0)
            {
                return;
            }

            var section = new TranslationSection("词性与释义");
            foreach (var value in meanings)
            {
                var dictionary = value as Dictionary<string, object>;
                if (dictionary == null)
                {
                    var meaningText = Convert.ToString(value);
                    if (!string.IsNullOrWhiteSpace(meaningText))
                    {
                        section.Items.Add(new TranslationItem(null, meaningText, null));
                    }
                    continue;
                }

                var partOfSpeech = GetString(
                    dictionary,
                    "part_of_speech",
                    "pos",
                    "词性");
                var definitions = GetStringList(
                    dictionary,
                    "definitions",
                    "meanings",
                    "senses",
                    "释义");
                var body = JoinNumbered(definitions);
                var note = BuildExamples(dictionary);
                if (!string.IsNullOrWhiteSpace(partOfSpeech)
                    || !string.IsNullOrWhiteSpace(body)
                    || !string.IsNullOrWhiteSpace(note))
                {
                    section.Items.Add(new TranslationItem(partOfSpeech, body, note));
                }
            }
            AddIfNotEmpty(output, section);
        }

        private static void AddPhrasesSection(
            TranslationResult output,
            Dictionary<string, object> root)
        {
            var phrases = GetList(root, "phrases", "collocations", "常用短语");
            if (phrases.Count == 0)
            {
                return;
            }

            var section = new TranslationSection("常用短语与搭配");
            foreach (var value in phrases)
            {
                var dictionary = value as Dictionary<string, object>;
                if (dictionary == null)
                {
                    var phraseText = Convert.ToString(value);
                    if (!string.IsNullOrWhiteSpace(phraseText))
                    {
                        section.Items.Add(new TranslationItem(null, phraseText, null));
                    }
                    continue;
                }

                var phrase = GetString(dictionary, "phrase", "collocation", "短语");
                var meaning = GetString(dictionary, "meaning", "translation", "释义");
                var english = GetString(dictionary, "example_en", "example", "英文例句");
                var chinese = GetString(dictionary, "example_zh", "example_translation", "例句翻译");
                var note = BuildBilingualExample(english, chinese);
                section.Items.Add(new TranslationItem(phrase, meaning, note));
            }
            AddIfNotEmpty(output, section);
        }

        private static void AddWordRelationsSection(
            TranslationResult output,
            Dictionary<string, object> root)
        {
            var section = new TranslationSection("相关词汇");
            AddRelationItem(section, "同义词", GetStringList(root, "synonyms", "同义词"));
            AddRelationItem(section, "反义词", GetStringList(root, "antonyms", "反义词"));
            AddRelationItem(section, "易混词", GetStringList(root, "confusables", "easily_confused", "易混词"));
            AddIfNotEmpty(output, section);
        }

        private static void AddRelationItem(
            TranslationSection section,
            string heading,
            List<string> values)
        {
            if (values.Count > 0)
            {
                section.Items.Add(new TranslationItem(heading, string.Join(" · ", values.ToArray()), null));
            }
        }

        private static void AddSimpleSection(
            TranslationResult output,
            string title,
            Dictionary<string, object> root,
            params string[] keys)
        {
            var value = GetString(root, keys);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            var section = new TranslationSection(title);
            section.Items.Add(new TranslationItem(null, value, null));
            output.Sections.Add(section);
        }

        private static void AddListSection(
            TranslationResult output,
            string title,
            Dictionary<string, object> root,
            params string[] keys)
        {
            var values = GetList(root, keys);
            if (values.Count == 0)
            {
                return;
            }

            var section = new TranslationSection(title);
            foreach (var value in values)
            {
                var dictionary = value as Dictionary<string, object>;
                if (dictionary != null)
                {
                    var heading = GetString(dictionary, "name", "title", "structure", "phrase", "point");
                    var body = GetString(dictionary, "explanation", "meaning", "description", "content");
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        body = JoinDictionaryValues(dictionary);
                    }
                    section.Items.Add(new TranslationItem(heading, body, null));
                }
                else
                {
                    var text = Convert.ToString(value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        section.Items.Add(new TranslationItem(null, text, null));
                    }
                }
            }
            AddIfNotEmpty(output, section);
        }

        private static string BuildExamples(Dictionary<string, object> meaning)
        {
            var examples = GetList(meaning, "examples", "例句");
            var builder = new StringBuilder();
            foreach (var value in examples)
            {
                var dictionary = value as Dictionary<string, object>;
                string line;
                if (dictionary == null)
                {
                    line = Convert.ToString(value);
                }
                else
                {
                    line = BuildBilingualExample(
                        GetString(dictionary, "en", "english", "example", "英文"),
                        GetString(dictionary, "zh", "chinese", "translation", "中文"));
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }
                builder.Append(line);
            }
            return builder.ToString();
        }

        private static string BuildBilingualExample(string english, string chinese)
        {
            if (string.IsNullOrWhiteSpace(english))
            {
                return chinese;
            }
            if (string.IsNullOrWhiteSpace(chinese))
            {
                return "例： " + english;
            }
            return "例： " + english + Environment.NewLine + "　　" + chinese;
        }

        private static void AddIfNotEmpty(
            TranslationResult output,
            TranslationSection section)
        {
            if (section.Items.Count > 0)
            {
                output.Sections.Add(section);
            }
        }

        private static TranslationContentKind ParseKind(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "word":
                case "单词":
                    return TranslationContentKind.Word;
                case "phrase":
                case "短语":
                    return TranslationContentKind.Phrase;
                case "sentence":
                case "句子":
                    return TranslationContentKind.Sentence;
                case "text":
                case "plain":
                case "article":
                case "multi_sentence":
                case "多句":
                case "段落":
                    return TranslationContentKind.PlainText;
                default:
                    return TranslationContentKind.Auto;
            }
        }

        private static string GetString(
            Dictionary<string, object> dictionary,
            params string[] keys)
        {
            object value;
            if (!TryGetValue(dictionary, keys, out value) || value == null)
            {
                return null;
            }
            var text = value as string;
            if (text != null)
            {
                return text.Trim();
            }

            var values = AsList(value);
            if (values.Count > 0)
            {
                var strings = new List<string>();
                foreach (var item in values)
                {
                    if (item != null)
                    {
                        strings.Add(Convert.ToString(item));
                    }
                }
                return string.Join("；", strings.ToArray());
            }
            return Convert.ToString(value).Trim();
        }

        private static List<string> GetStringList(
            Dictionary<string, object> dictionary,
            params string[] keys)
        {
            object value;
            var results = new List<string>();
            if (!TryGetValue(dictionary, keys, out value) || value == null)
            {
                return results;
            }

            foreach (var item in AsList(value))
            {
                var itemDictionary = item as Dictionary<string, object>;
                var text = itemDictionary == null
                    ? Convert.ToString(item)
                    : JoinDictionaryValues(itemDictionary);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(text.Trim());
                }
            }
            if (results.Count == 0 && value is string)
            {
                results.Add(Convert.ToString(value).Trim());
            }
            return results;
        }

        private static List<object> GetList(
            Dictionary<string, object> dictionary,
            params string[] keys)
        {
            object value;
            if (!TryGetValue(dictionary, keys, out value) || value == null)
            {
                return new List<object>();
            }
            return AsList(value);
        }

        private static List<object> AsList(object value)
        {
            var results = new List<object>();
            if (value == null)
            {
                return results;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string)
                && !(value is Dictionary<string, object>))
            {
                foreach (var item in enumerable)
                {
                    results.Add(item);
                }
                return results;
            }
            results.Add(value);
            return results;
        }

        private static bool TryGetValue(
            Dictionary<string, object> dictionary,
            string[] keys,
            out object value)
        {
            foreach (var key in keys)
            {
                if (dictionary.TryGetValue(key, out value))
                {
                    return true;
                }
                foreach (var pair in dictionary)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        private static string JoinDictionaryValues(Dictionary<string, object> dictionary)
        {
            var values = new List<string>();
            foreach (var pair in dictionary)
            {
                if (pair.Value == null)
                {
                    continue;
                }
                var text = pair.Value as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text.Trim());
                }
            }
            return string.Join("：", values.ToArray());
        }

        private static string JoinNumbered(List<string> values)
        {
            if (values.Count <= 1)
            {
                return values.Count == 0 ? null : values[0];
            }

            var builder = new StringBuilder();
            for (var index = 0; index < values.Count; index++)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }
                builder.Append(index + 1);
                builder.Append(". ");
                builder.Append(values[index]);
            }
            return builder.ToString();
        }

        private static string JoinNonEmpty(string separator, params string[] values)
        {
            var results = new List<string>();
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value.Trim());
                }
            }
            return string.Join(separator, results.ToArray());
        }
    }
}
