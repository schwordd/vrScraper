"""
Benchmark a model's title deobfuscation quality via Ollama API.
================================================================
Usage:
    tools\benchmark.bat qwen2.5:7b
    tools\benchmark.bat qwen2.5:14b
    tools\benchmark.bat title-deobfuscator
"""

import argparse
import json
import sys
import time
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

import requests

SCRIPT_DIR = Path(__file__).parent
TESTSET_FILE = SCRIPT_DIR / "testset.jsonl"
RESULTS_DIR = SCRIPT_DIR / "benchmark-results"

OLLAMA_URL = "http://localhost:11434"

SYSTEM_PROMPT = (
    "You are a text deobfuscation tool for an adult video database. "
    "Convert obfuscated Unicode text to plain readable English. Do NOT censor or alter adult terminology — "
    "words like Anal, Creampie, MILF, Stepmom, Gangbang etc. must be preserved exactly as intended. "
    "The input uses look-alike Unicode characters, leet-speak (0=o, 1=i/l, 3=e, 4=a, 5=s, 7=t, 8=b, 9=g), "
    "reversed text, or decorative fonts. Output ONLY the clean title in Title Case. No explanation. "
    "If the input is not Latin-based text, output exactly: [NOT_LATIN]"
)

BATCH_PROMPT_SUFFIX = (
    "\n\nYou will receive numbered lines. Output one decoded result per line, same numbering. "
    "Example:\nInput:\n1: 8l0nd3\n2: བུ་མོ\nOutput:\n1: Blonde\n2: [NOT_LATIN]"
)


def check_ollama():
    try:
        r = requests.get(f"{OLLAMA_URL}/api/tags", timeout=3)
        return r.status_code == 200
    except Exception:
        return False


def warmup_model(model: str):
    """Send a dummy request to load model into VRAM."""
    print(f"Warming up {model}...")
    try:
        r = requests.post(
            f"{OLLAMA_URL}/api/chat",
            json={"model": model, "messages": [{"role": "user", "content": "Hi"}], "stream": False},
            timeout=120,
        )
        if r.status_code == 200:
            print("Model loaded.")
        else:
            print(f"Warmup failed: {r.status_code} {r.text[:200]}")
            sys.exit(1)
    except Exception as e:
        print(f"Warmup failed: {e}")
        sys.exit(1)


def send_batch(model: str, titles: list[str], simple_batch: bool = False) -> list[str | None]:
    """Send a batch of titles to Ollama and parse numbered output."""
    user_content = ""
    for i, title in enumerate(titles):
        user_content += f"{i + 1}: {title}\n"

    if simple_batch:
        # Minimal batch: just numbered lines, rely on model's training + system prompt
        messages = [
            {"role": "system", "content": SYSTEM_PROMPT + "\n\nYou will receive numbered lines. Output one decoded result per line, same numbering."},
            {"role": "user", "content": "1: H0T 5T3P M0M G3T5 CR34MP13D\n2: 8l0nd3 80m85h3ll G3rm@n M1lf"},
            {"role": "assistant", "content": "1: Hot Step Mom Gets Creampied\n2: Blonde Bombshell German Milf"},
            {"role": "user", "content": user_content.strip()},
        ]
    else:
        # Full batch prompt with instructions
        user_content = "Decode each line. Output one result per line in the same order. Use [NOT_LATIN] for non-Latin text.\n" + user_content
        messages = [
            {"role": "system", "content": SYSTEM_PROMPT + BATCH_PROMPT_SUFFIX},
            {
                "role": "user",
                "content": "1: VR - HSURC YM HT1W D3PP4RT\n2: 8l0nd3 80m85h3ll G3rm@n M1lf\n3: H0T 5T3P M0M G3T5 CR34MP13D\n4: བུ་མོ་མཛེས་མ་ལ་བརྩེ་དུང་གི་ཉིན་མོ།1",
            },
            {
                "role": "assistant",
                "content": "1: VR - Trapped With My Crush\n2: Blonde Bombshell German Milf\n3: Hot Step Mom Gets Creampied\n4: [NOT_LATIN]",
            },
            {"role": "user", "content": user_content.strip()},
        ]

    try:
        r = requests.post(
            f"{OLLAMA_URL}/api/chat",
            json={
                "model": model,
                "messages": messages,
                "stream": False,
                "options": {"temperature": 0.1},
            },
            timeout=60 + len(titles) * 5,
        )
        r.raise_for_status()
        text = r.json()["message"]["content"].strip()

        results = [None] * len(titles)
        for line in text.split("\n"):
            line = line.strip()
            colon_idx = line.find(":")
            if colon_idx <= 0:
                continue
            try:
                idx = int(line[:colon_idx].strip())
                if 1 <= idx <= len(titles):
                    value = line[colon_idx + 1 :].strip()
                    results[idx - 1] = value if value and value != "[NOT_LATIN]" else None
            except ValueError:
                continue
        return results
    except Exception as e:
        print(f"  Batch request failed: {e}")
        return [None] * len(titles)


