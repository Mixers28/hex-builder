# Windows playtest builds and updates

The active game is **Unity 6.3 LTS 6000.3.26f1**, with Windows x64 Mono support. The installer bundles the self-contained .NET 10 update launcher, so players do not need Unity or .NET. Microsoft Visual C++ runtime prerequisites may be downloaded on installation. Target Windows 10/11 x64; actual minimum performance still needs measurement.

Use the **Setup.exe** asset from [GitHub Releases](https://github.com/Mixers28/hex-builder/releases). The portable ZIP is also playable. In-game updates are supported for the installed version: **Menu → Check for updates → Download → Save, update & restart**. The existing Godot `v0.1.0-playtest.1` needs a one-time Unity installation. Its [historical export procedure](GODOT_WINDOWS_BUILD.md) remains reproducible.

The game's menu can import the old Godot save from `%APPDATA%\AlphaHexFrontier\frontier.json`. Unity saves remain outside the install at `%USERPROFILE%\AppData\LocalLow\Project Alpha\Alpha Hex Builder\`. Import is explicit, archives the current Unity town, and does not modify the source file. Back up important playtest saves before changing versions.

## Export and package

1. Install the pinned Unity editor and Windows Mono module through Unity Hub. Close this project's interactive editor.
2. Run the checks, then export the actual player:

```sh
dotnet run --project tools/SimulationChecks/SimulationChecks.csproj --configuration Release
python3 tools/validate_repo.py
python3 tools/unity_cli.py test
python3 tools/unity_cli.py build-windows
```

The export writes `Builds/Windows/alphahex-player.json`, recording hashes of all Unity sources, assets, packages, settings and `VERSION`. Commit the source changes, including generated Unity assets and their `.meta` files. Builds stay ignored.

3. Create the transfer bundle from that export:

```sh
python3 tools/package_windows.py --bundle-only
```

This requires a clean tree and matching source hashes. It copies only the intended Windows player files and notices, excludes debug output, verifies the ZIP, and records every file hash plus the commit. `--allow-dirty` is for local packaging experiments only; the release gate rejects dirty bundles.

4. Create a **draft prerelease** named `v<VERSION>-playtest.<number>`, targeting the full source commit. Attach `Artifacts/windows-release/alpha-hex-windows-player.zip`. Dispatch **Windows playtest release** on that same commit with the draft tag as `release_tag`.

5. The Windows runner checks the bundle against its checkout, runs Core/update checks, executes the actual Windows player, creates the Velopack installer, installs into a disposable directory with spaces, and runs the installed player. A private next-version package then exercises failed-save protection, actual file replacement, relaunch and save continuity. That private fixture is never uploaded to the public feed.

6. Inspect the workflow result and retained logs. Only successful checks upload the Setup, portable ZIP, update package/feed and checksums to the draft. Publish the draft as a prerelease after checking the assets and evidence. Publishing is separate from `git push`; pushing source alone cannot update a compiled player.

Release packaging uses .NET SDK **10.0.401** and a separate Windows dependency lock; refresh the pin and lock together when upgrading. Local packaging after a verified export uses `python3 tools/package_windows.py`. It restores the pinned Velopack tool, publishes the self-contained launcher and runs `vpk '[win]' pack` for channel `win-playtest`. macOS cross-packaging uses the same literal bracket directive. Pass `--dotnet /path/to/dotnet` if needed. The release workflow always packages and tests natively on Windows.

## Update behavior and limits

The public feed is fixed to `Mixers28/hex-builder`, package `AlphaHex`, channel `win-playtest`, including prereleases. Downloads are checksum-verified and version downgrades are rejected. Applying an update saves and pauses the town, verifies the running game process, waits for the player and original launcher to exit, replaces the installation and restarts it. A failed save keeps the game open. Download failures leave the current installation usable. Save files are outside the installation and are migrated by the game, not copied over by the installer.

The initial pipeline distributes full update packages. Players do not manually download and unpack each release, but their connection still downloads the changed version. Velopack can produce smaller delta packages when a previous package is supplied; automatic fetching/retention of that baseline is not enabled in this first release. Do not promise every update will be a tiny download.

The private integration package tests the genuine installed updater against a locally staged verified package. Separate checks exercise actual feed parsing, downloads, corruption and downgrade rejection. It does not prove GitHub availability, every network/proxy configuration or recovery from sudden power loss. Native Windows checks run headless; Mac screenshots do not establish Windows rendering or GPU performance.

## Publisher trust and signing

As of 2026-10-11, `v0.1.0-playtest.1` is unsigned (`codesign/enable=false`). Product/company metadata and a GitHub checksum do not verify the publisher. The user reports a Windows trust warning; its exact text is still needed to distinguish unknown publisher or SmartScreen reputation from a malware detection.

The publisher is an **individual developer**, and the intended audience is the **UK/EU**. Legal residence has not been established by that market choice. Microsoft's Artifact Signing currently accepts individual developers located in the US or Canada; UK/EU eligibility in that service applies to organizations. If the individual publisher is based in the UK/EU, select a different route. [Microsoft eligibility](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart).

| Route | What it provides | Next requirement |
| --- | --- | --- |
| Public code-signing certificate for an individual | A verified publisher signature on the EXE for direct distribution, including GitHub Releases. | Choose a provider accepting the publisher's residence and identity documents, complete verification, and provision the signing key. Certum Standard explicitly supports individuals and is one candidate. |
| Microsoft Store MSIX distribution | Microsoft signs the package after Store certification; Store-installed packages avoid the normal download-reputation warning. | Individual Partner Center onboarding, suitable MSIX packaging, listing and certification. Uploading an EXE or sideloading an MSIX does not itself provide this Store signature. |

Sources: [Certum Standard and other certificate types](https://www.certum.eu/en/code-signing-certificates/), [Certum identity requirements](https://support.certum.eu/en/code-signing-required-documents/), [Microsoft distribution/signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options). No provider, purchase or Store submission has been selected or completed. Confirm the current product, residence eligibility and total price before purchase; this is not a quote.

Signing establishes publisher identity and file integrity. SmartScreen separately assesses reputation, so a new signed EXE can still be warned about; EV signing no longer guarantees an immediate bypass. Use a consistent validated publisher across releases. A self-signed certificate is not a public-distribution solution. [Microsoft SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

After obtaining a signing identity, extend the release pipeline to export, sign and timestamp the final executable/installer, verify the signature on Windows, then compute checksums and package/upload the signed bytes. Private signing material stays outside Git. Sign the Unity executable, launcher and Velopack installer/package through the release signing hooks; signing only the game EXE does not authenticate the whole installation. Test the exact downloaded release on Windows and retain the reported warning text and publisher details.

## Notices and release contents

The package includes the play guide, release notes, source/build manifest, Velopack MIT license, bundled .NET notices and notices from the Unity package versions used by the player. Source availability does not imply a new license grant for the game. Preserve third-party notices and never commit private signing material.

`docs/distribution/unity-notices/` contains unmodified license/notice files from the pinned package cache. Refresh these if package versions change. Engine legal information: [Unity third-party notices](https://unity.com/legal/third-party-notices). Velopack packaging and updater behavior follow its [Windows documentation](https://docs.velopack.io/packaging/windows) and [C# integration guide](https://docs.velopack.io/integrating/csharp).
