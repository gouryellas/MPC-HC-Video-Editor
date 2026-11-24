"""
Builds the downloadable translation lists (translations-<code>.txt) from the
kaikki.org Wiktionary extracts.

Usage:
    python -I tools/build-translations.py <folder with *.jsonl.gz> <output folder>

Each input is kaikki.org-dictionary-<Language>.jsonl.gz, renamed to
<Language>.jsonl.gz. Output is one "foreign,english" pair per line, which is
the format translations.txt uses.

Only words whose meaning is obvious are kept:
  * the first sense of the word's first entry, which Wiktionary lists first
    because it is the commonest, and only when its gloss comes down to one or
    two plain English words;
  * no names, abbreviations, affixes, letters or symbols;
  * nothing obsolete, archaic or rare;
  * nothing that is itself an English word. "red" is Spanish for "net" and
    "pain" French for "bread", and translating those would wreck English
    filenames. The English vocabulary is every word used in the glosses of all
    the dictionaries given, which needs no separate English list.

The data is CC BY-SA (Wiktionary, via kaikki.org), so each file carries a
credit line.
"""

import gzip
import json
import os
import re
import sys
import unicodedata

LANGUAGES = {"Spanish": "es", "Russian": "ru", "French": "fr"}

KEEP_POS = {"noun", "verb", "adj", "adv", "num", "prep", "conj", "pron", "intj", "det"}
SKIP_TAGS = {"obsolete", "archaic", "rare", "dated", "form-of", "alt-of",
             "abbreviation", "initialism", "acronym", "misspelling", "proscribed"}

FUNCTION_WORDS = {"of", "to", "the", "a", "an", "and", "or", "in", "on", "at", "for",
                  "with", "from", "by", "as", "relating", "pertaining", "used", "someone",
                  "something", "one", "who", "which", "that", "being"}

WORD = re.compile(r"^[a-z]+(?: [a-z]+)?$")


def fold(text):
    text = unicodedata.normalize("NFD", text.lower())
    return "".join(c for c in text if unicodedata.category(c) != "Mn")


def clean_gloss(gloss, pos):
    g = re.sub(r"\([^)]*\)", "", gloss)          # parenthetical notes
    g = re.split(r"[;,:]", g)[0]                  # first of several
    g = g.strip().lower()
    if pos == "verb" and g.startswith("to "):
        g = g[3:]
    for article in ("a ", "an ", "the "):
        if g.startswith(article):
            g = g[len(article):]
    g = g.strip(" .")
    if not WORD.match(g):
        return None
    # What is left of "of Maracay" or "relating to ..." once a name or a
    # clause has gone is not a translation.
    if g.split()[0] in FUNCTION_WORDS or g.split()[-1] in FUNCTION_WORDS:
        return None
    return g


def entries(path):
    with gzip.open(path, "rt", encoding="utf-8") as f:
        for line in f:
            try:
                yield json.loads(line)
            except ValueError:
                continue


def english_vocabulary(path):
    # Words used as an English translation, not every word in a gloss. A
    # gloss can name the foreign word itself ("diminutive of perro"), which
    # would make every common Spanish word look English.
    vocab = set()
    for e in entries(path):
        pos = e.get("pos", "")
        for s in e.get("senses", []):
            if {"form-of", "alt-of"} & set(s.get("tags", [])):
                continue
            for g in s.get("glosses", []):
                english = clean_gloss(g, pos)
                if english:
                    vocab.update(english.split())
    return vocab


# The grammar words in a gloss such as "feminine singular of hermoso" or
# "inflection of красивый". An adjective's forms all translate to the same
# English word, so they can take the base word's translation.
GRAMMAR = {"masculine", "feminine", "neuter", "singular", "plural", "inflection",
           "nominative", "genitive", "dative", "accusative", "instrumental",
           "prepositional", "animate", "inanimate", "short", "form", "and"}
FORM_OF = re.compile(r"^([a-z ]+?) of (\S+?):?$")
FEMALE_OF = re.compile(r"^female equivalent of \S+?: (.+)$")


def build(path, vocab, cyrillic):
    chosen = {}
    pending = {}
    for e in entries(path):
        word = e.get("word", "")
        pos = e.get("pos", "")
        if pos not in KEEP_POS or not word or word != word.lower():
            continue
        if len(word) < 3 or " " in word or "," in word or "-" in word:
            continue
        key = fold(word)
        if key in chosen or key in pending:
            continue  # first entry wins: it is the commonest
        if not cyrillic and key in vocab:
            continue  # also an English word

        senses = e.get("senses", [])
        if not senses:
            continue
        first = senses[0]
        glosses = first.get("glosses", [])
        if not glosses:
            continue
        tags = set(first.get("tags", []))

        if "form-of" in tags:
            # "female equivalent of chico: girl" carries its own translation.
            m = FEMALE_OF.match(glosses[0])
            if m:
                english = clean_gloss(m.group(1), pos)
                if english and fold(english) != key:
                    chosen[key] = (word, english)
                continue
            # An adjective's other forms translate as the base word does.
            m = FORM_OF.match(glosses[0].strip())
            if pos == "adj" and m and set(m.group(1).split()) <= GRAMMAR:
                pending[key] = (word, fold(m.group(2)))
            continue

        if SKIP_TAGS & tags:
            continue
        english = clean_gloss(glosses[0], pos)
        if not english or fold(english) == key:
            continue
        chosen[key] = (word, english)

    for key, (word, base) in pending.items():
        if key not in chosen and base in chosen:
            chosen[key] = (word, chosen[base][1])

    return sorted(chosen.values())


def main():
    src, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    present = [l for l in LANGUAGES if os.path.exists(os.path.join(src, l + ".jsonl.gz"))]

    # Each language's English vocabulary comes from the other dictionaries.
    # A dictionary's own glosses mention its own words often enough ("agua de
    # coco") to make them look English.
    print("Reading English vocabulary from glosses...")
    vocabs = {l: english_vocabulary(os.path.join(src, l + ".jsonl.gz")) for l in present}

    for lang in present:
        code = LANGUAGES[lang]
        others = [v for l, v in vocabs.items() if l != lang] or [vocabs[lang]]
        vocab = set().union(*others)
        pairs = build(os.path.join(src, lang + ".jsonl.gz"), vocab, cyrillic=(lang == "Russian"))
        target = os.path.join(out, f"translations-{code}.txt")
        with open(target, "w", encoding="utf-8", newline="\r\n") as f:
            f.write(f"# {lang} to English, for MPC-HC Video Editor.\n")
            f.write("# From Wiktionary (https://www.wiktionary.org) via kaikki.org,\n")
            f.write("# licensed CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0/).\n")
            for word, english in pairs:
                f.write(f"{word},{english}\n")
        print(f"{lang}: {len(pairs)} words -> {target}")


if __name__ == "__main__":
    main()
