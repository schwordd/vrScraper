@echo off
echo ============================================================
echo  Benchmark: Title Deobfuscation via Ollama
echo ============================================================
echo.

call "%~dp0finetuning-env\Scripts\activate.bat"

if "%~1"=="" (
    echo Usage: benchmark.bat MODEL_NAME
    echo Example: benchmark.bat qwen2.5:7b
    echo          benchmark.bat qwen2.5:14b
    echo          benchmark.bat title-deobfuscator
    pause
    exit /b 1
)

python "%~dp0benchmark.py" %*

echo.
pause
