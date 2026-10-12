#!/usr/bin/env python3
"""Bundle a verified Unity export, or package it with the Windows update launcher.

Export locally with unity_cli.py first. CI can validate the bundled source hashes
against its checkout without requiring an unlicensed Unity Editor build.
"""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import zipfile
from release_common import ROOT, git, sha256, source_hashes, write_json

PLAYER_ITEMS = ("AlphaHexGame.exe", "AlphaHexGame_Data", "UnityPlayer.dll", "MonoBleedingEdge",
                "D3D12", "UnityCrashHandler64.exe", "alphahex-player.json")


def run(command, env=None):
    subprocess.run([str(x) for x in command], cwd=ROOT, check=True, env=env)


def verify_player(player):
    manifest = json.loads((player / "alphahex-player.json").read_text(encoding="utf-8"))
    if manifest["sources"] != source_hashes():
        raise RuntimeError("The Unity export does not match these sources. Rebuild Windows before packaging.")
    if manifest["version"] != (ROOT / "VERSION").read_text().strip():
        raise RuntimeError("The player version differs from VERSION.")
    for name in PLAYER_ITEMS:
        if not (player / name).exists():
            raise RuntimeError("Missing player component: " + name)
    with (player / "AlphaHexGame.exe").open("rb") as stream:
        if stream.read(2) != b"MZ":
            raise RuntimeError("Not a Windows player executable.")
    return manifest


def copy_player(player, stage):
    for name in PLAYER_ITEMS:
        source = player / name
        if source.is_dir():
            shutil.copytree(source, stage / name, ignore=shutil.ignore_patterns("*.pdb", "*.mdb", "*_DoNotShip"))
        else:
            shutil.copy2(source, stage / name)
    # The Mono player already carries runtime notices. Add the package notices
    # captured from the exact Unity package versions used for this export.
    notices = ROOT / "docs/distribution/unity-notices"
    shutil.copytree(notices, stage / "notices/unity")


def zip_tree(folder, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(destination.suffix + ".pending")
    with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for file in sorted(folder.rglob("*")):
            if file.is_file():
                archive.write(file, file.relative_to(folder).as_posix())
    with zipfile.ZipFile(temporary) as archive:
        if archive.testzip():
            raise RuntimeError("ZIP integrity check failed.")
    temporary.replace(destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", type=Path, default=ROOT / "Builds/Windows")
    parser.add_argument("--output", type=Path, default=ROOT / "Artifacts/windows-release")
    parser.add_argument("--bundle-only", action="store_true")
    parser.add_argument("--allow-dirty", action="store_true", help="Local preview only; never use in release CI")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--version", help="Override only for the isolated updater integration fixture")
    args = parser.parse_args()
    if git("status", "--porcelain") and not args.allow_dirty:
        raise RuntimeError("Commit the source changes before making release artifacts.")
    player = args.player.resolve(); output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    manifest = verify_player(player)
    version = args.version or manifest["version"]
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", version):
        raise RuntimeError("Invalid package version.")
    with tempfile.TemporaryDirectory(prefix="alphahex-package-") as temporary:
        stage = Path(temporary) / "stage"; stage.mkdir()
        copy_player(player, stage)
        if args.bundle_only:
            write_json(stage / "player-bundle.json", {
                "source_commit": git("rev-parse", "HEAD"), "source_dirty": bool(git("status", "--porcelain")),
                "files": {p.relative_to(stage).as_posix(): sha256(p) for p in sorted(stage.rglob("*")) if p.is_file()},
            })
            destination = output / "alpha-hex-windows-player.zip"
            zip_tree(stage, destination); print(destination, sha256(destination)); return
        dotnet = str(Path(args.dotnet).resolve()) if Path(args.dotnet).is_file() else args.dotnet
        published = Path(temporary) / "launcher"
        env = os.environ.copy()
        env.setdefault("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
        run([dotnet, "tool", "restore"], env)
        run([dotnet, "publish", "tools/WindowsLauncher/WindowsLauncher.csproj", "--configuration", "Release",
             "--runtime", "win-x64", "--self-contained", "true", "-p:RestoreLockedMode=true",
             "-p:NuGetLockFilePath=packages.win-x64.lock.json",
             "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:Version=" + version,
             "--output", published], env)
        for file in published.iterdir():
            if file.suffix not in (".pdb", ".xml"):
                if file.is_dir(): shutil.copytree(file, stage / file.name)
                else: shutil.copy2(file, stage / file.name)
        shutil.copy2(ROOT / "docs/distribution/UNITY_WINDOWS_PLAYTEST.txt", stage / "READ_ME_FIRST.txt")
        shutil.copy2(ROOT / "docs/distribution/RELEASE_NOTES.md", stage / "RELEASE_NOTES.md")
        shutil.copy2(ROOT / "docs/distribution/VELOPACK_LICENSE.txt", stage / "notices/VELOPACK_LICENSE.txt")
        packages = Path(env.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
        runtime_notice_roots = sorted((packages / "microsoft.netcore.app.runtime.win-x64").glob("*"))
        if not runtime_notice_roots:
            raise RuntimeError("Missing bundled .NET runtime notices.")
        runtime = max(runtime_notice_roots, key=lambda p: tuple(int(n) for n in p.name.split(".")))
        for name in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT"):
            shutil.copy2(runtime / name, stage / ("notices/DOTNET_" + name))
        write_json(stage / "alphahex-build.json", {
            "package_version": version, "player_version": manifest["version"], "unity": manifest["engine"],
            "source_commit": git("rev-parse", "HEAD"), "source_dirty": bool(git("status", "--porcelain")),
            "channel": "win-playtest", "signed": False,
            "files": {p.relative_to(stage).as_posix(): sha256(p) for p in sorted(stage.rglob("*")) if p.is_file()},
        })
        run([dotnet, "tool", "run", "vpk", "--", "[win]", "pack", "--packId", "AlphaHex", "--packVersion", version,
             "--packDir", stage, "--mainExe", "AlphaHex.exe", "--outputDir", output, "--channel", "win-playtest",
             "--runtime", "win-x64", "--packTitle", "Alpha Hex Builder", "--packAuthors", "Mixers28",
             "--releaseNotes", ROOT / "docs/distribution/RELEASE_NOTES.md", "--delta", "BestSpeed",
             "--framework", "vcredist143-x64", "--yes", "--skip-updates"], env)
    assets = sorted(p for p in output.iterdir() if p.is_file() and p.name != "SHA256SUMS.txt")
    (output / "SHA256SUMS.txt").write_text("".join(sha256(p) + "  " + p.name + "\n" for p in assets), encoding="utf-8")
    print("Packaged", version, "in", output)


if __name__ == "__main__":
    main()
