"""
Unified Training Data Validator v3
===================================
Validates ALL 27k entries. Two sources of truth:
  1. Rule-based decoder (confusables + leet + accents + reversed)
  2. Dictionary-based word correction (i↔l, v↔u, b↔g ambiguity)

When decoder + haiku + dictionary agree → verified.
No data is removed. No LLM used. Everything stays.

Usage:
    python tools/validate_training_data.py           # report only
    python tools/validate_training_data.py --fix      # write training_data_v2.jsonl
"""

import argparse
import json
import re
import sys
from itertools import product
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

sys.path.insert(0, str(Path(__file__).parent))
from confusables import CONFUSABLES, LEET_MAP, AMBIGUOUS_PAIRS, WORD_DICTIONARY

SCRIPT_DIR = Path(__file__).parent
DATA_FILE = SCRIPT_DIR / "training_data_final.jsonl"
OUTPUT_FILE = SCRIPT_DIR / "training_data_v2.jsonl"
REPORT_FILE = SCRIPT_DIR / "validation_report.jsonl"

# ============================================================================
# Supplemental mappings
# ============================================================================
EXTRA_CONFUSABLES = {
    '\u157C': 'H', '\u142F': 'V',
    '\uA4D5': 'T', '\uA4E9': 'S', '\uA4F7': 'D', '\uA4DA': 'C', '\uA4D8': 'K',
    '\uA4F6': 'F', '\uA4F5': 'F', '\uA4E0': 'N', '\uA4DE': 'Y', '\uA4F2': 'I',
    '\uA4D6': 'G', '\uA4E7': 'H', '\uA4EE': 'E', '\uA4E4': 'U', '\uA4DB': 'C',
    '\uA4ED': 'G', '\uA4F0': 'A', '\uA4D1': 'N', '\uA4E3': 'N', '\uA4DD': 'F',
    '\uA4E2': 'S', '\uA4E8': 'Y', '\uA4D0': 'A', '\uA4EC': 'X', '\uA4E6': 'V',
    '\u13AA': 'L', '\u13D2': 'S', '\u13C6': 'G', '\u13A7': 'A', '\u13A6': 'T',
    '\u13B2': 'H', '\u13AB': 'E', '\u13B1': 'N', '\u13BE': 'P', '\u13AD': 'L',
    '\u13C7': 'M', '\u13A5': 'I', '\u13F9': 'W', '\u13E2': 'R', '\u13DC': 'U',
    '\u0258': 'e', '\u0252': 'a', '\u027F': 'r', '\u01A8': 's', '\u03F1': 'g',
    '\uA7FB': 'p', '\u2C6F': 'A',
    '\u5344': 'A', '\u3116': 'O', '\u3125': 'L', '\u5343': 'F', '\u4E59': 'Z',
    '\u15EA': 'D', '\u15F7': 'B', '\u4E05': 'T', '\u3129': 'U', '\u5C71': 'W',
}

ALL_MAP = {}
ALL_MAP.update(CONFUSABLES)
ALL_MAP.update(EXTRA_CONFUSABLES)

ACCENT_MAP = {}
for base, accented in [
    ('a', 'àáâãäåăạǎǟ'), ('e', 'èéêëěẽęệėẻ'), ('i', 'ìíîïĩịĭǐ'),
    ('o', 'òóôõöőọǒȍ'), ('u', 'ùúûüůũụǔ'), ('y', 'ýÿŷ'), ('n', 'ñňń'),
    ('c', 'çčć'), ('s', 'šśşŝ'), ('z', 'žźżẑ'), ('r', 'řŕ'),
    ('d', 'ďđ'), ('t', 'ťŧ'), ('l', 'ľłĺ'), ('g', 'ğǧ'),
    ('h', 'ħȟ'), ('b', 'ƀƄƅ'), ('w', 'ŵ'), ('j', 'ǰ'),
]:
    for ch in accented:
        ACCENT_MAP[ch] = base
        ACCENT_MAP[ch.upper()] = base.upper()

COMBINING_RANGES = [(0x0300, 0x036F), (0x0483, 0x0489), (0xFE00, 0xFE0F),
                    (0x200B, 0x200F), (0x2060, 0x2064)]

# Add circled digits ①-⑳ to confusables if not already there
for i in range(20):
    ch = chr(0x2460 + i)
    if ch not in EXTRA_CONFUSABLES:
        EXTRA_CONFUSABLES[ch] = str(i + 1)
# Negative circled digits ❶-❿
for i in range(10):
    ch = chr(0x2776 + i)
    if ch not in EXTRA_CONFUSABLES:
        EXTRA_CONFUSABLES[ch] = str(i + 1)

