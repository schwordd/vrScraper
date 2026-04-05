#!/usr/bin/env python3
"""Embed CHANGELOG.md content into readme.md between markers."""
import re
import sys


def main():
    changelog_path = "CHANGELOG.md"
    readme_path = "readme.md"

    with open(changelog_path, "r", encoding="utf-8") as f:
        changelog = f.read()

    with open(readme_path, "r", encoding="utf-8") as f:
        readme = f.read()

    pattern = r"<!-- CHANGELOG:START -->.*?<!-- CHANGELOG:END -->"
    if not re.search(pattern, readme, flags=re.DOTALL):
        print("ERROR: Changelog markers not found in readme.md")
        print("  Expected: <!-- CHANGELOG:START --> ... <!-- CHANGELOG:END -->")
        sys.exit(1)

    replacement = f"<!-- CHANGELOG:START -->\n{changelog}\n<!-- CHANGELOG:END -->"
    readme = re.sub(pattern, replacement, readme, flags=re.DOTALL)

    with open(readme_path, "w", encoding="utf-8") as f:
        f.write(readme)

    print(f"Changelog embedded in {readme_path}")


if __name__ == "__main__":
    main()
