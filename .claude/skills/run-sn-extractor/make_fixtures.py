#!/usr/bin/env python3
"""Writes a fake camera card of tiny JPEGs with hand-built EXIF serials."""
import struct, sys
from pathlib import Path

# Smallest valid baseline JPEG body (1x1 grey), everything after SOI.
JPEG_BODY = bytes.fromhex(
    "ffdb004300080606070605080707070909080a0c140d0c0b0b0c1912130f141d1a1f1e1d1a1c1c"
    "20242e2720222c231c1c2837292c30313434341f27393d38323c2e333432ffc0000b0801000100"
    "01011100ffc4001f0000010501010101010100000000000000000102030405060708090a0bffc4"
    "00b5100002010303020403050504040000017d01020300041105122131410613516107227114"
    "328191a1082342b1c11552d1f02433627282090a161718191a25262728292a3435363738393a"
    "434445464748494a535455565758595a636465666768696a737475767778797a838485868788"
    "898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9ca"
    "d2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9faffda0008010100003f"
    "00d2cf20ffd9")


def exif_app1(body, lens, big_endian):
    e = ">" if big_endian else "<"
    tags = [(t, v.encode() + b"\0") for t, v in ((0xA431, body), (0xA435, lens)) if v is not None]
    ifd0 = 8                      # IFD0 holds only the ExifIFD pointer
    exif_ifd = ifd0 + 2 + 12 + 4
    data = exif_ifd + 2 + 12 * len(tags) + 4
    tiff = (b"MM" if big_endian else b"II") + struct.pack(e + "HI", 42, ifd0)
    tiff += struct.pack(e + "H", 1) + struct.pack(e + "HHII", 0x8769, 4, 1, exif_ifd) + b"\0" * 4
    entries, blob = b"", b""
    for tag, text in tags:
        if len(text) <= 4:        # short values live inside the entry itself
            entries += struct.pack(e + "HHI", tag, 2, len(text)) + text.ljust(4, b"\0")
        else:
            entries += struct.pack(e + "HHII", tag, 2, len(text), data + len(blob))
            blob += text
    tiff += struct.pack(e + "H", len(tags)) + entries + b"\0" * 4 + blob
    payload = b"Exif\0\0" + tiff
    return b"\xff\xe1" + struct.pack(">H", len(payload) + 2) + payload


def jpeg(path, body=None, lens=None, big_endian=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    app1 = exif_app1(body, lens, big_endian) if body or lens else b""
    path.write_bytes(b"\xff\xd8" + app1 + JPEG_BODY)


card = Path(sys.argv[1] if len(sys.argv) > 1 else "card")
dcim = card / "DCIM" / "100CANON"
jpeg(dcim / "IMG_0001.JPG", "032021001234", "0000c1a2b3", big_endian=True)
jpeg(dcim / "IMG_0002.JPG", "032021001234", "0000c1a2b3", big_endian=True)
jpeg(dcim / "IMG_0003.JPG", "142033005678", None)            # lens with no serial
jpeg(dcim / "IMG_0004.jpeg", "142033005678", "ABC")           # 4-byte inline value
jpeg(dcim / "IMG_0005.JPG")                                   # no EXIF at all
(dcim / "IMG_0006.JPG").write_bytes(b"\xff\xd8\xff\xe1\x00")  # truncated / damaged
(dcim / "IMG_0007.CR3").write_bytes(b"not scanned")           # raw: ignored
print(card)