# Alternative leet mappings for dictionary fallback (standard → alternative)
ALT_LEET = {
    '4': 'e',  # standard: a, alt: e (Bust4d → Busted)
    '1': 'l',  # standard: i, alt: l
}

# HTML entities commonly found in training data
HTML_ENTITIES = {"&#039;": "'", "&amp;": "&", "&lt;": "<", "&gt;": ">", "&quot;": '"'}


# ============================================================================
# Decoder pipeline
# ============================================================================

def decode_confusables(text: str) -> str:
    """Map Unicode confusables + accents to ASCII, strip emoji/combining marks."""
    result = []
    for ch in text:
        cp = ord(ch)
        # Strip combining marks
        if any(start <= cp <= end for start, end in COMBINING_RANGES):
            continue
        # Strip ZWJ
        if cp == 0x200D:
            continue
        # IMPORTANT: Check confusables BEFORE emoji stripping
        # (squared/negative-squared letters are in emoji range but are mappable)
        if ch in ALL_MAP:
            result.append(ALL_MAP[ch])
            continue
        if ch in ACCENT_MAP:
            result.append(ACCENT_MAP[ch])
            continue
        # Now strip emoji (only for chars NOT in our maps)
        if cp >= 0x1F000 and not ch.isalpha():
            continue
        if 0x2700 <= cp <= 0x27BF:
            continue
        result.append(ch)
    decoded = re.sub(r' +', ' ', ''.join(result)).strip()

    # Detect spaced-out text: if most "words" are single letters, collapse all spaces
    # and re-split using dictionary. E.g. "J u P i t e r J e t S O n" → "Jupiter Jetson"
    words = decoded.split()
    if len(words) >= 4:
        single_letter_count = sum(1 for w in words if len(w) == 1 and w.isalpha())
        if single_letter_count / len(words) > 0.6:
            # Collapse all spaces, then try to re-split into words
            collapsed = ''.join(words)
            decoded = collapsed  # dictionary correction will handle the rest

    return decoded


# Patterns where digits are abbreviations, not leet: 4some, 3way, 1st, 2nd, etc.
DIGIT_ABBREV_RE = re.compile(r'(\d)(some|way|on\d|sum|gether|ever|ward|play|real)', re.IGNORECASE)
ORDINAL_RE = re.compile(r'\b(\d+)(st|nd|rd|th)\b', re.IGNORECASE)
# Date patterns: DD.MM.YY, DD-MM-YY, DD/MM/YY, YYYY-MM-DD, etc.
DATE_RE = re.compile(r'\b\d{1,4}[.\-/]\d{1,2}[.\-/]\d{1,4}\b')


def decode_leet(text: str) -> str:
    """Context-aware leet decode: pure number tokens, ordinals, and digit-abbreviations stay."""
    # Protect digit-abbreviations and ordinals first
    protected = {}
    for pattern in [DIGIT_ABBREV_RE, ORDINAL_RE]:
        for m in pattern.finditer(text):
            key = f'\x00PROT{len(protected)}\x00'
            protected[key] = m.group(0)
            text = text[:m.start()] + key + text[m.end():]

    tokens = re.split(r'(\s+)', text)
    result = []
    for token in tokens:
        if re.match(r'^\d+$', token):
            result.append(token)
        else:
            # Decode leet, but ! only inside words (not trailing punctuation)
            decoded_chars = []
            for i, ch in enumerate(token):
                if ch == '!' and (i == len(token) - 1 or not token[i + 1].isalpha()):
                    decoded_chars.append(ch)  # trailing ! is punctuation
                elif ch in LEET_MAP:
                    decoded_chars.append(LEET_MAP[ch])
                else:
                    decoded_chars.append(ch)
            result.append(''.join(decoded_chars))
    decoded = ''.join(result)

    # Restore protected patterns
    for key, orig in protected.items():
        decoded = decoded.replace(key, orig)

    return decoded


def decode_html(text: str) -> str:
    """Decode HTML entities."""
    for entity, char in HTML_ENTITIES.items():
        text = text.replace(entity, char)
    return text


def decode_full(text: str) -> str:
    """Full decode pipeline: HTML → confusables → leet → restore dates → alt leet dict fix."""
    html_decoded = decode_html(text)
    # Remember date positions in the original (after HTML decode, before confusables)
    date_matches = list(DATE_RE.finditer(html_decoded))

    decoded = decode_confusables(html_decoded)
    decoded = decode_leet(decoded)

    # Restore dates that got mangled by leet decode
    for m in date_matches:
        original_date = m.group(0)
        mangled = ''.join(LEET_MAP.get(ch, ch) for ch in original_date)
        if mangled != original_date and mangled in decoded:
            decoded = decoded.replace(mangled, original_date, 1)

    # Alt-leet fix: for words not in dictionary, try alternative leet (4→e instead of 4→a)
    # Work on the confusable-decoded (pre-leet) text to know which chars were leet
    conf_decoded = decode_confusables(html_decoded)
    decoded = _try_alt_leet_words(conf_decoded, decoded)

    return decoded


