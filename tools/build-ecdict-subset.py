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


def keep_entry(row):
    word = (row.get("word") or "").strip()
    translation = (row.get("translation") or "").strip()
    if not word or not translation or not ENGLISH_ENTRY.match(word):
        return False
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


def build_subset(source_path, output_path):
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    count = 0
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
                    output.write("# ECDICT learning subset v1\n")
                    for row in csv.DictReader(source):
                        if not keep_entry(row):
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
    return count


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", help="Path to the original ECDICT CSV")
    parser.add_argument("output", help="Path to the generated .tsv.gz subset")
    args = parser.parse_args()
    count = build_subset(args.source, args.output)
    size_mb = os.path.getsize(args.output) / (1024 * 1024)
    print(f"Wrote {count} entries ({size_mb:.2f} MiB): {args.output}")


if __name__ == "__main__":
    main()
