#!/usr/bin/env python3
"""Check portable project structure and Unity GUID references; this does not compile Unity."""
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    errors = []
    required = [
        "Assets/Scenes/Starter.unity",
        "Assets/AlphaHex/Scripts/Runtime/GameBootstrap.cs",
        "Assets/AlphaHex/Scripts/Runtime/GameHud.cs",
        "Assets/AlphaHex/Scripts/Runtime/HexWorldView.cs",
        "Assets/AlphaHex/Scripts/Runtime/OrbitCamera.cs",
        "Assets/AlphaHex/Scripts/Runtime/SaveService.cs",
        "Assets/AlphaHex/Scripts/Core/Simulation.cs",
        "Assets/AlphaHex/Editor/ProjectSetup.cs",
        "Packages/manifest.json",
        "ProjectSettings/ProjectVersion.txt",
        "tools/SimulationChecks/SimulationChecks.csproj",
        ".vscode/launch.json", ".vscode/tasks.json", "README.md", "AGENTS.md",
    ]
    for relative in required:
        if not (ROOT / relative).is_file():
            errors.append("Missing " + relative)

    configs = list((ROOT / ".vscode").glob("*.json")) + list((ROOT / "Packages").glob("*.json"))
    configs += list((ROOT / "Assets").rglob("*.asmdef"))
    configs += [ROOT / "global.json", ROOT / "alpha-hex-builder.code-workspace"]
    for path in configs:
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as exc:
            errors.append(str(path.relative_to(ROOT)) + ": " + str(exc))

    guids = {}
    assets = ROOT / "Assets"
    for path in assets.rglob("*"):
        if path.name.endswith(".meta"):
            source = Path(str(path)[:-5])
            if not source.exists():
                errors.append("Orphan meta: " + str(path.relative_to(ROOT)))
            text = path.read_text(encoding="utf-8")
            match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", text, re.MULTILINE)
            if not match:
                errors.append("Invalid GUID: " + str(path.relative_to(ROOT)))
            elif match.group(1) in guids:
                errors.append("Duplicate GUID: " + str(path.relative_to(ROOT)))
            else:
                guids[match.group(1)] = source
        elif not Path(str(path) + ".meta").is_file():
            errors.append("Missing meta: " + str(path.relative_to(ROOT)))

    scene_path = assets / "Scenes/Starter.unity"
    if scene_path.is_file():
        scene = scene_path.read_text(encoding="utf-8")
        matches = re.findall(r"m_Script:.*guid:\s*([0-9a-f]{32})", scene)
        if len(matches) != 1:
            errors.append("Starter scene must reference exactly one bootstrap script.")
        for guid in matches:
            target = guids.get(guid)
            if target is None or target.name != "GameBootstrap.cs":
                errors.append("Starter scene bootstrap GUID does not resolve to GameBootstrap.cs.")

    for path in (assets / "AlphaHex/Scripts/Core").glob("*.cs"):
        if re.search(r"^\s*using\s+(UnityEngine|UnityEditor)", path.read_text(encoding="utf-8"), re.MULTILINE):
            errors.append("Core references Unity: " + path.name)

    if errors:
        for error in errors:
            print("FAIL", error)
        return 1
    print("PASS: required files, JSON configuration, asset metadata, unique GUIDs, starter scene reference, and Core boundary.")
    print("Unity compilation and rendering are separate checks; see docs/QA.md.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