def _try_alt_leet_words(pre_leet: str, post_leet: str) -> str:
    """For words where standard leet gives non-dictionary result, try alt mappings."""
    ALT_MAP = {'0': 'o', '1': 'l', '3': 'e', '4': 'e', '5': 's', '7': 't', '8': 'b', '9': 'g', '@': 'a', '$': 's'}

    pre_words = re.split(r'(\s+|[-/,;:!?().&\'"])', pre_leet)
    post_words = re.split(r'(\s+|[-/,;:!?().&\'"])', post_leet)

    if len(pre_words) != len(post_words):
        return post_leet

    result = []
    for pre_w, post_w in zip(pre_words, post_words):
        if not pre_w or not any(c.isalpha() for c in post_w):
            result.append(post_w)
            continue

        post_clean = re.sub(r'[^a-zA-Z]', '', post_w).lower()
        if post_clean in EXTENDED_DICT or len(post_clean) < 3:
            result.append(post_w)
            continue

        # Standard leet didn't produce a dict word — try alt leet
        if any(c.isdigit() or c in '@$!' for c in pre_w):
            alt = ''.join(ALT_MAP.get(ch, ch) for ch in pre_w)
            alt_clean = re.sub(r'[^a-zA-Z]', '', alt).lower()
            if alt_clean in EXTENDED_DICT:
                result.append(alt)
                continue
            # Also try canonical (i/l swaps on alt)
            alt_canon = _canonicalize(alt_clean)
            if alt_canon in CANONICAL_INDEX:
                best = CANONICAL_INDEX[alt_canon][0]
                result.append(_reconstruct(post_w, re.sub(r'[^a-zA-Z]', '', post_w), best))
                continue

        result.append(post_w)

    return ''.join(result)


# ASCII letters that look like other letters when flipped upside-down
UPSIDE_DOWN_ASCII_SWAPS = str.maketrans('bqdpnumwBQDPNUMW', 'qbpdunwmQBPDNUMW')


def apply_upside_down_swaps(text: str) -> str:
    """Swap ASCII letter pairs that change identity when flipped (b↔q, d↔p, n↔u, m↔w)."""
    return text.translate(UPSIDE_DOWN_ASCII_SWAPS)


def decode_reversed(text: str) -> tuple[str, str]:
    """Decode reversed/upside-down text.
    Key: apply ASCII upside-down swaps BEFORE confusables mapping,
    so special Unicode chars (ʍ→w etc.) aren't double-swapped."""
    text = decode_html(text)
    # Step 1: Swap regular ASCII letters that change when flipped
    # (only affects a-z/A-Z, not special Unicode which confusables handles)
    swapped = apply_upside_down_swaps(text)
    # Step 2: Map confusables (overwrites special Unicode chars like ʍ→w)
    mapped = decode_confusables(swapped)
    # Step 3: Reverse
    full_rev = decode_leet(mapped[::-1])
    words_rev = decode_leet(' '.join(w[::-1] for w in mapped.split()))
    return full_rev, words_rev


# ============================================================================
# Dictionary-based word correction (canonical index approach)
# ============================================================================

def _build_extended_dictionary() -> set[str]:
    """Build extended dictionary: base words + NLTK + performer names + clean titles."""
    words = set(WORD_DICTIONARY)

    # NLTK English words (~236k)
    try:
        from nltk.corpus import words as nltk_words
        words.update(w.lower() for w in nltk_words.words() if len(w) >= 3)
    except Exception:
        pass

    # Performer names from vrScraper DB
    DB_PATH = Path('d:/data/vrscraper/vrscraper.db')
    if DB_PATH.exists():
        try:
            import sqlite3
            db = sqlite3.connect(str(DB_PATH))
            for (name,) in db.execute('SELECT Name FROM Stars'):
                for part in name.split():
                    if len(part) >= 3:
                        words.add(part.lower())
            db.close()
        except Exception:
            pass

    # Words from clean training titles (freq >= 2)
    from collections import Counter
    title_words = Counter()
    try:
        with open(DATA_FILE, encoding='utf-8') as f:
            decoder = json.JSONDecoder()
            for line in f:
                line = line.strip()
                if not line:
                    continue
                pos = 0
                while pos < len(line):
                    try:
                        while pos < len(line) and line[pos] in ' \t':
                            pos += 1
                        if pos >= len(line):
                            break
                        entry, end = decoder.raw_decode(line, pos)
                        pos = end
                        if entry.get('is_clean') and entry.get('original'):
                            for w in re.findall(r'[a-zA-Z]+', entry['original']):
                                if len(w) >= 3:
                                    title_words[w.lower()] += 1
                    except json.JSONDecodeError:
                        break
    except FileNotFoundError:
        pass

    words.update(w for w, c in title_words.items() if c >= 2)
    return words


