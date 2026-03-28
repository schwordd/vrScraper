"""
Prepare training data from validated v2 file.
Generates chat-format JSONL for SFT training with:
  - Single title pairs (70%)
  - Batch format pairs à 10 (30%)

Usage:
    python tools/prepare_training.py
"""

import json
import random
import sys
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

SCRIPT_DIR = Path(__file__).parent
V2_FILE = SCRIPT_DIR / "training_data_v2.jsonl"
OUTPUT_FILE = SCRIPT_DIR / "training_chat.jsonl"

SYSTEM_PROMPT = (
    "You are a text deobfuscation tool for an adult video database. "
    "Convert obfuscated Unicode text to plain readable English. Do NOT censor or alter adult terminology — "
    "words like Anal, Creampie, MILF, Stepmom, Gangbang etc. must be preserved exactly as intended. "
    "The input uses look-alike Unicode characters, leet-speak (0=o, 1=i/l, 3=e, 4=a, 5=s, 7=t, 8=b, 9=g), "
    "reversed text, or decorative fonts. Output ONLY the clean title in Title Case. No explanation. "
    "If the input is not Latin-based text, output exactly: [NOT_LATIN]"
)

BATCH_SYSTEM_PROMPT = SYSTEM_PROMPT + (
    "\n\nYou will receive numbered lines. Output one decoded result per line, same numbering."
)


def load_verified(v2_file: Path) -> tuple[list[dict], list[dict]]:
    """Load verified entries, split into changed and clean."""
    changed = []  # original != expected
    clean = []    # original == expected (passthrough)

    with open(v2_file, encoding='utf-8') as f:
        for line in f:
            e = json.loads(line.strip())
            if e['status'] != 'verified':
                continue
            if not e['original'] or not e['expected']:
                continue
            if e['original'] == e['expected']:
                clean.append(e)
            else:
                changed.append(e)

    return changed, clean


def make_single_pair(entry: dict) -> dict:
    """Create a single-title chat training pair."""
    return {
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": entry["original"]},
            {"role": "assistant", "content": entry["expected"]},
        ]
    }


def make_batch_pair(entries: list[dict]) -> dict:
    """Create a batch chat training pair from a group of entries."""
    user_lines = []
    assistant_lines = []
    for i, e in enumerate(entries):
        user_lines.append(f"{i + 1}: {e['original']}")
        assistant_lines.append(f"{i + 1}: {e['expected']}")

    return {
        "messages": [
            {"role": "system", "content": BATCH_SYSTEM_PROMPT},
            {"role": "user", "content": "\n".join(user_lines)},
            {"role": "assistant", "content": "\n".join(assistant_lines)},
        ]
    }


def main():
    random.seed(42)

    changed, clean = load_verified(V2_FILE)
    print(f"Loaded: {len(changed)} changed + {len(clean)} clean = {len(changed) + len(clean)} verified")

    # -----------------------------------------------------------------------
    # Build training samples
    # -----------------------------------------------------------------------

    # 1. ALL changed entries as single pairs (core training data)
    single_pairs = [make_single_pair(e) for e in changed]

    # 2. Sample of clean entries as single pairs (teach passthrough)
    clean_sample_size = min(len(clean), len(changed) // 5)  # ~20% of changed count
    clean_sample = random.sample(clean, clean_sample_size)
    single_clean = [make_single_pair(e) for e in clean_sample]

    # 3. Batch pairs with variable sizes (2-10) from changed entries
    random.shuffle(changed)
    batch_pairs = []
    idx = 0
    while idx < len(changed) - 1:
        batch_size = random.randint(2, 10)
        batch = changed[idx:idx + batch_size]
        if len(batch) >= 2:
            batch_pairs.append(make_batch_pair(batch))
        idx += batch_size

    # 4. Mixed batches with variable sizes (some changed + some clean)
    all_for_mixed = changed + clean_sample
    random.shuffle(all_for_mixed)
    mixed_batch_pairs = []
    idx = 0
    while idx < len(all_for_mixed) - 1:
        batch_size = random.randint(2, 10)
        batch = all_for_mixed[idx:idx + batch_size]
        if len(batch) >= 2:
            mixed_batch_pairs.append(make_batch_pair(batch))
        idx += batch_size

    # Combine: all singles + all batches
    all_samples = single_pairs + single_clean + batch_pairs + mixed_batch_pairs
    random.shuffle(all_samples)

    print(f"\nTraining samples generated:")
    print(f"  Single (changed):  {len(single_pairs)}")
    print(f"  Single (clean):    {len(single_clean)}")
    print(f"  Batch (changed):   {len(batch_pairs)}")
    print(f"  Batch (mixed):     {len(mixed_batch_pairs)}")
    print(f"  Total:             {len(all_samples)}")

    # Calculate ratio
    total_single = len(single_pairs) + len(single_clean)
    total_batch = len(batch_pairs) + len(mixed_batch_pairs)
    print(f"\n  Single/Batch ratio: {total_single}/{total_batch} "
          f"({total_single / len(all_samples) * 100:.0f}%/{total_batch / len(all_samples) * 100:.0f}%)")

    # Write output
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        for sample in all_samples:
            f.write(json.dumps(sample, ensure_ascii=False) + '\n')

    size_mb = OUTPUT_FILE.stat().st_size / 1024 / 1024
    print(f"\nOutput: {OUTPUT_FILE} ({size_mb:.1f} MB, {len(all_samples)} samples)")


if __name__ == '__main__':
    main()
