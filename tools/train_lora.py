"""
QLoRA Finetuning: qwen2.5:7b für Title-Deobfuskation
=====================================================
Startet das Training mit den Daten aus training_data_final.jsonl.
Erzeugt einen LoRA-Adapter der danach zu GGUF konvertiert und in Ollama geladen wird.

Usage:
    tools\train.bat                     # mit Defaults
    tools\train.bat --epochs 3          # mehr Epochen
    tools\train.bat --batch-size 4      # kleinerer Batch bei VRAM-Problemen
"""

import argparse
import json
import os
import sys
from pathlib import Path

# Force UTF-8 output on Windows
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

import torch
from datasets import Dataset
from peft import LoraConfig, get_peft_model, prepare_model_for_kbit_training, TaskType
from transformers import (
    AutoModelForCausalLM,
    AutoTokenizer,
    BitsAndBytesConfig,
    TrainingArguments,
)
from trl import SFTTrainer, SFTConfig

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
BASE_MODEL = "Qwen/Qwen2.5-7B-Instruct"
SCRIPT_DIR = Path(__file__).parent
DATA_FILE = SCRIPT_DIR / "training_chat.jsonl"
OUTPUT_DIR = SCRIPT_DIR / "lora-output"
MERGED_DIR = SCRIPT_DIR / "merged-model"

SYSTEM_PROMPT = (
    "You are a text deobfuscation tool for an adult video database. "
    "Convert obfuscated Unicode text to plain readable English. Do NOT censor or alter adult terminology — "
    "words like Anal, Creampie, MILF, Stepmom, Gangbang etc. must be preserved exactly as intended. "
    "The input uses look-alike Unicode characters, leet-speak (0=o, 1=i/l, 3=e, 4=a, 5=s, 7=t, 8=b, 9=g), "
    "reversed text, or decorative fonts. Output ONLY the clean title in Title Case. No explanation. "
    "If the input is not Latin-based text, output exactly: [NOT_LATIN]"
)