def _canonicalize(word: str) -> str:
    """Replace all ambiguous chars with wildcards for index lookup.
    i, l, I, 1 → * (after leet decode these are all ambiguous)
    v, u → #
    b, g → @
    """
    return word.lower().replace('i', '*').replace('l', '*').replace('v', '#').replace('u', '#').replace('b', '@').replace('g', '@')


def _build_canonical_index(dictionary: set[str]) -> dict[str, list[str]]:
    """Build index: canonical form → list of dictionary words."""
    index = {}
    for word in dictionary:
        canon = _canonicalize(word)
        if canon not in index:
            index[canon] = []
        index[canon].append(word)
    return index


# Build once at import time
print("Building dictionary index...", end=" ", flush=True)
EXTENDED_DICT = _build_extended_dictionary()
CANONICAL_INDEX = _build_canonical_index(EXTENDED_DICT)
print(f"{len(EXTENDED_DICT)} words, {len(CANONICAL_INDEX)} canonical forms")


def dict_correct_word(word: str) -> str | None:
    """Fix a word using canonical index lookup.
    Handles unlimited i/l/I and v/u and b/g swaps in O(1).
    Also tries alternative leet mappings (4→e, 1→l) if standard decode fails."""
    clean = re.sub(r'[^a-zA-Z]', '', word)
    if not clean or len(clean) < 3:
        return None
    if clean.lower() in EXTENDED_DICT:
        return None  # already correct

    # Try canonical index (i/l, v/u, b/g swaps)
    canon = _canonicalize(clean)
    candidates = CANONICAL_INDEX.get(canon)
    if candidates:
        best = candidates[0]
        for c in candidates:
            if len(c) == len(clean):
                best = c
                break
        return _reconstruct(word, clean, best)

    # Try alternative leet on the original word (before standard leet was applied)
    # This handles cases like Bu$t4d where 4→a gives "Bustad" but 4→e gives "Busted"
    # We need the pre-leet version, so try alt mappings on remaining digits
    for alt_char, alt_map in ALT_LEET.items():
        if alt_char not in word:
            continue
        alt_word = word.replace(alt_char, alt_map)
        alt_clean = re.sub(r'[^a-zA-Z]', '', alt_word)
        if alt_clean.lower() in EXTENDED_DICT:
            return _reconstruct(word, clean, alt_clean)
        # Also try canonical on alt
        alt_canon = _canonicalize(alt_clean)
        alt_candidates = CANONICAL_INDEX.get(alt_canon)
        if alt_candidates:
            best = alt_candidates[0]
            for c in alt_candidates:
                if len(c) == len(alt_clean):
                    best = c
                    break
            return _reconstruct(word, clean, best)

    return None


def _reconstruct(original_word: str, clean_alpha: str, corrected_alpha: str) -> str:
    """Reconstruct preserving non-alpha characters and case pattern."""
    result = []
    alpha_idx = 0
    for ch in original_word:
        if ch.isalpha() and alpha_idx < len(corrected_alpha):
            new_ch = corrected_alpha[alpha_idx]
            if ch.isupper():
                new_ch = new_ch.upper()
            else:
                new_ch = new_ch.lower()
            result.append(new_ch)
            alpha_idx += 1
        else:
            result.append(ch)
    return ''.join(result)


def dict_correct_text(text: str) -> str:
    """Apply dictionary correction to all words in text."""
    words = re.split(r'(\s+|[-/,;:!?().&\'"])', text)
    result = []
    for word in words:
        if not word or not any(c.isalpha() for c in word):
            result.append(word)
            continue
        fixed = dict_correct_word(word)
        if fixed:
            result.append(fixed)
        else:
            result.append(word)
    return ''.join(result)


# ============================================================================
# Comparison helpers
# ============================================================================

def norm(text: str) -> str:
    """Normalize for comparison."""
    text = text.lower()
    text = ''.join(ACCENT_MAP.get(ch, ch) for ch in text)
    text = text.replace('\u2019', "'").replace('\u2018', "'")
    text = re.sub(r'\s+', ' ', text).strip()
    return text


def fuzzy_eq(a: str, b: str) -> bool:
    """Match with i/l, v/u, b/g ambiguity."""
    a, b = norm(a), norm(b)
    if a == b:
        return True
    if a.replace('i', 'l') == b.replace('i', 'l'):
        return True
    if a.replace('v', 'u') == b.replace('v', 'u'):
        return True
    if a.replace('b', 'g') == b.replace('b', 'g'):
        return True
    # Combined
    a2 = a.replace('i', 'l').replace('v', 'u')
    b2 = b.replace('i', 'l').replace('v', 'u')
    if a2 == b2:
        return True
    return False


