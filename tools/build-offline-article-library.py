#!/usr/bin/env python3
"""Build the licensed offline English reading library.

The generated bundle intentionally contains only:
* Original VOA Learning English text (public domain). Wire-service adaptations
  are rejected by both metadata and credit-line checks.
* Abstracts from individually selected Nature Portfolio open-access papers.

This is a maintainer tool, not part of the normal application build.
"""

import argparse
import concurrent.futures
import datetime as dt
import gzip
import hashlib
import html
from html.parser import HTMLParser
import json
import os
import re
import time
import urllib.error
import urllib.parse
import urllib.request


BASE_URL = "https://learningenglish.voanews.com"
VOA_LICENSE_URL = "https://learningenglish.voanews.com/p/6861.html"
USER_AGENT = "TranslationByLocalAI offline educational library builder/1.0"

# Quotas add up to 188. Twelve selected Nature Portfolio abstracts bring the
# checked-in library to exactly 200 readings.
VOA_SECTIONS = (
    ("Health & Lifestyle", 955, 22),
    ("Science & Technology", 1579, 25),
    ("Education Tips", 7468, 20),
    ("Ask a Teacher", 5535, 24),
    ("Everyday Grammar", 4456, 28),
    ("Arts & Culture", 986, 14),
    ("As It Is", 3521, 18),
    ("Education", 959, 17),
    ("Words & Their Stories", 987, 20),
)

# All are Nature Portfolio DOIs. Scientific Reports articles are published
# open access; the builder still rejects any record whose deposited license is
# not a supported Creative Commons license.
NATURE_DOIS = (
    "10.1038/s41598-025-33775-0",
    "10.1038/s41598-024-56055-9",
    "10.1038/s41598-025-88398-2",
    "10.1038/s41598-023-44717-z",
    "10.1038/s41598-025-92784-1",
    "10.1038/s41598-022-11924-z",
    "10.1038/s41598-026-58879-z",
    "10.1038/s41598-026-37302-7",
    "10.1038/s41598-025-86252-z",
    "10.1038/s41598-023-38439-5",
    "10.1038/s41598-023-35804-2",
    "10.1038/s41598-023-32069-7",
)

WIRE_MARKERS = (
    "associated press",
    "reuters",
    "agence france-presse",
    "the associated press reported this story",
    "the associated press adapted this story",
    "reuters reported this story",
    "reuters adapted this story",
    "agence france-presse reported this story",
    "agence france-presse adapted this story",
    "adapted this story for voa learning english",
    "this story was adapted from",
    "copyright associated press",
    "copyright reuters",
)

OFF_TOPIC_TITLE_MARKERS = (
    "election",
    "presidential campaign",
    "trump",
    "biden",
    "ukraine war",
    "gaza war",
    "israel-hamas",
    "military strike",
)

TOPIC_RULES = (
    ("人与自然", ("climate", "environment", "nature", "animal", "plant", "garden",
                  "ocean", "forest", "wildlife", "pollution", "weather", "earth")),
    ("健康生活", ("health", "sleep", "food", "exercise", "medical", "virus", "stress",
                  "diet", "brain", "wellbeing", "well-being", "mental", "cancer")),
    ("科技创新", ("technology", "science", "ai ", "artificial intelligence", "robot",
                  "space", "computer", "research", "energy", "internet")),
    ("教育成长", ("school", "student", "teacher", "learn", "education", "college",
                  "study", "reading", "language", "grammar")),
    ("文化艺术", ("art", "music", "film", "book", "museum", "culture", "story",
                  "festival", "history")),
    ("社会沟通", ("community", "family", "friend", "work", "social", "volunteer",
                  "communication", "city", "people")),
)

WORD_RE = re.compile(r"[A-Za-z]+(?:['’\-][A-Za-z]+)*")
SENTENCE_RE = re.compile(r"(?<=[.!?])\s+")
TAG_RE = re.compile(r"<[^>]+>")