def load_training_data(data_file: Path, **_kwargs):
    """Load pre-prepared chat-format JSONL (from prepare_training.py).
    Contains single + batch format training pairs."""
    conversations = []
    with open(data_file, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            conversations.append(json.loads(line))

    single = sum(1 for c in conversations if "\n" not in c["messages"][1]["content"])
    batch = len(conversations) - single
    print(f"Training samples: {len(conversations)} total ({single} single + {batch} batch)")
    return conversations


def main():
    parser = argparse.ArgumentParser(description="QLoRA Finetuning for title deobfuscation")
    parser.add_argument("--epochs", type=int, default=2, help="Number of training epochs (default: 2)")
    parser.add_argument("--batch-size", type=int, default=8, help="Per-device batch size (default: 8, reduce if OOM)")
    parser.add_argument("--grad-accum", type=int, default=2, help="Gradient accumulation steps (default: 2)")
    parser.add_argument("--lr", type=float, default=2e-4, help="Learning rate (default: 2e-4)")
    parser.add_argument("--lora-rank", type=int, default=64, help="LoRA rank (default: 64)")
    parser.add_argument("--lora-alpha", type=int, default=128, help="LoRA alpha (default: 128)")
    parser.add_argument("--max-seq-len", type=int, default=512, help="Max sequence length (default: 512)")
    args = parser.parse_args()

    print(f"GPU: {torch.cuda.get_device_name(0)}")
    print(f"VRAM: {torch.cuda.get_device_properties(0).total_memory / 1024**3:.1f} GB")
    print(f"Base model: {BASE_MODEL}")
    print()

    # -----------------------------------------------------------------------
    # 1. Load training data
    # -----------------------------------------------------------------------
    print("=" * 60)
    print("Loading training data...")
    conversations = load_training_data(DATA_FILE)

    # -----------------------------------------------------------------------
    # 2. Load tokenizer
    # -----------------------------------------------------------------------
    print("Loading tokenizer...")
    tokenizer = AutoTokenizer.from_pretrained(BASE_MODEL, trust_remote_code=True)
    if tokenizer.pad_token is None:
        tokenizer.pad_token = tokenizer.eos_token

    # -----------------------------------------------------------------------
    # 3. Load model in 4-bit (QLoRA)
    # -----------------------------------------------------------------------
    print("Loading model in 4-bit quantization...")
    bnb_config = BitsAndBytesConfig(
        load_in_4bit=True,
        bnb_4bit_quant_type="nf4",
        bnb_4bit_compute_dtype=torch.bfloat16,
        bnb_4bit_use_double_quant=True,
    )

    model = AutoModelForCausalLM.from_pretrained(
        BASE_MODEL,
        quantization_config=bnb_config,
        device_map="auto",
        trust_remote_code=True,
        torch_dtype=torch.bfloat16,
    )

    model = prepare_model_for_kbit_training(model)

    # -----------------------------------------------------------------------
    # 4. Apply LoRA
    # -----------------------------------------------------------------------
    print("Applying LoRA adapters...")
    lora_config = LoraConfig(
        r=args.lora_rank,
        lora_alpha=args.lora_alpha,
        target_modules=["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"],
        lora_dropout=0.05,
        bias="none",
        task_type=TaskType.CAUSAL_LM,
    )

    model = get_peft_model(model, lora_config)
    model.print_trainable_parameters()

    # -----------------------------------------------------------------------
    # 5. Prepare dataset
    # -----------------------------------------------------------------------
    print("Preparing dataset...")
    dataset = Dataset.from_list(conversations)

    # Split 95/5 for train/eval
    split = dataset.train_test_split(test_size=0.05, seed=42)
    train_dataset = split["train"]
    eval_dataset = split["test"]
    print(f"Train: {len(train_dataset)}, Eval: {len(eval_dataset)}")

    # -----------------------------------------------------------------------
    # 6. Training
    # -----------------------------------------------------------------------
    print("=" * 60)
    print("Starting training...")
    print(f"  Epochs: {args.epochs}")
    print(f"  Batch size: {args.batch_size} x {args.grad_accum} grad accum = {args.batch_size * args.grad_accum} effective")
    print(f"  Learning rate: {args.lr}")
    print(f"  LoRA rank: {args.lora_rank}, alpha: {args.lora_alpha}")
    print(f"  Max seq len: {args.max_seq_len}")
    print("=" * 60)

    training_args = SFTConfig(
        output_dir=str(OUTPUT_DIR),
        num_train_epochs=args.epochs,
        per_device_train_batch_size=args.batch_size,
        per_device_eval_batch_size=args.batch_size,
        gradient_accumulation_steps=args.grad_accum,
        learning_rate=args.lr,
        weight_decay=0.01,
        warmup_ratio=0.03,
        lr_scheduler_type="cosine",
        logging_steps=25,
        save_strategy="epoch",
        eval_strategy="epoch",
        bf16=True,
        max_length=args.max_seq_len,
        gradient_checkpointing=True,
        gradient_checkpointing_kwargs={"use_reentrant": False},
        report_to="none",
        save_total_limit=2,
        load_best_model_at_end=True,
        metric_for_best_model="eval_loss",
    )

    trainer = SFTTrainer(
        model=model,
        args=training_args,
        train_dataset=train_dataset,
        eval_dataset=eval_dataset,
        processing_class=tokenizer,
    )

    trainer.train()

    # -----------------------------------------------------------------------
    # 7. Save LoRA adapter
    # -----------------------------------------------------------------------
    print("=" * 60)
    print(f"Saving LoRA adapter to {OUTPUT_DIR}...")
    model.save_pretrained(str(OUTPUT_DIR))
    tokenizer.save_pretrained(str(OUTPUT_DIR))

    print()
    print("DONE! Next steps:")
    print(f"  1. Merge adapter:     python tools/merge_and_convert.py")
    print(f"  2. Convert to GGUF:   (see merge_and_convert.py)")
    print(f"  3. Create Ollama model: ollama create title-deobfuscator -f tools/Modelfile")


if __name__ == "__main__":
    main()