def levenshtein_similarity(s1: str, s2: str) -> float:
    if s1 == s2: return 1.0
    len1, len2 = len(s1), len(s2)
    if len1 == 0 or len2 == 0: return 0.0
    if max(len1, len2) > 500: return 0.0
    matrix = [[0] * (len2 + 1) for _ in range(len1 + 1)]
    for i in range(len1 + 1): matrix[i][0] = i
    for j in range(len2 + 1): matrix[0][j] = j
    for i in range(1, len1 + 1):
        for j in range(1, len2 + 1):
            cost = 0 if s1[i - 1] == s2[j - 1] else 1
            matrix[i][j] = min(matrix[i - 1][j] + 1, matrix[i][j - 1] + 1, matrix[i - 1][j - 1] + cost)
    return 1.0 - matrix[len1][len2] / max(len1, len2)


def word_plausibility(text: str) -> float:
    """Score how plausible a text is based on dictionary word coverage.
    Requires majority of words (not just one) to be real.
    Returns ratio of words that are in dictionary (word count, not char count)."""
    words = [w for w in re.findall(r'[a-zA-Z]+', text.lower()) if len(w) >= 3]
    if not words:
        return 0.0
    dict_words = sum(1 for w in words if w in EXTENDED_DICT)
    return dict_words / len(words)


def has_reversed_chars(text: str) -> bool:
    """Check for Unicode upside-down/mirrored characters."""
    REVERSED = set('ɘɿƨƚɒϱꟼʜʎʍɥꓷǝʇɹʌɐɯɔʅʞɟᴉɓ')
    rev_count = sum(1 for c in text if c in REVERSED)
    alpha_count = sum(1 for c in text if c.isalpha() or c in REVERSED)
    return alpha_count > 3 and (rev_count / alpha_count) > 0.3


def is_reversed_ascii(text: str) -> bool:
    """Detect plain ASCII words written backwards.
    E.g. 'EnotS YcartS' = 'Stone Stracy' reversed per word."""
    words = re.findall(r'[a-zA-Z]+', text)
    if len(words) < 2:
        return False
    # Check if reversed words are more plausible than original
    original_dict_hits = sum(1 for w in words if w.lower() in EXTENDED_DICT)
    reversed_words = [w[::-1] for w in words]
    reversed_dict_hits = sum(1 for w in reversed_words if w.lower() in EXTENDED_DICT)
    # If reversing produces significantly more dictionary hits, it's reversed
    return reversed_dict_hits > original_dict_hits and reversed_dict_hits >= 2


def is_non_latin(text: str) -> bool:
    non_latin = 0
    total = 0
    for ch in text:
        cp = ord(ch)
        if ch.isspace() or ch in '-.,:;!?()[]{}\'"/&@#$%^*+=~`|\\<>_0123456789':
            continue
        total += 1
        if (0x0E00 <= cp <= 0x0E7F or 0x3040 <= cp <= 0x30FF or
            0x4E00 <= cp <= 0x9FFF or 0xAC00 <= cp <= 0xD7AF or
            0x0F00 <= cp <= 0x0FFF or 0x0600 <= cp <= 0x06FF or
            0x0590 <= cp <= 0x05FF or 0x0900 <= cp <= 0x097F):
            non_latin += 1
    return total > 0 and non_latin / total > 0.5


# ============================================================================
# Validation
# ============================================================================

def has_leet_chars(text: str) -> bool:
    """Check if text contains leet-speak characters (digits/symbols used as letters)."""
    return bool(re.search(r'[0-9@$!]', text)) and any(c.isalpha() for c in text)


def looks_like_catalog_code(text: str) -> bool:
    """Detect catalog codes like 'Km1054c', 'GOPJ-034', 'Savr00335vrv18khia'.
    These are alphanumeric sequences where digits are IDs, not leet."""
    stripped = re.sub(r'[^a-zA-Z0-9]', '', text)
    if not stripped:
        return False
    # High ratio of digits to total chars suggests catalog code
    digit_ratio = sum(c.isdigit() for c in stripped) / len(stripped)
    # Catalog codes often: start with letters followed by digits, or are all-caps+digits
    has_code_pattern = bool(re.match(r'^[A-Za-z]{2,}[\d]{2,}', stripped) or
                           re.match(r'^[A-Z0-9\-]+$', text.strip()))
    # Very short text with mixed digits = likely code
    is_short_mixed = len(stripped) <= 15 and 0.2 < digit_ratio < 0.8
    return has_code_pattern or (is_short_mixed and digit_ratio > 0.3)


