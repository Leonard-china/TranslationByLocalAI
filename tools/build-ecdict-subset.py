#!/usr/bin/env python3
"""Build the compact learning dictionary shipped with TranslationByLocalAI."""

import argparse
import csv
import gzip
import io
import os
import re


ENGLISH_ENTRY = re.compile(r"^[A-Za-z][A-Za-z0-9 .'’-]*$")


def parse_positive_int(value):
    try:
        return int(value or 0) > 0
    except ValueError:
        return False


def keep_entry(row, required_words=None):
    word = (row.get("word") or "").strip()
    translation = (row.get("translation") or "").strip()
    if not word or not translation or not ENGLISH_ENTRY.match(word):
        return False
    if required_words and word.lower().replace("’", "'") in required_words:
        return True
    return (
        parse_positive_int(row.get("bnc"))
        or parse_positive_int(row.get("frq"))
        or bool((row.get("collins") or "").strip())
        or bool((row.get("oxford") or "").strip())
        or bool((row.get("tag") or "").strip())
    )


def escape_field(value):
    normalized = (value or "").replace("\r\n", "\n").replace("\r", "\n")
    normalized = normalized.replace("\\n", "\n")
    return (
        normalized.replace("\\", "\\\\")
        .replace("\t", "\\t")
        .replace("\n", "\\n")
    )


def build_subset(source_path, output_path, required_words=None):
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    count = 0
    included_words = set()
    with open(source_path, "r", encoding="utf-8", newline="") as source:
        with open(output_path, "wb") as raw_output:
            with gzip.GzipFile(
                filename="",
                mode="wb",
                fileobj=raw_output,
                compresslevel=9,
                mtime=0,
            ) as compressed:
                with io.TextIOWrapper(compressed, encoding="utf-8", newline="\n") as output:
                    output.write("# ECDICT learning subset v2 + offline article vocabulary\n")
                    for row in csv.DictReader(source):
                        if not keep_entry(row, required_words):
                            continue
                        fields = (
                            row.get("word"),
                            row.get("phonetic"),
                            row.get("translation"),
                            row.get("pos"),
                            row.get("exchange"),
                        )
                        output.write("\t".join(escape_field(value) for value in fields))
                        output.write("\n")
                        count += 1
                        included_words.add(
                            (row.get("word") or "").strip().lower().replace("’", "'")
                        )

                    # A reading must never lead to a dead click. ECDICT covers
                    # virtually all normal vocabulary; remaining items are
                    # mainly names, abbreviations and newly coined compounds.
                    # Keep explicit local entries for those article tokens so
                    # the UI can identify them instantly and without AI.
                    for word in sorted((required_words or set()) - included_words):
                        if word.endswith("'s") and len(word) > 2:
                            base = word[:-2]
                            translation = "文章词汇；“" + base + "”的所有格形式"
                            exchange = "0:" + base + "/1:s"
                        elif "-" in word:
                            translation = (
                                "文章中的复合词；由 "
                                + " + ".join(part for part in word.split("-") if part)
                                + " 构成"
                            )
                            exchange = ""
                        else:
                            translation = "文章中的专有名词、缩写或新词（本地词条）"
                            exchange = ""
                        fields = (word, "", translation, "", exchange)
                        output.write(
                            "\t".join(escape_field(value) for value in fields)
                        )
                        output.write("\n")
                        count += 1
    return count


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", help="Path to the original ECDICT CSV")
    parser.add_argument("output", help="Path to the generated .tsv.gz subset")
    parser.add_argument(
        "--word-list",
        help="Optional UTF-8 file whose words must be included when ECDICT has them",
    )
    args = parser.parse_args()
    required_words = None
    if args.word_list:
        with open(args.word_list, "r", encoding="utf-8") as source:
            required_words = {
                line.strip().lower().replace("’", "'")
                for line in source
                if line.strip()
            }
    count = build_subset(args.source, args.output, required_words)
    size_mb = os.path.getsize(args.output) / (1024 * 1024)
    print(f"Wrote {count} entries ({size_mb:.2f} MiB): {args.output}")


if __name__ == "__main__":
    main()
