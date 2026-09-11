#!/usr/bin/env python3
"""Build src/launcher.ico — the EXE icon windres embeds as IDI_ICON1.

Why this exists rather than "export an .ico from a paint program":

An .ico is a container, and the shell picks ONE entry out of it. The entry it
can pick depends on how old the shell is:

  * Vista and later can decode a PNG-compressed entry, which is how a single
    256x256 image can be the whole file.
  * Windows 2000/XP need a classic BMP (DIB) entry. 32bpp with an alpha
    channel is fine there.
  * Windows 95/98/ME need a BMP entry too, and render it through the AND mask
    rather than through alpha. A 256-colour entry is the safe one: the 9x
    shell is routinely running at 8 or 16 bit colour, where it would have to
    dither a 32bpp icon anyway.

The file this replaces held exactly one entry: 256x256, PNG-compressed. That
is a perfectly good icon on the machine it was exported from and nothing at
all on the machines the legacy launcher exists for -- the resource was in the
EXE the whole time, and XP and 9x both fell back to the generic application
icon because there was no entry either of them could read.

Pillow's own ICO writer only emits 32bpp BMP (and PNG at 256), so the
container is written out by hand here.

Usage (from LANCommander.Launcher.Legacy/):
    python tools/generate-app-icon.py
"""

import io
import os
import struct
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

# The artwork. Shared with the Avalonia launcher so the two cannot drift; it
# is the same 256x256 image that used to be the .ico's only entry.
SOURCE = os.path.join(ROOT, "..", "LANCommander.Launcher", "Assets", "icon.png")
OUTPUT = os.path.join(ROOT, "src", "launcher.ico")

# 32bpp BMP entries. 16/32/48 are the sizes the shell actually asks for
# (small icon, large icon, "Large Icons" view); 64 and 128 keep Alt+Tab and
# the Vista+ extra-large view sharp without a 256x256 DIB's quarter megabyte.
BMP32_SIZES = (16, 24, 32, 48, 64, 128)

# 256-colour entries, for Win9x. Only the sizes 9x asks for -- it has no
# extra-large icon view.
BMP8_SIZES = (16, 24, 32, 48)

# PNG-compressed, for Vista and later.
PNG_SIZES = (256,)

# Alpha at or above this counts as opaque in the AND mask. The artwork is
# effectively binary already (475 of 65536 pixels are partially transparent),
# so the threshold only decides what happens on the antialiased rim.
ALPHA_THRESHOLD = 128


def scaled(src, size):
    """`src` resampled to size x size with a good filter."""
    if src.size == (size, size):
        return src.copy()
    return src.resize((size, size), Image.LANCZOS)


def and_mask(img):
    """The 1bpp transparency mask, bottom-up, rows padded to 4 bytes.

    Set bits are transparent. This is what a pre-Vista shell honours; the
    alpha channel of a 32bpp entry is ignored on 9x and only consulted on
    2000/XP.
    """
    w, h = img.size
    alpha = img.getchannel("A")
    row_bytes = ((w + 31) // 32) * 4

    out = bytearray()
    for y in range(h - 1, -1, -1):
        row = bytearray(row_bytes)
        for x in range(w):
            if alpha.getpixel((x, y)) < ALPHA_THRESHOLD:
                row[x // 8] |= 0x80 >> (x % 8)
        out += row
    return bytes(out)


def dib_32(img):
    """A 32bpp BGRA icon image: header, XOR bitmap, AND mask."""
    w, h = img.size

    # biHeight is doubled: the DIB covers the colour bitmap and the mask.
    header = struct.pack("<IiiHHIIiiII",
                         40,        # biSize
                         w,         # biWidth
                         h * 2,     # biHeight
                         1,         # biPlanes
                         32,        # biBitCount
                         0,         # biCompression (BI_RGB)
                         0,         # biSizeImage
                         0, 0,      # pixels-per-metre
                         0, 0)      # palette counts

    px = img.load()
    xor = bytearray()
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[x, y]
            xor += bytes((b, g, r, a))

    return header + bytes(xor) + and_mask(img)


def dib_8(img):
    """A 256-colour icon image: header, palette, XOR bitmap, AND mask."""
    w, h = img.size

    # Quantise the colour, but from the opaque pixels only. Letting the fully
    # transparent ones vote drags palette entries towards whatever RGB happens
    # to sit under alpha 0, which is wasted on pixels the mask hides anyway.
    opaque = Image.new("RGB", (w, h), (0, 0, 0))
    opaque.paste(img.convert("RGB"), (0, 0), img.getchannel("A").point(
        lambda v: 255 if v >= ALPHA_THRESHOLD else 0))

    pal_img = opaque.quantize(colors=256, method=Image.MEDIANCUT, dither=Image.NONE)

    palette = pal_img.getpalette() or []
    palette += [0] * (768 - len(palette))

    header = struct.pack("<IiiHHIIiiII",
                         40, w, h * 2, 1, 8, 0, 0, 0, 0,
                         256,   # biClrUsed
                         0)     # biClrImportant

    # RGBQUAD is BGR0.
    pal_bytes = bytearray()
    for i in range(256):
        r, g, b = palette[i * 3:i * 3 + 3]
        pal_bytes += bytes((b, g, r, 0))

    px = pal_img.load()
    row_bytes = ((w + 3) // 4) * 4
    xor = bytearray()
    for y in range(h - 1, -1, -1):
        row = bytearray(row_bytes)
        for x in range(w):
            row[x] = px[x, y]
        xor += row

    return header + bytes(pal_bytes) + bytes(xor) + and_mask(img)


def png_image(img):
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def main():
    if not os.path.exists(SOURCE):
        sys.exit("Source artwork not found: %s" % SOURCE)

    src = Image.open(SOURCE).convert("RGBA")

    # (width, height, colour count, planes, bit count, payload).
    #
    # Ordered smallest-first within each format and 8bpp before 32bpp, which
    # is only cosmetic -- every shell scores the entries itself rather than
    # taking the first one -- but makes the file readable in a hex dump.
    entries = []

    for size in BMP8_SIZES:
        img = scaled(src, size)
        # bColorCount is a byte, so 256 colours is spelled 0.
        entries.append((size, size, 0, 1, 8, dib_8(img)))

    for size in BMP32_SIZES:
        img = scaled(src, size)
        entries.append((size, size, 0, 1, 32, dib_32(img)))

    for size in PNG_SIZES:
        img = scaled(src, size)
        # 256 is spelled 0 in the byte-wide width/height fields.
        entries.append((size % 256, size % 256, 0, 1, 32, png_image(img)))

    out = bytearray()
    out += struct.pack("<HHH", 0, 1, len(entries))

    offset = 6 + 16 * len(entries)
    for w, h, colors, planes, bits, data in entries:
        out += struct.pack("<BBBBHHII", w, h, colors, 0, planes, bits,
                           len(data), offset)
        offset += len(data)

    for _, _, _, _, _, data in entries:
        out += data

    with open(OUTPUT, "wb") as f:
        f.write(bytes(out))

    print("Wrote %s (%d entries, %d bytes)" %
          (os.path.relpath(OUTPUT, ROOT), len(entries), len(out)))
    for w, h, _, _, bits, data in entries:
        kind = "PNG" if data[:8] == b"\x89PNG\r\n\x1a\n" else "BMP"
        print("  %3dx%-3d %2dbpp %s  %7d bytes"
              % (w or 256, h or 256, bits, kind, len(data)))


if __name__ == "__main__":
    main()
