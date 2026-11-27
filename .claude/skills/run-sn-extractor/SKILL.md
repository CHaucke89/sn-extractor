---
name: run-sn-extractor
description: Build, run, drive, screenshot, or test the SN Extractor WinForms app (SNE.exe) on Linux with mono + Xvfb. Use when asked to run the app, check a change to the EXIF reader or the window, or generate test JPEGs with Canon body/lens serials.
---

# Run SN Extractor

SN Extractor is a single-file C# WinForms app (`SNExtractor.cs`) that reads EXIF
body/lens serials (tags `0xA431`/`0xA435`) from JPEGs on a camera card. On Linux it
builds with `mcs` and runs under mono's WinForms on Xvfb. Everything goes through
`.claude/skills/run-sn-extractor/driver.sh`. Paths are relative to the repo root.

## Prerequisites

```bash
sudo apt-get update
sudo apt-get install -y mono-devel libmono-system-windows-forms4.0-cil xdotool x11-apps imagemagick
```

`python3` (stdlib only) is used for fixtures. No exiftool, no .NET SDK.

## Build

```bash
.claude/skills/run-sn-extractor/driver.sh build
```

Writes `/tmp/sne/SNE.exe` (the app) and `/tmp/sne/probe.exe` (the app's
`ExifReader` plus `probe.cs`, no GUI). Override the dir with `SNE_OUT=...`.

## Fake camera card

```bash
.claude/skills/run-sn-extractor/driver.sh fixtures      # -> /tmp/sne/card/DCIM/100CANON
```

`make_fixtures.py` hand-builds the EXIF block so each case is controlled:
big-endian (`MM`) and little-endian (`II`) files, a lens with no serial, a
4-byte serial stored inline in the tag entry, a JPEG with no EXIF, a truncated
file, and a `.CR3` that must be skipped. Add cases there when a PR touches parsing.

## Check the EXIF reader directly (most PRs)

```bash
.claude/skills/run-sn-extractor/driver.sh probe /tmp/sne/card/DCIM/100CANON/*
```

Prints `path<TAB>body<TAB>lens` per file. Expected:

```
IMG_0001.JPG  032021001234  0000c1a2b3
IMG_0003.JPG  142033005678
IMG_0004.jpeg 142033005678  ABC
IMG_0005.JPG / IMG_0006.JPG / IMG_0007.CR3   (blank)
```

## Drive the window

```bash
S=.claude/skills/run-sn-extractor/driver.sh
$S launch /tmp/sne/card            # starts Xvfb :99 + app, prints title "SN Extractor - 6 images"
$S ss /tmp/sne/before.png          # screenshot; open it and look
rm /tmp/sne/card/DCIM/100CANON/IMG_0005.JPG
$S scan                            # clicks Scan, prints "SN Extractor - 5 images"
$S ss /tmp/sne/after.png
$S quit
$S launch                          # no folder arg -> "No SD card found."
$S quit
```

The window title is the app's only text status, so `launch`/`scan`/`title`
print it as the check. Row contents are only visible in the screenshot.

## Human path

On Windows: `build.bat`, or `dotnet publish SNExtractor.csproj -c Release -o out`
(what CI runs). Neither was run here.

## Gotchas

- `mcs` needs `-r:System.Windows.Forms.dll -r:System.Drawing.dll`; Windows `csc`
  (in `build.bat`) pulls them in from its default response file, so `build.bat` has no `-r`.
- The probe build compiles `SNExtractor.cs` with a second `Main`, so it passes
  `-main:Probe`. `ExifReader` is internal, which is why it is compiled in rather than referenced.
- The folder argument (drag a folder onto the exe) is the only way to point the
  app at files here: the container has no removable drives, so a bare launch
  always shows "No SD card found.".
- Rows appear in filesystem order (unsorted on Linux ext4/overlay); Windows/NTFS
  returns them alphabetically. Don't treat the order as a bug from Linux runs.
- There is no window manager, so the window sits at (22,22) with no title bar.
  `scan` clicks relative to the window (`mousemove --window`), not the screen.
- Killed Xvfb processes linger as `<defunct>` zombies in this container, so the
  driver checks the display with `xdotool getdisplaygeometry`, not `pgrep`.
- `Gtk not found ... using built-in colorscheme` in `/tmp/sne/mono.log` is harmless.

## Troubleshooting

- `Could not open display (X-Server required...)` in `mono.log`: Xvfb wasn't up yet
  (a dying Xvfb was mistaken for a live one). Run `quit`, then `launch` again.
- `error CS0234: The type or namespace name 'Forms' does not exist in the namespace 'System.Windows'`: missing the
  `-r:` references above, or `libmono-system-windows-forms4.0-cil` isn't installed.