def is_emoji_only(text: str) -> bool:
    """Check if text is only emoji (no alpha content after stripping)."""
    stripped = decode_confusables(text)
    return not any(c.isalpha() or c.isdigit() for c in stripped)


def validate_entry(entry: dict) -> dict:
    """Validate one entry. Returns {status, method, expected}."""
    original = entry.get('original') or entry.get('title', '')
    if not original:
        return {'status': 'review', 'method': 'no_original', 'expected': ''}

    is_clean = entry.get('is_clean', False)
    haiku_expected = entry.get('normalized')  # None for entries marked clean

    # Emoji-only titles → keep original as-is
    if is_emoji_only(original):
        return {'status': 'verified', 'method': 'emoji_passthrough', 'expected': original}

    # Step 1: Decode
    # For clean entries without Haiku: careful approach
    if is_clean and not haiku_expected:
        decoded_html = decode_html(original)

        # If it has leet chars AND alpha, it might be obfuscated despite being marked clean
        if has_leet_chars(decoded_html) and not re.match(r'^[A-Z0-9\s\-_.]+$', decoded_html):
            # But catalog codes should stay as-is
            if looks_like_catalog_code(decoded_html):
                return {'status': 'verified', 'method': 'clean_catalog_code',
                        'expected': original}
            # Apply full decode (leet + confusables + dict)
            full_decoded = dict_correct_text(decode_full(original))
            if norm(full_decoded) != norm(original):
                # Check plausibility of decoded result
                plaus = word_plausibility(full_decoded)
                if plaus > 0.6:
                    return {'status': 'verified', 'method': 'clean_leet_decoded',
                            'expected': full_decoded}
                return {'status': 'review', 'method': 'clean_but_has_leet',
                        'expected': full_decoded, 'decoder': full_decoded[:120]}

        # Check for reversed chars even in "clean" entries
        if has_reversed_chars(decoded_html):
            rev_full, rev_words = decode_reversed(original)
            if rev_full:
                rev_full = dict_correct_text(rev_full)
            plaus = word_plausibility(rev_full) if rev_full else 0
            if plaus > 0.6:
                return {'status': 'verified', 'method': 'clean_reversed_decoded',
                        'expected': rev_full}

        decoded_conf = decode_confusables(decoded_html)
        dict_corrected = dict_correct_text(decoded_conf)
        original_dict = dict_correct_text(decoded_html)

        original_norm = norm(original)
        dict_norm = norm(dict_corrected)
        original_dict_norm = norm(original_dict)

        # Nothing changed at all → clean
        if dict_norm == original_norm and original_dict_norm == original_norm:
            return {'status': 'verified', 'method': 'clean', 'expected': original}

        # Confusables changed something → check if result is empty (stripped too much)
        if dict_norm and dict_norm != original_norm:
            return {'status': 'verified', 'method': 'confusable_corrected',
                    'expected': dict_corrected}

        # Only dictionary changed something (i/l fix)
        if original_dict_norm != original_norm:
            return {'status': 'verified', 'method': 'dict_corrected',
                    'expected': original_dict}

        return {'status': 'verified', 'method': 'clean', 'expected': original}

    # Full decode for obfuscated entries (with leet)
    decoded = decode_full(original)

    # Step 2: Dictionary correction on decoded output
    dict_corrected = dict_correct_text(decoded)

    # Step 3: Also dictionary-correct the original (for i/l swap only titles)
    original_dict = dict_correct_text(decode_html(original))

    # Step 4: Reversed decode if applicable
    rev_full = rev_words = None
    if has_reversed_chars(original):
        rev_full, rev_words = decode_reversed(original)
        if rev_full:
            rev_full = dict_correct_text(rev_full)
        if rev_words:
            rev_words = dict_correct_text(rev_words)

    # Normalized versions for comparison
    decoded_norm = norm(dict_corrected)
    original_norm = norm(original)
    original_dict_norm = norm(original_dict)

    # Did anything change?
    decode_changed = decoded_norm != original_norm
    dict_changed = original_dict_norm != original_norm

    # -----------------------------------------------------------------------
    # Decision tree
    # -----------------------------------------------------------------------

    # REVERSED TEXT
    if has_reversed_chars(original):
        candidates = [(rev_full, 'reversed_full'), (rev_words, 'reversed_words')]
        for candidate, rev_method in candidates:
            if not candidate:
                continue
            cand_norm = norm(candidate)
            # Reversed decode + Haiku agree
            if haiku_expected and fuzzy_eq(candidate, haiku_expected):
                return {'status': 'verified', 'method': f'{rev_method}_haiku_agree',
                        'expected': haiku_expected}
        # No haiku match — check plausibility
        best = rev_full or rev_words or dict_corrected
        if haiku_expected:
            sim = levenshtein_similarity(norm(best), norm(haiku_expected))
            if sim > 0.85:
                return {'status': 'verified', 'method': 'reversed_haiku_close',
                        'expected': haiku_expected}
            # Plausibility check: if decoder output is more plausible than Haiku,
            # trust decoder (Haiku often garbled reversed text)
            decoder_plaus = word_plausibility(best)
            haiku_plaus = word_plausibility(haiku_expected)
            if decoder_plaus > 0.5 and decoder_plaus > haiku_plaus + 0.2:
                return {'status': 'verified', 'method': 'reversed_decoder_wins',
                        'expected': best}
            if haiku_plaus > 0.5 and haiku_plaus > decoder_plaus + 0.2:
                return {'status': 'verified', 'method': 'reversed_haiku_wins',
                        'expected': haiku_expected}
            return {'status': 'review', 'method': 'reversed_disagree',
                    'expected': haiku_expected, 'decoder': best[:120],
                    'similarity': round(sim, 3),
                    'decoder_plaus': round(decoder_plaus, 2),
                    'haiku_plaus': round(haiku_plaus, 2)}
        # No haiku — trust decoder if plausible
        decoder_plaus = word_plausibility(best)
        if decoder_plaus > 0.5:
            return {'status': 'verified', 'method': 'reversed_decoder_only',
                    'expected': best}
        return {'status': 'review', 'method': 'reversed_no_haiku',
                'expected': best, 'decoder': best[:120]}

    # CHECK FOR REVERSED ASCII (plain words written backwards)
    if not has_reversed_chars(original) and is_reversed_ascii(original):
        words = re.findall(r'[a-zA-Z]+', original)
        reversed_text = re.sub(r'[a-zA-Z]+', lambda m: m.group(0)[::-1], original)
        reversed_text = dict_correct_text(reversed_text)
        if haiku_expected and fuzzy_eq(reversed_text, haiku_expected):
            return {'status': 'verified', 'method': 'reversed_ascii_haiku_agree',
                    'expected': haiku_expected}
        plaus = word_plausibility(reversed_text)
        if plaus > 0.6:
            return {'status': 'verified', 'method': 'reversed_ascii_decoded',
                    'expected': reversed_text}
        if haiku_expected:
            return {'status': 'review', 'method': 'reversed_ascii_disagree',
                    'expected': haiku_expected, 'decoder': reversed_text[:120]}

    # NOTHING CHANGED (neither decoder nor dictionary changed anything)
    if not decode_changed and not dict_changed:
        # Truly clean
        if haiku_expected is None or fuzzy_eq(original, haiku_expected):
            return {'status': 'verified', 'method': 'clean', 'expected': original}
        # Haiku thought it needed changing, but decoder+dict disagree
        sim = levenshtein_similarity(original_norm, norm(haiku_expected))
        if sim > 0.85:
            return {'status': 'verified', 'method': 'clean_haiku_close',
                    'expected': original}
        # Non-Latin originals where Haiku "translated" → our decoder is right to keep original
        if is_non_latin(original):
            return {'status': 'verified', 'method': 'non_latin_passthrough',
                    'expected': original}
        # Titles with metadata tags (#WZVR-SOTF etc.) — Haiku stripped the tag, decoder is right
        if '#' in original and norm(haiku_expected) in norm(original):
            return {'status': 'verified', 'method': 'clean_with_metadata',
                    'expected': original}
        return {'status': 'review', 'method': 'clean_but_haiku_differs',
                'expected': original, 'haiku': haiku_expected,
                'similarity': round(sim, 3)}

    # SOMETHING CHANGED — determine best expected
    # Priority: dict_corrected (decoded + dictionary fixed)
    best_decoded = dict_corrected

    if haiku_expected:
        # GOLD: Decoder+Dict and Haiku agree
        if fuzzy_eq(best_decoded, haiku_expected):
            return {'status': 'verified', 'method': 'decoder_haiku_agree',
                    'expected': haiku_expected}

        # Check if dictionary-corrected original matches Haiku
        if dict_changed and fuzzy_eq(original_dict, haiku_expected):
            return {'status': 'verified', 'method': 'dict_haiku_agree',
                    'expected': haiku_expected}

        # Close match between decoder and Haiku
        sim = levenshtein_similarity(norm(best_decoded), norm(haiku_expected))
        if sim > 0.85:
            # Prefer haiku when close (it had more context)
            return {'status': 'verified', 'method': 'decoder_haiku_close',
                    'expected': haiku_expected}

        # Check if decoder output contains haiku output + more (e.g. Haiku dropped a date)
        # Use character containment: all haiku text appears somewhere in decoder text
        haiku_clean = norm(haiku_expected).replace(' ', '')
        decoder_clean = norm(best_decoded).replace(' ', '')
        if haiku_clean and haiku_clean in decoder_clean and len(decoder_clean) > len(haiku_clean) + 3:
            return {'status': 'verified', 'method': 'decoder_superset_of_haiku',
                    'expected': best_decoded}
        # Also with i/l ambiguity
        haiku_il = haiku_clean.replace('i', 'l')
        decoder_il = decoder_clean.replace('i', 'l')
        if haiku_il and haiku_il in decoder_il and len(decoder_il) > len(haiku_il) + 3:
            return {'status': 'verified', 'method': 'decoder_superset_of_haiku',
                    'expected': best_decoded}

        # Disagreement — plausibility tiebreaker
        decoder_plaus = word_plausibility(best_decoded)
        haiku_plaus = word_plausibility(haiku_expected)
        if decoder_plaus > 0.5 and decoder_plaus > haiku_plaus + 0.2:
            return {'status': 'verified', 'method': 'decoder_more_plausible',
                    'expected': best_decoded}
        if haiku_plaus > 0.5 and haiku_plaus > decoder_plaus + 0.2:
            return {'status': 'verified', 'method': 'haiku_more_plausible',
                    'expected': haiku_expected}
        return {'status': 'review', 'method': 'decoder_haiku_disagree',
                'expected': haiku_expected, 'decoder': best_decoded[:120],
                'similarity': round(sim, 3)}
    else:
        # No Haiku — was marked clean but we found obfuscation
        # Only if dict correction found something (not just leet on numbers etc.)
        if dict_changed and not decode_changed:
            # Only dictionary changed it (i/l swap etc.)
            return {'status': 'verified', 'method': 'dict_corrected',
                    'expected': original_dict}
        if decode_changed:
            # Decoder changed it but no Haiku to confirm
            return {'status': 'review', 'method': 'decoder_changed_no_haiku',
                    'expected': best_decoded, 'decoder': best_decoded[:120]}
        return {'status': 'review', 'method': 'dict_changed_no_haiku',
                'expected': best_decoded, 'decoder': best_decoded[:120]}


