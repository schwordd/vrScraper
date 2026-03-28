@echo off
echo ============================================================
echo  Merge LoRA + Convert to GGUF + Create Ollama Modelfile
echo ============================================================
echo.

call "%~dp0finetuning-env\Scripts\activate.bat"

echo Activated venv: finetuning-env
echo.

python "%~dp0merge_and_convert.py" %*

echo.
pause
