# Serial Number Extractor

[![Build](https://github.com/chaucke89/sn-extractor/actions/workflows/build.yml/badge.svg)](https://github.com/chaucke89/sn-extractor/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/chaucke89/sn-extractor)](https://github.com/chaucke89/sn-extractor/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A tiny Windows app that scans a camera card and lists the Canon body and lens serial number stored in each photo.

## Features

- Scans every removable drive automatically when it opens
- Shows the file, camera serial and lens serial for each JPG (blank means none was stored)
- Drop a folder onto `SNE.exe` to scan that folder instead of the card
- Single exe with no exiftool or other dependencies

## Download

Grab `SNE-<version>.exe` from the [latest release](https://github.com/chaucke89/sn-extractor/releases/latest). It runs on the .NET Framework 4.8 that ships with Windows, so there's nothing to install.

### Code signing

Every release exe is signed with my personal certificate through [Azure Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/) as part of the release workflow, so Windows shows **Christopher Haucke** as the verified publisher. To check a download, right-click `SNE.exe` > **Properties** > **Digital Signatures**.

## Usage

1. Insert the SD card.
2. Double-click `SNE.exe`.

Inserted the card after opening the app? Click **Scan** to look again.

## Build from source

Run `build.bat`, which uses the C# compiler that comes with Windows. Or, with the .NET SDK:

```
dotnet publish SNExtractor.csproj -c Release -o out
```

## License

[MIT](LICENSE)
