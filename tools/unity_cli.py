#!/usr/bin/env python3
"""Find a local Unity Editor and open, test, configure, or build this project.

Use --editor /absolute/path/to/Unity or UNITY_EDITOR to override discovery.
Only command arguments are forwarded; no command string is executed through a shell.
"""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def project_version():
    text = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
    match = re.search(r"^m_EditorVersion:\s*(\S+)", text, flags=re.MULTILINE)
    if not match:
        raise RuntimeError("ProjectVersion.txt has no Unity version.")
    return match.group(1)


def locate_editor(explicit):
    version = project_version()
    paths = []
    if explicit:
        paths.append(Path(explicit).expanduser())
    elif os.environ.get("UNITY_EDITOR"):
        paths.append(Path(os.environ["UNITY_EDITOR"]).expanduser())
    else:
        if sys.platform == "darwin":
            paths.append(Path("/Applications/Unity/Hub/Editor") / version / "Unity.app/Contents/MacOS/Unity")
        elif sys.platform == "win32":
            paths.append(Path(os.environ.get("ProgramFiles", "C:/Program Files")) /
                         "Unity/Hub/Editor" / version / "Editor/Unity.exe")
        else:
            paths.extend([
                Path.home() / "Unity/Hub/Editor" / version / "Editor/Unity",
                Path.home() / "Unity/Hub/Editor" / version / "Editor/Unity.exe",
                Path("/opt/unity/Editor/Unity"),
            ])
        found = shutil.which("Unity") or shutil.which("unity-editor")
        if found:
            paths.append(Path(found))
    for candidate in paths:
        if candidate.is_file():
            return candidate
    checked = "\n  ".join(str(p) for p in paths)
    raise RuntimeError(
        "Unity " + version + " was not found. Install it through Unity Hub, or provide --editor.\n"
        "Checked:\n  " + checked
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["open", "configure", "test", "build-macos", "build-windows", "build-ios"])
    parser.add_argument("--editor", help="Absolute path to the Unity executable (not the .app folder)")
    args = parser.parse_args()
    try:
        editor = locate_editor(args.editor)
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    command = [str(editor), "-projectPath", str(ROOT)]
    if args.command == "open":
        subprocess.Popen(command, cwd=ROOT)
        print("Opening", ROOT, "in Unity", project_version())
        return 0

    artifacts = ROOT / "Artifacts"
    artifacts.mkdir(exist_ok=True)
    log_path = artifacts / (args.command + ".log")
    command.extend(["-batchmode", "-nographics", "-logFile", str(log_path)])
    if args.command == "test":
        # The test runner exits itself. -quit can exit before asynchronous tests finish.
        command.extend(["-runTests", "-testPlatform", "EditMode", "-testResults", str(artifacts / "editmode-results.xml")])
    else:
        methods = {
            "configure": "AlphaHex.Editor.ProjectSetup.EnsureProject",
            "build-macos": "AlphaHex.Editor.ProjectSetup.BuildMac",
            "build-windows": "AlphaHex.Editor.ProjectSetup.BuildWindows",
            "build-ios": "AlphaHex.Editor.ProjectSetup.BuildIOS",
        }
        command.extend(["-quit", "-executeMethod", methods[args.command]])
        targets = {"build-macos": "OSXUniversal", "build-windows": "Win64", "build-ios": "iOS"}
        if args.command in targets:
            command.extend(["-buildTarget", targets[args.command]])
    print("Unity", args.command, "— close this project's interactive Editor first.")
    print("Log:", log_path)
    result = subprocess.run(command, cwd=ROOT)
    if result.returncode:
        print("Unity returned", result.returncode, "— inspect the log above.", file=sys.stderr)
    return result.returncode


if __name__ == "__main__":
    sys.exit(main())
