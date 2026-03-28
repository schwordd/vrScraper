"""
Merge LoRA adapter into base model and convert to GGUF for Ollama.
=================================================================
Run this AFTER train_lora.py has completed successfully.

Usage:
    tools\merge.bat
"""

import argparse
import subprocess
import sys
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

import torch
from peft import PeftModel
from transformers import AutoModelForCausalLM, AutoTokenizer

SCRIPT_DIR = Path(__file__).parent
BASE_MODEL = "Qwen/Qwen2.5-7B-Instruct"
LORA_DIR = SCRIPT_DIR / "lora-output"
MERGED_DIR = SCRIPT_DIR / "merged-model"
GGUF_DIR = SCRIPT_DIR / "gguf-output"


def merge():
    """Merge LoRA adapter into base model (FP16)."""
    print("Loading base model (FP16)...")
    model = AutoModelForCausalLM.from_pretrained(
        BASE_MODEL,
        torch_dtype=torch.float16,
        device_map="cpu",  # merge on CPU to avoid VRAM issues
        trust_remote_code=True,
    )
    tokenizer = AutoTokenizer.from_pretrained(BASE_MODEL, trust_remote_code=True)

    print(f"Loading LoRA adapter from {LORA_DIR}...")
    model = PeftModel.from_pretrained(model, str(LORA_DIR))

    print("Merging weights...")
    model = model.merge_and_unload()

    print(f"Saving merged model to {MERGED_DIR}...")
    MERGED_DIR.mkdir(parents=True, exist_ok=True)
    model.save_pretrained(str(MERGED_DIR), safe_serialization=True)
    tokenizer.save_pretrained(str(MERGED_DIR))
    print(f"Merged model saved ({sum(f.stat().st_size for f in MERGED_DIR.rglob('*') if f.is_file()) / 1024**3:.1f} GB)")


def convert_to_gguf():
    """Convert merged model to GGUF Q4_K_M format using llama.cpp."""
    GGUF_DIR.mkdir(parents=True, exist_ok=True)
    output_file = GGUF_DIR / "title-deobfuscator-q4_k_m.gguf"

    # Check if llama.cpp convert script is available
    # User needs to have llama-cpp-python or llama.cpp installed
    print()
    print("=" * 60)
    print("Converting to GGUF Q4_K_M format...")
    print("=" * 60)

    try:
        # Try using the llama-cpp-python conversion
        result = subprocess.run(
            [
                sys.executable, "-m", "llama_cpp.convert",
                "--outfile", str(output_file),
                "--outtype", "q4_k_m",
                str(MERGED_DIR),
            ],
            capture_output=True,
            text=True,
        )
        if result.returncode == 0:
            print(f"GGUF saved: {output_file}")
            return output_file
    except Exception:
        pass

    # Fallback: tell user to install llama.cpp
    print()
    print("Automatic GGUF conversion not available.")
    print("Install llama.cpp and run manually:")
    print()
    print(f"  # Option A: pip install llama-cpp-python")
    print(f"  python -m llama_cpp.convert --outfile {output_file} --outtype q4_k_m {MERGED_DIR}")
    print()
    print(f"  # Option B: Clone llama.cpp")
    print(f"  git clone https://github.com/ggml-org/llama.cpp")
    print(f"  python llama.cpp/convert_hf_to_gguf.py {MERGED_DIR} --outfile {output_file} --outtype q4_k_m")
    print()
    return None


def create_modelfile(gguf_path: Path | None = None):
    """Create an Ollama Modelfile."""
    if gguf_path is None:
        gguf_path = GGUF_DIR / "title-deobfuscator-q4_k_m.gguf"

    modelfile_path = SCRIPT_DIR / "Modelfile"
    modelfile_content = f"""FROM {gguf_path.resolve()}

PARAMETER temperature 0.1
PARAMETER num_ctx 256
PARAMETER stop "<|im_end|>"

SYSTEM \"\"\"You are a text deobfuscation tool for an adult video database. Convert obfuscated Unicode text to plain readable English. Do NOT censor or alter adult terminology. Output ONLY the clean title in Title Case. No explanation. If the input is not Latin-based text, output exactly: [NOT_LATIN]\"\"\"
"""
    modelfile_path.write_text(modelfile_content, encoding="utf-8")
    print(f"Modelfile written: {modelfile_path}")
    print()
    print("Create the Ollama model with:")
    print(f"  ollama create title-deobfuscator -f {modelfile_path}")
    print()
    print("Then update vrScraper Settings → Ollama Model to: title-deobfuscator")


def main():
    parser = argparse.ArgumentParser(description="Merge LoRA + Convert to GGUF")
    parser.add_argument("--skip-merge", action="store_true", help="Skip merge, only convert")
    parser.add_argument("--skip-gguf", action="store_true", help="Skip GGUF conversion")
    args = parser.parse_args()

    if not args.skip_merge:
        merge()

    gguf_path = None
    if not args.skip_gguf:
        gguf_path = convert_to_gguf()

    create_modelfile(gguf_path)

    print("=" * 60)
    print("ALL DONE!")
    print("=" * 60)


if __name__ == "__main__":
    main()
