#!/usr/bin/env python3
"""Native Windows release gate. Uses disposable paths, never a player's save."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import zipfile
from release_common import ROOT, git, sha256, write_json
from package_windows import verify_player


def run(*args, timeout=900, env=None):
    subprocess.run([str(x) for x in args], cwd=ROOT, check=True, timeout=timeout, env=env)


def require(condition, message):
    if not condition: raise RuntimeError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True)
    args = parser.parse_args()
    require(sys.platform == "win32", "This gate must run on Windows.")
    version = (ROOT / "VERSION").read_text().strip()
    require(re.fullmatch("v" + re.escape(version) + r"-playtest\.\d+", args.tag), "Tag must match VERSION and be a numbered playtest.")
    release = json.loads(subprocess.check_output(["gh", "release", "view", args.tag, "--json", "isDraft,targetCommitish"]))
    require(release["isDraft"] and release["targetCommitish"] == git("rev-parse", "HEAD"), "Release must be a draft targeting this exact commit.")
    artifacts = ROOT / "Artifacts/native-windows"; artifacts.mkdir(parents=True, exist_ok=False)
    run("gh", "release", "download", args.tag, "--pattern", "alpha-hex-windows-player.zip", "--dir", artifacts)
    player = artifacts / "player"
    with zipfile.ZipFile(artifacts / "alpha-hex-windows-player.zip") as archive:
        require(all(not Path(n).is_absolute() and ".." not in Path(n).parts and "\\" not in n and ":" not in n for n in archive.namelist()), "Unsafe player archive path.")
        require(archive.testzip() is None, "Corrupt player archive.")
        archive.extractall(player)
    bundle = json.loads((player / "player-bundle.json").read_text(encoding="utf-8"))
    require(bundle["source_commit"] == git("rev-parse", "HEAD") and not bundle["source_dirty"], "Player bundle has uncommitted or different sources.")
    files = {p.relative_to(player).as_posix(): sha256(p) for p in sorted(player.rglob("*")) if p.is_file() and p.name != "player-bundle.json"}
    require(files == bundle["files"], "Player bundle file hashes differ."); verify_player(player)
    raw_qa = artifacts / "raw-player-qa"
    run(player / "AlphaHexGame.exe", "-batchmode", "-nographics", "--alpha-qa", "--qa-directory", raw_qa,
        "--qa-fixtures", ROOT / "Assets/AlphaHex/Tests/Fixtures", "-logFile", artifacts / "raw-player.log", timeout=180)
    require(json.loads((raw_qa / "player-result.json").read_text())["passed"], "Native Unity player QA failed.")
    output = ROOT / "Artifacts/windows-release"
    run(sys.executable, "tools/package_windows.py", "--player", player, "--output", output)
    # A second private package exercises the real replacement/relaunch path. It
    # is never uploaded or advertised in the public feed.
    parts = [int(p) for p in version.split(".")]; parts[-1] += 1
    fixture_version = ".".join(str(p) for p in parts)
    fixture = artifacts / "private-update-fixture"
    run(sys.executable, "tools/package_windows.py", "--player", player, "--output", fixture, "--version", fixture_version)
    setup = next(output.glob("*Setup.exe"))
    install = artifacts / "Install With Spaces"
    run(setup, "--silent", "--installto", install, "--log", artifacts / "install.log", timeout=180)
    launcher = install / "current/AlphaHex.exe"
    require(launcher.is_file() and (install / "Update.exe").is_file(), "Installer did not create a launchable installation.")
    installed_qa = artifacts / "installed-player-qa"
    run(launcher, "-batchmode", "-nographics", "--alpha-qa", "--qa-directory", installed_qa,
        "--qa-fixtures", ROOT / "Assets/AlphaHex/Tests/Fixtures", "-logFile", artifacts / "installed-player.log", timeout=180)
    require(json.loads((installed_qa / "player-result.json").read_text())["passed"], "Installed player QA failed.")
    full = next(fixture.glob("*-full.nupkg"))
    # Download/integrity/error handling is independently exercised by the real
    # Velopack self-test. Here place a verified local package in its pending cache.
    feed = json.loads((fixture / "releases.win-playtest.json").read_text())
    asset = next(a for a in feed["Assets"] if a["FileName"] == full.name)
    require(sha256(full).upper() == asset["SHA256"].upper(), "Integration fixture checksum differs.")
    shutil.copy2(full, install / "packages" / full.name)
    update_qa = artifacts / "update-qa"; update_qa.mkdir()
    env = os.environ.copy()
    env["ALPHA_HEX_QA_UPDATE_DIRECTORY"] = str(update_qa)
    env["ALPHA_HEX_QA_UPDATE_VERSION"] = fixture_version
    process = subprocess.Popen([str(launcher)], env=env, cwd=install / "current")
    try:
        deadline = time.monotonic() + 240
        while time.monotonic() < deadline and not (update_qa / "update-result.json").exists():
            time.sleep(.5)
        require((update_qa / "update-result.json").exists(), "Installer update/restart timed out; inspect update-qa logs and requests.")
        result = json.loads((update_qa / "update-result.json").read_text())
        require(result.get("passed") and result.get("savePreserved") and result.get("restarted"), "Updated player lost its settlement.")
        process.wait(timeout=30)
    finally:
        if process.poll() is None: process.kill()
    write_json(artifacts / "verification.json", {
        "passed": True, "commit": git("rev-parse", "HEAD"), "version": version,
        "nativePlayer": True, "installedPlayer": True, "failedSaveBlocksUpdate": True,
        "nativeReplacementAndRestart": True, "savePreserved": True,
        "renderedWindowsTest": False, "signed": False,
    })
    print("PASS native Windows player, installer, failed-save protection, real package replacement, relaunch and save continuity.")
    # Publishing remains a separate reviewed action. This workflow can only add
    # assets to the draft whose commit and source hashes passed above.
    assets = [p for p in output.iterdir() if p.is_file()]
    run("gh", "release", "upload", args.tag, *assets, artifacts / "verification.json", "--clobber")


if __name__ == "__main__":
    main()