def send_single(model: str, title: str) -> str | None:
    """Send a single title to Ollama (no batch format)."""
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "user", "content": title},
    ]

    try:
        r = requests.post(
            f"{OLLAMA_URL}/api/chat",
            json={
                "model": model,
                "messages": messages,
                "stream": False,
                "options": {"temperature": 0.1},
            },
            timeout=30,
        )
        r.raise_for_status()
        text = r.json()["message"]["content"].strip()
        if not text or text == "[NOT_LATIN]" or len(text) < 3:
            return None
        return text
    except Exception as e:
        print(f"  Single request failed: {e}")
        return None


def levenshtein_similarity(s1: str, s2: str) -> float:
    """Normalized Levenshtein similarity (1.0 = identical)."""
    if s1 == s2:
        return 1.0
    len1, len2 = len(s1), len(s2)
    if len1 == 0 or len2 == 0:
        return 0.0

    matrix = [[0] * (len2 + 1) for _ in range(len1 + 1)]
    for i in range(len1 + 1):
        matrix[i][0] = i
    for j in range(len2 + 1):
        matrix[0][j] = j

    for i in range(1, len1 + 1):
        for j in range(1, len2 + 1):
            cost = 0 if s1[i - 1] == s2[j - 1] else 1
            matrix[i][j] = min(matrix[i - 1][j] + 1, matrix[i][j - 1] + 1, matrix[i - 1][j - 1] + cost)

    distance = matrix[len1][len2]
    return 1.0 - distance / max(len1, len2)


