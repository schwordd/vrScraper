"""Generate english_words.txt from NLTK corpus for embedding in the .NET app."""
import sys
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

from nltk.corpus import words as nltk_words

OUTPUT = Path(__file__).parent.parent / "vrScraper" / "Resources" / "english_words.txt"

all_words = set()
for w in nltk_words.words():
    w = w.lower()
    if len(w) >= 3 and w.isalpha():
        all_words.add(w)

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
with open(OUTPUT, "w", encoding="utf-8") as f:
    for w in sorted(all_words):
        f.write(w + "\n")

print(f"Written {len(all_words)} words to {OUTPUT} ({OUTPUT.stat().st_size / 1024:.0f} KB)")
