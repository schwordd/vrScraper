"""
Compare Python decoder vs C# decoder against testset.jsonl.
============================================================
C# is tested via HTTP API (app must be running on port 5001).
Python is tested directly.

Usage:
    python tools/test_decoder_comparison.py                  # full comparison
    python tools/test_decoder_comparison.py --python-only    # only Python decoder
    python tools/test_decoder_comparison.py --csharp-only    # only C# decoder (app must be running)
"""

import argparse
import json
import sys
import time
import urllib.parse
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

import requests

sys.path.insert(0, str(Path(__file__).parent))

SCRIPT_DIR = Path(__file__).parent
TESTSET_FILE = SCRIPT_DIR / "testset.jsonl"
REPORT_FILE = SCRIPT_DIR / "decoder_comparison_report.json"
CS_API = "http://localhost:5001/api/admin/test-normalize"


def load_testset() -> list[dict]:
    entries = []
    with open(TESTSET_FILE, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                entries.append(json.loads(line))
    return entries


def python_decode(title: str) -> str:
    """Run Python decoder on a single title."""
    from validate_training_data import decode_full, dict_correct_text
    decoded = decode_full(title)
    corrected = dict_correct_text(decoded)
    return corrected


def csharp_decode(title: str) -> str | None:
    """Call C# decoder via HTTP API."""
    try:
        r = requests.get(CS_API, params={"title": title}, timeout=10)
        if r.status_code != 200:
            return None
        data = r.json()
        return data.get("decoder", title)
    except Exception as e:
        return None


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


def compare(testset: list[dict], run_python: bool, run_csharp: bool):
    results = []
    py_exact = cs_exact = both_exact = divergent = 0
    py_only = cs_only = neither = 0

    total = len(testset)
    start = time.time()

    for i, entry in enumerate(testset):
        inp = entry["input"]
        expected = entry["expected"]
        entry_type = entry.get("type", "unknown")

        result = {
            "id": entry["id"],
            "input": inp,
            "expected": expected,
            "type": entry_type,
        }

        # Python decoder
        py_out = None
        if run_python:
            py_out = python_decode(inp)
            # For clean entries, if decoder doesn't change → use original
            if py_out == inp and entry_type == "clean":
                py_out = inp
            result["python"] = py_out
            result["python_exact"] = py_out.strip().lower() == expected.strip().lower()
            result["python_sim"] = round(levenshtein_similarity(py_out.strip().lower(), expected.strip().lower()), 4)

        # C# decoder
        cs_out = None
        if run_csharp:
            cs_out = csharp_decode(inp)
            if cs_out is None:
                cs_out = inp  # API error → treat as unchanged
            result["csharp"] = cs_out
            result["csharp_exact"] = cs_out.strip().lower() == expected.strip().lower()
            result["csharp_sim"] = round(levenshtein_similarity(cs_out.strip().lower(), expected.strip().lower()), 4)

        # Comparison
        if run_python and run_csharp:
            py_match = result["python_exact"]
            cs_match = result["csharp_exact"]
            same_output = py_out.strip().lower() == cs_out.strip().lower()

            result["same_output"] = same_output
            result["divergent"] = not same_output

            if py_match and cs_match: both_exact += 1
            elif py_match and not cs_match: py_only += 1
            elif not py_match and cs_match: cs_only += 1
            else: neither += 1

            if not same_output: divergent += 1

        results.append(result)

        if (i + 1) % 50 == 0:
            elapsed = time.time() - start
            rate = (i + 1) / elapsed
            eta = (total - i - 1) / rate if rate > 0 else 0
            print(f"  [{i+1}/{total}] {rate:.1f}/s, ETA: {eta:.0f}s", end="\r")

    elapsed = time.time() - start
    print(f"  [{total}/{total}] Done in {elapsed:.1f}s" + " " * 20)

    return results


def print_report(results: list[dict], run_python: bool, run_csharp: bool):
    total = len(results)
    obf = [r for r in results if r["type"] == "obfuscated"]
    clean = [r for r in results if r["type"] == "clean"]

    print()
    print("=" * 70)
    print("DECODER COMPARISON REPORT")
    print("=" * 70)

    if run_python:
        py_exact = sum(1 for r in results if r.get("python_exact"))
        py_obf_exact = sum(1 for r in obf if r.get("python_exact"))
        py_clean_exact = sum(1 for r in clean if r.get("python_exact"))
        py_sim = sum(r.get("python_sim", 0) for r in results) / total
        print(f"\nPYTHON DECODER:")
        print(f"  Total Exact:  {py_exact}/{total} ({py_exact/total*100:.1f}%)")
        print(f"  Obfuscated:   {py_obf_exact}/{len(obf)} ({py_obf_exact/len(obf)*100:.1f}%)")
        print(f"  Clean:        {py_clean_exact}/{len(clean)} ({py_clean_exact/len(clean)*100:.1f}%)")
        print(f"  Avg Sim:      {py_sim:.4f}")

    if run_csharp:
        cs_exact = sum(1 for r in results if r.get("csharp_exact"))
        cs_obf_exact = sum(1 for r in obf if r.get("csharp_exact"))
        cs_clean_exact = sum(1 for r in clean if r.get("csharp_exact"))
        cs_sim = sum(r.get("csharp_sim", 0) for r in results) / total
        print(f"\nC# DECODER:")
        print(f"  Total Exact:  {cs_exact}/{total} ({cs_exact/total*100:.1f}%)")
        print(f"  Obfuscated:   {cs_obf_exact}/{len(obf)} ({cs_obf_exact/len(obf)*100:.1f}%)")
        print(f"  Clean:        {cs_clean_exact}/{len(clean)} ({cs_clean_exact/len(clean)*100:.1f}%)")
        print(f"  Avg Sim:      {cs_sim:.4f}")

    if run_python and run_csharp:
        divergent = [r for r in results if r.get("divergent")]
        py_wins = [r for r in divergent if r.get("python_exact") and not r.get("csharp_exact")]
        cs_wins = [r for r in divergent if r.get("csharp_exact") and not r.get("python_exact")]
        both_wrong = [r for r in divergent if not r.get("python_exact") and not r.get("csharp_exact")]
        both_right_diff = [r for r in divergent if r.get("python_exact") and r.get("csharp_exact")]

        print(f"\nCOMPARISON:")
        print(f"  Same output:      {total - len(divergent)}/{total}")
        print(f"  Divergent:        {len(divergent)}/{total}")
        print(f"  Python wins:      {len(py_wins)} (Python correct, C# wrong)")
        print(f"  C# wins:          {len(cs_wins)} (C# correct, Python wrong)")
        print(f"  Both wrong (diff):{len(both_wrong)}")
        print(f"  Both right (diff):{len(both_right_diff)}")

        if py_wins:
            print(f"\n  PYTHON WINS (C# should fix) — showing up to 15:")
            for r in py_wins[:15]:
                print(f"    ID {r['id']}")
                print(f"      IN:  {r['input'][:70]}")
                print(f"      EXP: {r['expected'][:70]}")
                print(f"      PY:  {r['python'][:70]}")
                print(f"      CS:  {r['csharp'][:70]}")
                print()

        if cs_wins:
            print(f"\n  C# WINS (Python should fix) — showing up to 15:")
            for r in cs_wins[:15]:
                print(f"    ID {r['id']}")
                print(f"      IN:  {r['input'][:70]}")
                print(f"      EXP: {r['expected'][:70]}")
                print(f"      PY:  {r['python'][:70]}")
                print(f"      CS:  {r['csharp'][:70]}")
                print()

        if both_wrong:
            print(f"\n  BOTH WRONG — showing up to 10:")
            for r in both_wrong[:10]:
                print(f"    ID {r['id']}")
                print(f"      IN:  {r['input'][:70]}")
                print(f"      EXP: {r['expected'][:70]}")
                print(f"      PY:  {r['python'][:70]}")
                print(f"      CS:  {r['csharp'][:70]}")
                print()


def main():
    parser = argparse.ArgumentParser(description="Compare Python and C# decoders")
    parser.add_argument("--python-only", action="store_true")
    parser.add_argument("--csharp-only", action="store_true")
    args = parser.parse_args()

    run_python = not args.csharp_only
    run_csharp = not args.python_only

    testset = load_testset()
    print(f"Loaded {len(testset)} test cases from {TESTSET_FILE}")

    # Check C# API if needed
    if run_csharp:
        try:
            r = requests.get(CS_API, params={"title": "test"}, timeout=3)
            if r.status_code != 200:
                print(f"ERROR: C# API returned {r.status_code}. Is the app running?")
                if not args.csharp_only:
                    print("Falling back to Python-only mode.")
                    run_csharp = False
                else:
                    sys.exit(1)
        except requests.ConnectionError:
            print("ERROR: Cannot connect to C# API at localhost:5001. Is the app running?")
            if not args.csharp_only:
                print("Falling back to Python-only mode.")
                run_csharp = False
            else:
                sys.exit(1)

    if run_python:
        print("Initializing Python decoder...")
        # Trigger dictionary build
        python_decode("test")

    print(f"\nRunning comparison (Python={run_python}, C#={run_csharp})...")
    results = compare(testset, run_python, run_csharp)
    print_report(results, run_python, run_csharp)

    # Save full results
    with open(REPORT_FILE, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2)
    print(f"\nFull results saved: {REPORT_FILE}")


if __name__ == "__main__":
    main()
