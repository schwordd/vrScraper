@echo off
echo ============================================================
echo  QLoRA Finetuning: qwen2.5-7b Title Deobfuscation
echo ============================================================
echo.

call "%~dp0finetuning-env\Scripts\activate.bat"

echo Activated venv: finetuning-env
echo.

python "%~dp0train_lora.py" %*

echo.
pause
