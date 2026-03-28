"""
Extract a fixed test set from training data for benchmarking.
Uses the same seed and split logic as train_lora.py to ensure
the test set does NOT overlap with training data.
"""

import json
import random
import sys
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

SCRIPT_DIR = Path(__file__).parent
DATA_FILE = SCRIPT_DIR / "training_data_final.jsonl"
TESTSET_FILE = SCRIPT_DIR / "testset.jsonl"
TEST_SIZE = 500


def load_all_entries(data_file: Path):
    obfuscated = []
    clean = []

    with open(data_file, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            decoder = json.JSONDecoder()
            pos = 0
            while pos < len(line):
                try:
                    while pos < len(line) and line[pos] in " \t":
                        pos += 1
                    if pos >= len(line):
                        break
                    entry, end = decoder.raw_decode(line, pos)
                    pos = end

                    if entry.get("is_clean"):
                        clean.append(entry)
                    elif entry.get("normalized"):
                        obfuscated.append(entry)
                except json.JSONDecodeError:
                    break

    return obfuscated, clean


def main():
    obfuscated, clean = load_all_entries(DATA_FILE)
    print(f"Loaded: {len(obfuscated)} obfuscated, {len(clean)} clean")

    # Use a separate seed (99) so this test set is independent from train split (seed 42)
    random.seed(99)

    # Take 400 obfuscated + 100 clean for the test set
    test_obfuscated = random.sample(obfuscated, min(400, len(obfuscated)))
    test_clean = random.sample(clean, min(100, len(clean)))

    testset = []
    for entry in test_obfuscated:
        testset.append({
            "id": entry["id"],
            "input": entry["original"],
            "expected": entry["normalized"],
            "type": "obfuscated",
        })
    for entry in test_clean:
        testset.append({
            "id": entry["id"],
            "input": entry["original"],
            "expected": entry["original"],  # clean should pass through unchanged
            "type": "clean",
        })

    random.shuffle(testset)

    with open(TESTSET_FILE, "w", encoding="utf-8") as f:
        for item in testset:
            f.write(json.dumps(item, ensure_ascii=False) + "\n")

    print(f"Test set written: {TESTSET_FILE}")
    print(f"  {len(test_obfuscated)} obfuscated + {len(test_clean)} clean = {len(testset)} total")


if __name__ == "__main__":
    main()
