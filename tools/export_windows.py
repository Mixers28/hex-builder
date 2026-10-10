#!/usr/bin/env python3
"""Export and package the pinned Godot project for a Windows friends playtest."""

import argparse
import hashlib
import re
import shutil
import struct
import subprocess
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VERSION = "4.7.2"
TEMPLATE_SHA256 = "f298490b8d44d934be425a5a65a51bf15f422428b229a06a6e11d9ffea248011"


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default="godot", help="Godot 4.7.2 executable")
    parser.add_argument("--package-only", action="store_true", help="Package an existing export")
    args = parser.parse_args()
    destination = ROOT / "Builds/windows-x86_64/AlphaHex"
    evidence = ROOT / "Artifacts/windows-export"
    destination.mkdir(parents=True, exist_ok=True)
    evidence.mkdir(parents=True, exist_ok=True)

    if not args.package_only:
        engine = shutil.which(args.godot)
        if not engine:
            parser.error("Godot not found; supply its executable with --godot.")
        version = subprocess.check_output([engine, "--headless", "--version"], text=True).strip()
        if not version.startswith(VERSION + ".stable."):
            parser.error(f"Expected Godot {VERSION}.stable, found {version}")
        templates = ROOT / "Artifacts/godot-templates" / VERSION
        archive = templates / f"Godot_v{VERSION}-stable_export_templates.tpz"
        if not archive.is_file():
            parser.error("Missing official export templates; see docs/WINDOWS_BUILD.md.")
        if sha256(archive) != TEMPLATE_SHA256:
            parser.error("Official export-template archive checksum does not match.")
        with zipfile.ZipFile(archive) as package:
            if package.read("templates/version.txt").decode().strip() != VERSION + ".stable":
                parser.error("Export-template version does not match the engine pin.")
            for kind in ("debug", "release"):
                for suffix in ("", "_console"):
                    name = f"windows_{kind}_x86_64{suffix}.exe"
                    (templates / name).write_bytes(package.read("templates/" + name))
        result = subprocess.run(
            [engine, "--headless", "--path", str(ROOT / "godot"),
             "--export-release", "Windows Desktop", str(destination / "AlphaHex.exe"),
             "--log-file", str(evidence / "export.log")],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, check=False,
        )
        (evidence / "export-console.log").write_text(result.stdout)
        if result.returncode or re.search(r"(?:SCRIPT )?ERROR:", result.stdout):
            raise SystemExit("Windows export failed; see Artifacts/windows-export/export-console.log")

    # Reject the wrong platform or a missing resource pack before sharing.
    executable = destination / "AlphaHex.exe"
    with executable.open("rb") as stream:
        header = stream.read(64)
        if header[:2] != b"MZ":
            raise SystemExit("Output is not a Windows executable.")
        stream.seek(struct.unpack_from("<I", header, 60)[0])
        pe = stream.read(96)
    if pe[:4] != b"PE\0\0" or struct.unpack_from("<H", pe, 4)[0] != 0x8664:
        raise SystemExit("Output is not a Windows x86_64 PE executable.")
    if struct.unpack_from("<H", pe, 24)[0] != 0x20B or struct.unpack_from("<H", pe, 92)[0] != 2:
        raise SystemExit("Expected a 64-bit Windows GUI executable.")
    pack = destination / "AlphaHex.pck"
    with pack.open("rb") as stream:
        if stream.read(4) != b"GDPC":
            raise SystemExit("Exported Godot resource pack is missing or invalid.")

    documents = {
        "WINDOWS_PLAYTEST.txt": "READ_ME_FIRST.txt",
        "GODOT_LICENSE.txt": "GODOT_LICENSE.txt",
        "GODOT_COPYRIGHT.txt": "GODOT_COPYRIGHT.txt",
    }
    for source, target in documents.items():
        shutil.copyfile(ROOT / "docs/distribution" / source, destination / target)
    names = ["AlphaHex.exe", "AlphaHex.pck", *documents.values()]
    sums = "".join(f"{sha256(destination / name)}  {name}\n" for name in names)
    (destination / "SHA256SUMS.txt").write_text(sums)
    names.append("SHA256SUMS.txt")
    archive = ROOT / "Artifacts/alpha-hex-first-frontier-windows-x64.zip"
    staging = archive.with_suffix(".zip.new")
    with zipfile.ZipFile(staging, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as package:
        for name in names:
            package.write(destination / name, "AlphaHex/" + name)
    with zipfile.ZipFile(staging) as package:
        if package.testzip() is not None:
            raise SystemExit("ZIP integrity check failed.")
        for name in names:
            if package.read("AlphaHex/" + name) != (destination / name).read_bytes():
                raise SystemExit(f"Packaged file mismatch: {name}")
    staging.replace(archive)
    report = (f"Windows x86_64 release packaged: {archive.name}\n"
              f"Files: {len(names)}\nSize: {archive.stat().st_size} bytes\n"
              f"SHA-256: {sha256(archive)}\n"
              "PE architecture, pack magic, archive CRC and packaged bytes verified.\n"
              "This packaging check does not establish native Windows runtime compatibility.\n")
    (evidence / "package.txt").write_text(report)
    print(report, end="")


if __name__ == "__main__":
    main()
