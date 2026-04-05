#!/usr/bin/env python3
"""Generate changelog entry: merge Unreleased notes with auto-generated commit entries."""
import re
import subprocess
import os
from datetime import date


def get_prev_tag():
    """Get the most recent version tag."""
    result = subprocess.run(
        ["git", "tag", "--sort=-version:refname"],
        capture_output=True, text=True
    )
    for line in result.stdout.strip().split("\n"):
        if line.strip().startswith("v"):
            return line.strip()
    return None


def get_commits_since(tag):
    """Get commit messages since a given tag, filtering noise."""
    result = subprocess.run(
        ["git", "log", "--pretty=format:%s", f"{tag}..HEAD", "--no-merges"],
        capture_output=True, text=True
    )
    lines = []
    for line in result.stdout.strip().split("\n"):
        line = line.strip()
        if not line or line == ".":
            continue
        if line.startswith("Update version to"):
            continue
        if line.startswith("Merge"):
            continue
        lines.append(f"- {line}")
    return lines


def main():
    version = os.environ.get("NEXT_VERSION")
    if not version:
        print("ERROR: NEXT_VERSION environment variable not set")
        exit(1)

    today = date.today().isoformat()
    prev_tag = get_prev_tag()

    # Auto-generated entries from commits
    auto_lines = get_commits_since(prev_tag) if prev_tag else []

    # Read current changelog
    changelog_path = "CHANGELOG.md"
    if os.path.exists(changelog_path):
        with open(changelog_path, "r", encoding="utf-8") as f:
            content = f.read()
    else:
        content = "# Changelog\n\n## Unreleased\n"

    # Extract manual entries from Unreleased section
    match = re.search(
        r"^## Unreleased[ \t]*\n(.*?)(?=^## |\Z)",
        content, re.MULTILINE | re.DOTALL
    )
    manual = match.group(1).strip() if match else ""

    # Combine: manual entries first, then auto-generated
    parts = []
    if manual:
        parts.append(manual)
    if auto_lines:
        parts.append("\n".join(auto_lines))
    combined = "\n".join(parts) if parts else "- Maintenance release"

    # Build new version block
    new_block = f"## Unreleased\n\n## v{version} ({today})\n{combined}\n"

    # Replace Unreleased section with new block
    if match:
        content = content[:match.start()] + new_block + "\n" + content[match.end():]
    else:
        content = content.replace("# Changelog", f"# Changelog\n\n{new_block}", 1)

    # Write updated changelog
    with open(changelog_path, "w", encoding="utf-8") as f:
        f.write(content)

    # Write release notes for GitHub Release
    import tempfile
    notes_path = os.path.join(tempfile.gettempdir(), "release_notes.md")
    with open(notes_path, "w", encoding="utf-8") as f:
        f.write(combined + "\n")

    # Also write path to env for GitHub Actions
    github_env = os.environ.get("GITHUB_ENV")
    if github_env:
        with open(github_env, "a") as f:
            f.write(f"RELEASE_NOTES_FILE={notes_path}\n")

    print(f"Changelog updated for v{version}")
    print(f"  Auto entries: {len(auto_lines)}")
    print(f"  Manual entries: {'yes' if manual else 'none'}")
    print(f"  Release notes: {notes_path}")


if __name__ == "__main__":
    main()
