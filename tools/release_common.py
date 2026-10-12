"""Small, shared provenance helpers for the Unity export and Windows release."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def sha256(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def source_hashes():
    files = [ROOT / "VERSION"]
    for name in ("Assets", "Packages", "ProjectSettings"):
        files.extend(p for p in (ROOT / name).rglob("*") if p.is_file() and p.name != ".DS_Store")
    return {p.relative_to(ROOT).as_posix(): sha256(p) for p in sorted(files)}


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT).decode().strip()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