# ============================================================================
# Main
# ============================================================================

def load_entries(data_file: Path) -> list[dict]:
    entries = []
    with open(data_file, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            decoder = json.JSONDecoder()
            pos = 0
            while pos < len(line):
                try:
                    while pos < len(line) and line[pos] in ' \t':
                        pos += 1
                    if pos >= len(line):
                        break
                    entry, end = decoder.raw_decode(line, pos)
                    pos = end
                    entries.append(entry)
                except json.JSONDecodeError:
                    break
    return entries


def main():
    parser = argparse.ArgumentParser(description="Unified training data validator v3")
    parser.add_argument("--fix", action="store_true", help="Write validated output file")
    args = parser.parse_args()

    entries = load_entries(DATA_FILE)
    print(f"Loaded: {len(entries)} entries")

    stats = {'verified': 0, 'review': 0}
    method_stats = {}
    issues = []
    validated = []

    for entry in entries:
        result = validate_entry(entry)
        status = result['status']
        method = result['method']
        expected = result.get('expected', '')
        stats[status] += 1
        method_stats[method] = method_stats.get(method, 0) + 1

        original = entry.get('original') or entry.get('title', '')

        validated_entry = {
            'id': entry['id'],
            'original': original,
            'expected': expected,
            'status': status,
            'method': method,
        }

        if status != 'verified':
            validated_entry.update({k: v for k, v in result.items()
                                    if k not in ('status', 'method', 'expected')})
            issues.append(validated_entry)

        validated.append(validated_entry)

    # Print summary
    print()
    print("=" * 60)
    print("VALIDATION SUMMARY")
    print("=" * 60)
    for status, count in sorted(stats.items()):
        pct = count / len(entries) * 100
        print(f"  {status:10s}: {count:6d} ({pct:.1f}%)")
    print()

    print("BY METHOD:")
    for method, count in sorted(method_stats.items(), key=lambda x: -x[1]):
        print(f"  {method:35s}: {count:5d}")
    print()

    # Save issues report
    with open(REPORT_FILE, 'w', encoding='utf-8') as f:
        for issue in issues:
            f.write(json.dumps(issue, ensure_ascii=False) + '\n')
    print(f"Issues: {REPORT_FILE} ({len(issues)} entries)")

    # Write validated output
    if args.fix:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            for v in validated:
                out = {
                    'id': v['id'],
                    'original': v['original'],
                    'expected': v['expected'],
                    'status': v['status'],
                    'method': v['method'],
                }
                f.write(json.dumps(out, ensure_ascii=False) + '\n')

        print(f"\nOutput: {OUTPUT_FILE}")
        print(f"  Total: {len(validated)}")
        print(f"  Verified: {stats['verified']}")
        print(f"  Review: {stats['review']}")


if __name__ == '__main__':
    main()