def request_text(url, attempts=5):
    cache_directory = os.path.join("dist", "article-source-cache")
    os.makedirs(cache_directory, exist_ok=True)
    cache_path = os.path.join(
        cache_directory, hashlib.sha256(url.encode("utf-8")).hexdigest() + ".txt")
    if os.path.exists(cache_path):
        with open(cache_path, "r", encoding="utf-8") as cached:
            return cached.read()

    request = urllib.request.Request(
        url,
        headers={"User-Agent": USER_AGENT, "Accept": "text/html,application/json"},
    )
    for attempt in range(attempts):
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                text = response.read().decode("utf-8", errors="replace")
                with open(cache_path, "w", encoding="utf-8", newline="\n") as cached:
                    cached.write(text)
                return text
        except (urllib.error.URLError, TimeoutError):
            if attempt + 1 == attempts:
                raise
            time.sleep(1.5 * (attempt + 1))


class ArticleBodyParser(HTMLParser):
    """Extract block text from the main VOA .wsw article container."""

    BLOCK_TAGS = {"p", "h2", "h3", "li"}
    SKIP_TAGS = {"script", "style", "figure", "audio", "video", "svg"}

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.in_article = False
        self.article_div_depth = 0
        self.skip_depth = 0
        self.current_tag = None
        self.current_parts = []
        self.blocks = []

    def handle_starttag(self, tag, attrs):
        attributes = dict(attrs)
        classes = set((attributes.get("class") or "").split())

        if not self.in_article and tag == "div" and "wsw" in classes:
            self.in_article = True
            self.article_div_depth = 1
            return
        if not self.in_article:
            return

        if tag == "div":
            self.article_div_depth += 1

        if self.skip_depth:
            self.skip_depth += 1
            return
        if (tag in self.SKIP_TAGS
                or "wsw__embed" in classes
                or "quiz" in classes
                or "media-pholder" in classes):
            self.skip_depth = 1
            return
        if tag in self.BLOCK_TAGS:
            self.current_tag = tag
            self.current_parts = []
        elif tag == "br" and self.current_tag:
            self.current_parts.append("\n")

    def handle_endtag(self, tag):
        if not self.in_article:
            return
        if self.skip_depth:
            self.skip_depth -= 1
        elif self.current_tag == tag:
            text = normalize_space("".join(self.current_parts))
            if text:
                self.blocks.append((tag, text))
            self.current_tag = None
            self.current_parts = []

        if tag == "div":
            self.article_div_depth -= 1
            if self.article_div_depth <= 0:
                self.in_article = False

    def handle_data(self, data):
        if self.in_article and not self.skip_depth and self.current_tag:
            self.current_parts.append(data)


def normalize_space(value):
    value = html.unescape(value or "")
    value = value.replace("\xa0", " ").replace("\u200b", "")
    value = re.sub(r"[ \t]+", " ", value)
    value = re.sub(r"\s*\n\s*", "\n", value)
    return value.strip()


def extract_meta(document, name=None, property_name=None):
    expected_key = "name" if name else "property"
    expected_value = name if name else property_name
    for tag in re.findall(r"<meta\b[^>]*>", document, re.I | re.S):
        attributes = {}
        for match in re.finditer(
                r"([:\w-]+)\s*=\s*([\"'])(.*?)\2", tag, re.I | re.S):
            attributes[match.group(1).lower()] = match.group(3)
        if (attributes.get(expected_key, "").lower()
                == (expected_value or "").lower()):
            return normalize_space(attributes.get("content"))
    return ""


def extract_json_ld(document):
    for match in re.finditer(
            r'<script[^>]+type=["\']application/ld\+json["\'][^>]*>(.*?)</script>',
            document, re.I | re.S):
        try:
            value = json.loads(html.unescape(match.group(1)))
            if isinstance(value, dict) and value.get("@type") in (
                    "NewsArticle", "Article", "ScholarlyArticle"):
                return value
        except (ValueError, TypeError):
            pass
    return {}


def extract_voa_links(section_id, pages):
    found = []
    seen = set()
    for page in range(1, pages + 1):
        document = request_text(f"{BASE_URL}/z/{section_id}?p={page}")
        for path in re.findall(r'href=["\'](/a/[^"\']+/\d+\.html)["\']', document):
            url = BASE_URL + path
            if url not in seen:
                seen.add(url)
                found.append(url)
    return found


