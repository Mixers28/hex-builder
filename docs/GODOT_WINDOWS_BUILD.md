> Historical Godot 0.1 release procedure. The active Unity pipeline is in [WINDOWS_BUILD.md](WINDOWS_BUILD.md).

# Windows friends playtest

The current demo is available as a portable Windows x86_64 [public prerelease](https://github.com/Mixers28/hex-builder/releases/tag/v0.1.0-playtest.1). Send the [Windows ZIP](https://github.com/Mixers28/hex-builder/releases/download/v0.1.0-playtest.1/alpha-hex-first-frontier-windows-x64.zip), which contains an `AlphaHex` folder with `AlphaHex.exe`, `AlphaHex.pck`, a short play guide, engine notices and checksums. Friends extract the whole ZIP and run the EXE. No engine installation is required. Keep the PCK beside the EXE; this follows Godot's [Windows export format](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_windows.html).

The intended playtest platform is Windows 10/11 on Intel/AMD x86_64 with a mouse, keyboard and an OpenGL 3.3-capable graphics driver. These are a target and engine baseline, not measured minimum performance for this game; see Godot's [system requirements](https://docs.godotengine.org/en/4.7/about/system_requirements.html). Native Windows launch, graphics, input and save-path behavior still require a Windows PC test. Running the exported resource pack with the Mac engine does not verify the Windows executable.

The build is an unsigned prototype, so the publisher may appear as unknown. Players need no engine installation, player account or multiplayer service. GitHub Releases hosts the ZIP and its SHA-256 checksum; generated builds remain outside Git history. Start with one friend's launch test, then share the same ZIP with the group. `READ_ME_FIRST.txt` gives the village-to-watchtower route, controls and feedback questions. The long-term armies, animals and seafaring systems are not part of this demo.

## Publisher trust and signing

As of 2026-10-11, `v0.1.0-playtest.1` is unsigned (`codesign/enable=false`). Product/company metadata and a GitHub checksum do not verify the publisher. The user reports a Windows trust warning; its exact text is still needed to distinguish unknown publisher or SmartScreen reputation from a malware detection.

The publisher is an **individual developer**, and the intended audience is the **UK/EU**. Legal residence has not been established by that market choice. Microsoft's Artifact Signing currently accepts individual developers located in the US or Canada; UK/EU eligibility in that service applies to organizations. If the individual publisher is based in the UK/EU, select a different route. [Microsoft eligibility](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart).

| Route | What it provides | Next requirement |
| --- | --- | --- |
| Public code-signing certificate for an individual | A verified publisher signature on the EXE for direct distribution, including GitHub Releases. | Choose a provider accepting the publisher's residence and identity documents, complete verification, and provision the signing key. Certum Standard explicitly supports individuals and is one candidate. |
| Microsoft Store MSIX distribution | Microsoft signs the package after Store certification; Store-installed packages avoid the normal download-reputation warning. | Individual Partner Center onboarding, suitable MSIX packaging, listing and certification. Uploading an EXE or sideloading an MSIX does not itself provide this Store signature. |

Sources: [Certum Standard and other certificate types](https://www.certum.eu/en/code-signing-certificates/), [Certum identity requirements](https://support.certum.eu/en/code-signing-required-documents/), [Microsoft distribution/signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options). No provider, purchase or Store submission has been selected or completed. Confirm the current product, residence eligibility and total price before purchase; this is not a quote.

Signing establishes publisher identity and file integrity. SmartScreen separately assesses reputation, so a new signed EXE can still be warned about; EV signing no longer guarantees an immediate bypass. Use a consistent validated publisher across releases. A self-signed certificate is not a public-distribution solution. [Microsoft SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

After obtaining a signing identity, extend the release pipeline to export, sign and timestamp the final executable/installer, verify the signature on Windows, then compute checksums and package/upload the signed bytes. Private signing material stays outside Git. Because the current game uses a separate PCK, signing the EXE alone does not sign that data file; evaluate a signed installer or MSIX when choosing how to authenticate the complete delivered package. Test the exact downloaded release on Windows and retain the reported warning text and publisher details.

## Rebuild

The engine and templates are pinned to **Godot 4.7.2 stable**. The export preset uses repository-relative custom-template paths so it does not depend on a developer's global template installation. Templates and generated builds stay in ignored folders.

1. Download the standard `Godot_v4.7.2-stable_export_templates.tpz` asset from the [official release](https://github.com/godotengine/godot-builds/releases/tag/4.7.2-stable) into `Artifacts/godot-templates/4.7.2/`. The .NET/Mono archive is not used.
2. From the repository root, run:

```sh
python3 tools/export_windows.py --godot /path/to/Godot
```

If `godot` is on PATH, omit `--godot`. The command verifies the engine version and official template-archive SHA-256, extracts the Windows x86_64 templates, exports the release, checks its PE architecture and resource-pack header, and packages only the six intended delivery files. It verifies ZIP CRCs and byte-for-byte contents. A previous ZIP is replaced only after packaging succeeds. Logs and hashes are written under `Artifacts/windows-export/`.

Output files:

- `Builds/windows-x86_64/AlphaHex/`: the runnable extracted folder.
- `Artifacts/alpha-hex-first-frontier-windows-x64.zip`: the file to share.
- `Artifacts/windows-export/package.txt`: package size, SHA-256 and integrity results.

`--package-only` rebuilds the ZIP from an existing export and the current guide/notices. It does not compile changed game code or retest the executable. Avoid using it after game changes.

The preset keeps the Compatibility renderer, uses compiled GDScript and a separate PCK, and sets the EXE's product name/version. It includes the small internal QA scripts so the exported pack itself can be verified. Developer screenshots, source documents, export-template archives, caches and saved settlements are not copied into the playtest ZIP.

## Verify the delivered pack

Godot can load the actual exported PCK from an otherwise empty working directory. Use an absolute filesystem folder for QA output: the exported `res://` pack is read-only. For example, with `godot` on PATH:

```sh
ALPHA_HEX_QA_OUTPUT=/absolute/path/to/qa-rules godot --headless \
  --main-pack /absolute/path/to/AlphaHex.pck \
  --script res://tests/simulation_checks.gd

ALPHA_HEX_QA_OUTPUT=/absolute/path/to/qa-runtime godot \
  --main-pack /absolute/path/to/AlphaHex.pck -- --qa
```

The rendered QA session starts fresh, pauses simulation except for explicit input checks, and never loads or writes the normal settlement. Rule checks use uniquely named files inside the selected QA folder. Final engine logs must be free of script/runtime errors as well as reporting successful assertions. The initial export test exposed the old harness's assumption that `res://qa-output` was writable; QA now accepts `ALPHA_HEX_QA_OUTPUT` and uses absolute filesystem paths.

Before wider sharing, test on a Windows PC: extract and launch, maximize/restore the window, build and staff a workplace, choose **Menu [M] → Save & Exit**, relaunch, and confirm the same town returns. Also check normal window closure. A failed save should show an error and leave the session open. Continue to the stone/watchtower milestone. The expected Windows save directory is `%APPDATA%\AlphaHexFrontier\`, with the normal engine log under `logs\godot.log`. Record Windows version, GPU, problems and time played; do not describe a successful Mac pack test as a Windows pass.

## Engine notices

`docs/distribution/GODOT_LICENSE.txt` and `GODOT_COPYRIGHT.txt` are the unmodified notices from the Godot `4.7.2-stable` tag, included beside the executable following the engine's [distribution guidance](https://docs.godotengine.org/en/4.7/about/complying_with_licenses.html). All active game visuals are generated by code; system fonts are used without bundling font files. Refresh the engine notices and template checksum together when changing the engine pin.
