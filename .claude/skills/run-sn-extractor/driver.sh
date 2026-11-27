#!/usr/bin/env bash
# Builds and drives SNE.exe under mono + Xvfb. Run from the repo root.
set -euo pipefail
SKILL=.claude/skills/run-sn-extractor
OUT=${SNE_OUT:-/tmp/sne}
export DISPLAY=${SNE_DISPLAY:-:99}
FORMS="-r:System.Windows.Forms.dll -r:System.Drawing.dll"
mkdir -p "$OUT"

alive() { xdotool getdisplaygeometry >/dev/null 2>&1; }   # pgrep sees zombie Xvfbs
win() { xdotool search --name 'Serial Number Extractor|SN Extractor|No SD card' | head -1; }

case "${1:-}" in
  build)
    mcs -nologo -target:winexe -optimize -win32icon:csi.ico $FORMS -out:"$OUT/SNE.exe" SNExtractor.cs
    mcs -nologo -main:Probe $FORMS -out:"$OUT/probe.exe" SNExtractor.cs "$SKILL/probe.cs"
    echo "$OUT/SNE.exe $OUT/probe.exe" ;;
  fixtures)
    python3 "$SKILL/make_fixtures.py" "${2:-$OUT/card}" ;;
  probe)
    shift; mono "$OUT/probe.exe" "$@" ;;
  launch)
    alive || { Xvfb "$DISPLAY" -screen 0 800x600x24 >/dev/null 2>&1 & sleep 1; }
    mono "$OUT/SNE.exe" ${2:+"$2"} >"$OUT/mono.log" 2>&1 &
    for _ in $(seq 20); do [ -n "$(win)" ] && break; sleep 0.5; done
    sleep 1; xdotool getwindowname "$(win)" ;;
  title)
    xdotool getwindowname "$(win)" ;;
  scan)
    xdotool mousemove --window "$(win)" 40 15 click 1
    sleep "${2:-2}"; xdotool getwindowname "$(win)" ;;
  ss)
    f=${2:-$OUT/sne.png}; xwd -root -silent | convert xwd:- -crop 460x380+0+0 "$f"; echo "$f" ;;
  quit)
    pkill -f "$OUT/SNE.exe" || true; pkill -f "Xvfb $DISPLAY" || true
    while alive; do sleep 0.2; done ;;
  *)
    echo "usage: $0 build|fixtures [dir]|probe <files>|launch [folder]|title|scan [wait]|ss [png]|quit" >&2; exit 1 ;;
esac