def content_without_learning_glossary(blocks):
    cleaned = []
    for tag, block in blocks:
        lower = block.lower()
        if lower in ("words in this story", "quiz", "conversation"):
            break
        if lower.startswith("write to us in the comments"):
            break
        cleaned.append((tag, block))
    return cleaned


def clickable_html_text(value):
    parts = []
    position = 0
    for match in WORD_RE.finditer(value or ""):
        parts.append(html.escape(value[position:match.start()]))
        word = match.group(0)
        parts.append(
            '<span class="word" data-word="'
            + html.escape(word, quote=True)
            + '">'
            + html.escape(word)
            + "</span>"
        )
        position = match.end()
    parts.append(html.escape((value or "")[position:]))
    return "".join(parts).replace("\n", "<br>")


def blocks_to_html(blocks):
    output = []
    for tag, text in blocks:
        semantic_tag = tag if tag in ("h2", "h3") else "p"
        css_class = ' class="list-item"' if tag == "li" else ""
        output.append(
            "<" + semantic_tag + css_class + ">"
            + clickable_html_text(text)
            + "</" + semantic_tag + ">"
        )
    return "\n".join(output)


def reading_paragraphs(content, sentences_per_paragraph=3):
    existing = [
        normalize_space(value)
        for value in re.split(r"\n\s*\n", content or "")
        if normalize_space(value)
    ]
    if len(existing) > 1:
        return existing
    sentences = [
        normalize_space(value)
        for value in SENTENCE_RE.split(content or "")
        if normalize_space(value)
    ]
    if len(sentences) <= sentences_per_paragraph:
        return existing or sentences
    return [
        " ".join(sentences[index:index + sentences_per_paragraph])
        for index in range(0, len(sentences), sentences_per_paragraph)
    ]


def parse_date(value):
    if not value:
        return ""
    match = re.match(r"(\d{4}-\d{2}-\d{2})", value)
    return match.group(1) if match else ""


def classify_topic(title, section, content):
    haystack = (title + " " + section + " " + content[:1800]).lower()
    scores = []
    for topic, markers in TOPIC_RULES:
        scores.append((sum(haystack.count(marker) for marker in markers), topic))
    score, topic = max(scores)
    if score:
        return topic
    if "grammar" in section.lower() or "teacher" in section.lower():
        return "教育成长"
    if "arts" in section.lower():
        return "文化艺术"
    return "社会沟通"


def readability(content):
    words = WORD_RE.findall(content)
    sentences = [part for part in SENTENCE_RE.split(content) if WORD_RE.search(part)]
    word_count = len(words)
    sentence_count = max(1, len(sentences))
    long_words = sum(1 for word in words if len(word) >= 9)
    avg_sentence = word_count / sentence_count
    long_ratio = long_words / max(1, word_count)

    if avg_sentence <= 16 and long_ratio <= 0.11:
        difficulty = "基础"
    elif avg_sentence <= 21 and long_ratio <= 0.16:
        difficulty = "进阶"
    else:
        difficulty = "挑战"

    if word_count < 450:
        length_band = "短篇"
    elif word_count <= 850:
        length_band = "中篇"
    else:
        length_band = "长篇"
    return word_count, difficulty, length_band