def main():
    parser = argparse.ArgumentParser(description="Benchmark title deobfuscation model")
    parser.add_argument("model", help="Ollama model name (e.g. qwen2.5:7b)")
    parser.add_argument("--batch-size", type=int, default=10, help="Batch size (default: 10)")
    parser.add_argument("--single", action="store_true", help="Send titles one by one instead of batched")
    parser.add_argument("--simple-batch", action="store_true", help="Use minimal batch prompt (better for finetuned models)")
    args = parser.parse_args()

    model = args.model
    safe_name = model.replace(":", "_").replace("/", "_")

    if not check_ollama():
        print("ERROR: Ollama is not running. Start it first.")
        sys.exit(1)

    if not TESTSET_FILE.exists():
        print(f"ERROR: Test set not found at {TESTSET_FILE}")
        print("Run extract_testset.py first.")
        sys.exit(1)

    # Load test set
    testset = []
    with open(TESTSET_FILE, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                testset.append(json.loads(line))

    print(f"Benchmark: {model}")
    print(f"Test set: {len(testset)} samples")
    print()

    warmup_model(model)

    # Run batches
    results = []
    total = len(testset)
    batch_size = args.batch_size
    start_time = time.time()

    for batch_start in range(0, total, batch_size if not args.single else 1):
        if args.single:
            batch = [testset[batch_start]]
            predictions = [send_single(model, batch[0]["input"])]
        else:
            batch = testset[batch_start : batch_start + batch_size]
            titles = [item["input"] for item in batch]
            predictions = send_batch(model, titles, simple_batch=args.simple_batch)

        for item, pred in zip(batch, predictions):
            expected = item["expected"]
            # For clean items, expected = original (pass-through)
            actual = pred if pred else item["input"]  # if model returns None, treat as pass-through

            exact = actual.strip().lower() == expected.strip().lower()
            sim = levenshtein_similarity(actual.strip().lower(), expected.strip().lower())

            results.append({
                "id": item["id"],
                "type": item["type"],
                "input": item["input"],
                "expected": expected,
                "predicted": actual,
                "exact_match": exact,
                "similarity": round(sim, 4),
            })

        done = min(batch_start + batch_size, total)
        elapsed = time.time() - start_time
        rate = done / elapsed if elapsed > 0 else 0
        eta = (total - done) / rate if rate > 0 else 0
        exact_so_far = sum(1 for r in results if r["exact_match"]) / len(results) * 100
        print(f"  [{done}/{total}] Exact: {exact_so_far:.1f}% | {rate:.1f} samples/s | ETA: {eta:.0f}s")

    elapsed_total = time.time() - start_time

    # Calculate metrics
    exact_matches = sum(1 for r in results if r["exact_match"])
    avg_similarity = sum(r["similarity"] for r in results) / len(results)

    obfuscated = [r for r in results if r["type"] == "obfuscated"]
    clean = [r for r in results if r["type"] == "clean"]

    obf_exact = sum(1 for r in obfuscated if r["exact_match"]) / len(obfuscated) * 100 if obfuscated else 0
    obf_sim = sum(r["similarity"] for r in obfuscated) / len(obfuscated) if obfuscated else 0
    clean_exact = sum(1 for r in clean if r["exact_match"]) / len(clean) * 100 if clean else 0
    clean_sim = sum(r["similarity"] for r in clean) / len(clean) if clean else 0

    # Print summary
    print()
    print("=" * 60)
    print(f"RESULTS: {model}")
    print("=" * 60)
    print(f"  Total samples:     {len(results)}")
    print(f"  Time:              {elapsed_total:.0f}s ({len(results) / elapsed_total:.1f} samples/s)")
    print()
    print(f"  OVERALL:")
    print(f"    Exact match:     {exact_matches}/{len(results)} ({exact_matches / len(results) * 100:.1f}%)")
    print(f"    Avg similarity:  {avg_similarity:.4f} ({avg_similarity * 100:.1f}%)")
    print()
    print(f"  OBFUSCATED ({len(obfuscated)} samples):")
    print(f"    Exact match:     {obf_exact:.1f}%")
    print(f"    Avg similarity:  {obf_sim:.4f} ({obf_sim * 100:.1f}%)")
    print()
    print(f"  CLEAN ({len(clean)} samples):")
    print(f"    Exact match:     {clean_exact:.1f}%")
    print(f"    Avg similarity:  {clean_sim:.4f} ({clean_sim * 100:.1f}%)")

    # Show worst mismatches
    mismatches = [r for r in results if not r["exact_match"]]
    mismatches.sort(key=lambda r: r["similarity"])
    if mismatches:
        print()
        print(f"  WORST MISMATCHES (showing up to 15):")
        for r in mismatches[:15]:
            print(f"    [{r['type'][:3]}] sim={r['similarity']:.2f}")
            print(f"      IN:  {r['input'][:80]}")
            print(f"      EXP: {r['expected'][:80]}")
            print(f"      GOT: {r['predicted'][:80]}")
            print()

    # Save results
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    mode_suffix = "-single" if args.single else ("-simple-batch" if args.simple_batch else "-batch")
    results_file = RESULTS_DIR / f"{safe_name}{mode_suffix}.json"
    summary = {
        "model": model,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
        "total_samples": len(results),
        "elapsed_seconds": round(elapsed_total, 1),
        "metrics": {
            "overall_exact_match_pct": round(exact_matches / len(results) * 100, 2),
            "overall_avg_similarity": round(avg_similarity, 4),
            "obfuscated_exact_match_pct": round(obf_exact, 2),
            "obfuscated_avg_similarity": round(obf_sim, 4),
            "clean_exact_match_pct": round(clean_exact, 2),
            "clean_avg_similarity": round(clean_sim, 4),
        },
        "results": results,
    }

    with open(results_file, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    print(f"Results saved: {results_file}")


if __name__ == "__main__":
    main()