def parse_voa_article(candidate):
    section, url = candidate
    document = request_text(url)
    lower_document = document.lower()
    title = extract_meta(document, property_name="og:title")
    if not title:
        title = extract_meta(document, name="title")
    if not title or any(marker in title.lower() for marker in OFF_TOPIC_TITLE_MARKERS):
        return None

    parser = ArticleBodyParser()
    parser.feed(document)
    blocks = content_without_learning_glossary(parser.blocks)
    content = "\n\n".join(text for _, text in blocks)
    lower_content = content.lower()
    if any(marker in lower_content for marker in WIRE_MARKERS):
        return None
    if '"copied":"yes"' in lower_document:
        return None

    metadata = extract_json_ld(document)
    date = parse_date(str(metadata.get("datePublished") or ""))
    if not date:
        date_match = re.search(r'"pub_datetime":"(\d{4}-\d{2}-\d{2})', document)
        date = date_match.group(1) if date_match else ""
    if date and date < "2022-01-01":
        return None

    word_count, difficulty, length_band = readability(content)
    if word_count < 260 or word_count > 1800:
        return None

    author_value = metadata.get("author")
    if isinstance(author_value, dict):
        author = normalize_space(author_value.get("name"))
    elif isinstance(author_value, list):
        author = ", ".join(
            normalize_space(item.get("name"))
            for item in author_value if isinstance(item, dict) and item.get("name")
        )
    else:
        author = "VOA Learning English"

    article_id = re.search(r"/(\d+)\.html$", url).group(1)
    return {
        "Id": "voa-" + article_id,
        "Title": title,
        "Source": "VOA Learning English",
        "Section": section,
        "Topic": classify_topic(title, section, content),
        "Difficulty": difficulty,
        "LengthBand": length_band,
        "WordCount": word_count,
        "PublishedDate": date,
        "Author": author or "VOA Learning English",
        "Url": url,
        "License": "Public domain (VOA original); excludes AP/Reuters/AFP",
        "LicenseUrl": VOA_LICENSE_URL,
        "Content": content,
        "Html": blocks_to_html(blocks),
    }


def clean_jats(value):
    paragraph_marker = "[[OFFLINE_READING_PARAGRAPH]]"
    value = re.sub(
        r"</jats:p>\s*<jats:p[^>]*>",
        paragraph_marker,
        value or "",
        flags=re.I,
    )
    value = re.sub(
        r"<jats:title[^>]*>\s*abstract\s*</jats:title>",
        "",
        value,
        flags=re.I,
    )
    value = html.unescape(TAG_RE.sub("", value))
    value = re.sub(r"\s+", " ", value).strip()
    return value.replace(paragraph_marker, "\n\n")


def crossref_item(doi):
    endpoint = "https://api.crossref.org/works/" + urllib.parse.quote(doi, safe="")
    payload = json.loads(request_text(endpoint))
    return payload["message"]


def date_from_parts(item):
    for field in ("published-online", "published-print", "published", "issued"):
        parts = ((item.get(field) or {}).get("date-parts") or [])
        if not parts or not parts[0]:
            continue
        values = list(parts[0]) + [1, 1]
        return f"{int(values[0]):04d}-{int(values[1]):02d}-{int(values[2]):02d}"
    return ""


def parse_nature_article(doi):
    item = crossref_item(doi)
    licenses = item.get("license") or []
    allowed = [
        entry.get("URL", "") for entry in licenses
        if "creativecommons.org/licenses/by" in entry.get("URL", "").lower()
    ]
    if not allowed:
        raise RuntimeError("Nature DOI has no supported CC license: " + doi)

    content = clean_jats(item.get("abstract"))
    paragraphs = reading_paragraphs(content)
    content = "\n\n".join(paragraphs)
    word_count, difficulty, length_band = readability(content)
    if word_count < 100:
        raise RuntimeError("Nature abstract is unexpectedly short: " + doi)

    authors = []
    for author in item.get("author") or []:
        name = normalize_space(
            ((author.get("given") or "") + " " + (author.get("family") or "")).strip()
        )
        if name:
            authors.append(name)
    if len(authors) > 4:
        authors = authors[:3] + ["et al."]

    title_values = item.get("title") or []
    journals = item.get("container-title") or []
    url = "https://www.nature.com/articles/" + doi.split("/", 1)[1]
    return {
        "Id": "nature-" + doi.replace("/", "-").replace(".", "-"),
        "Title": normalize_space(title_values[0] if title_values else doi),
        "Source": "Nature Portfolio",
        "Section": normalize_space(journals[0] if journals else "Open Access Research"),
        "Topic": classify_topic(
            title_values[0] if title_values else "", "Nature Portfolio", content),
        "Difficulty": difficulty,
        "LengthBand": length_band,
        "WordCount": word_count,
        "PublishedDate": date_from_parts(item),
        "Author": ", ".join(authors),
        "Url": url,
        "License": "Open access · " + allowed[0].rstrip("/").rsplit("/", 2)[-2].upper(),
        "LicenseUrl": allowed[0],
        "Content": content,
        "Html": blocks_to_html([("p", paragraph) for paragraph in paragraphs]),
    }


def select_voa_articles(candidates_by_section, target_count):
    selected = []
    selected_ids = set()
    all_articles = []

    flat_candidates = []
    for section, links in candidates_by_section.items():
        flat_candidates.extend((section, link) for link in links)

    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as executor:
        futures = {
            executor.submit(parse_voa_article, candidate): candidate
            for candidate in flat_candidates
        }
        completed = 0
        for future in concurrent.futures.as_completed(futures):
            completed += 1
            try:
                article = future.result()
                if article:
                    all_articles.append(article)
            except Exception as error:
                print("WARN article:", futures[future][1], error)
            if completed % 50 == 0:
                print(f"Fetched {completed}/{len(futures)} VOA candidates")

    all_articles.sort(
        key=lambda item: (item["PublishedDate"], item["WordCount"]),
        reverse=True,
    )
    quotas = {name: quota for name, _, quota in VOA_SECTIONS}
    for section, _, _ in VOA_SECTIONS:
        section_items = [item for item in all_articles if item["Section"] == section]
        for item in section_items[:quotas[section]]:
            if item["Id"] not in selected_ids:
                selected.append(item)
                selected_ids.add(item["Id"])

    if len(selected) < target_count:
        for item in all_articles:
            if item["Id"] in selected_ids:
                continue
            selected.append(item)
            selected_ids.add(item["Id"])
            if len(selected) >= target_count:
                break

    if len(selected) < target_count:
        counts = {}
        for item in all_articles:
            counts[item["Section"]] = counts.get(item["Section"], 0) + 1
        raise RuntimeError(
            f"Only {len(selected)} eligible VOA originals; need {target_count}. "
            f"Eligible by section: {counts}"
        )
    return selected[:target_count]


def write_bundle(articles, output_path, vocabulary_path):
    articles.sort(key=lambda item: (item["Topic"], item["Title"].lower()))
    library = {
        "SchemaVersion": 1,
        "GeneratedDate": dt.date.today().isoformat(),
        "ArticleCount": len(articles),
        "Articles": articles,
    }
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    encoded = json.dumps(
        library, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    with open(output_path, "wb") as raw:
        with gzip.GzipFile(
                filename="", mode="wb", fileobj=raw, compresslevel=9, mtime=0) as target:
            target.write(encoded)

    words = sorted({
        word.lower().replace("’", "'")
        for article in articles
        for word in WORD_RE.findall(article["Title"] + "\n" + article["Content"])
    })
    with open(vocabulary_path, "w", encoding="utf-8", newline="\n") as target:
        target.write("\n".join(words))
        target.write("\n")


def print_summary(articles, output_path):
    print(f"Wrote {len(articles)} articles: {output_path}")
    for field in ("Source", "Difficulty", "LengthBand", "Topic"):
        counts = {}
        for item in articles:
            value = item[field]
            counts[value] = counts.get(value, 0) + 1
        print(field + ":", ", ".join(
            f"{key}={value}" for key, value in sorted(counts.items())))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output", default="resources/offline-articles.json.gz")
    parser.add_argument(
        "--vocabulary", default="resources/article-vocabulary.txt")
    parser.add_argument(
        "--pages-per-section", type=int, default=6)
    args = parser.parse_args()

    candidates = {}
    for section, section_id, _ in VOA_SECTIONS:
        links = extract_voa_links(section_id, args.pages_per_section)
        candidates[section] = links
        print(f"{section}: {len(links)} candidates")

    voa_articles = select_voa_articles(candidates, 188)
    nature_articles = []
    for doi in NATURE_DOIS:
        nature_articles.append(parse_nature_article(doi))
        time.sleep(0.2)

    articles = voa_articles + nature_articles
    if len(articles) != 200:
        raise RuntimeError(f"Expected 200 articles, got {len(articles)}")
    write_bundle(articles, args.output, args.vocabulary)
    print_summary(articles, args.output)


if __name__ == "__main__":
    main()
