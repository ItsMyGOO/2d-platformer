"""Generate placeholder pixel-art PNGs for characters (no PIL, raw PNG via zlib)."""
import os
import struct
import zlib

OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Game",
    "Art",
    "Placeholders",
)

PALETTE = {
    ".": (0, 0, 0, 0),        # transparent
    "o": (24, 40, 28, 255),   # player outline (dark green)
    "g": (86, 182, 91, 255),  # player body (green)
    "w": (236, 240, 236, 255),# eye white
    "k": (22, 22, 26, 255),   # pupil / dark detail
    "s": (157, 78, 219, 255), # slime body (purple)
    "d": (74, 20, 140, 255),  # slime outline (dark purple)
    "t": (214, 202, 174, 255),# dust
}

PLAYER_IDLE = [
    "................",
    "................",
    "................",
    "....oooooo......",
    "...oggggggo.....",
    "...ogkggkgo.....",
    "...oggggggo.....",
    "..oggggggggo....",
    "..oggggggggo....",
    "..oggggggggo....",
    "...oggggggo.....",
    "....oggggo......",
    "....oggggo......",
    "....oggggo......",
    "....og..go......",
    "...ogg..ggo.....",
]

PLAYER_RUN1 = [
    "................",
    "................",
    "................",
    "....oooooo......",
    "...oggggggo.....",
    "...ogkggkgo.....",
    "...oggggggo.....",
    "..oggggggggo....",
    "..oggggggggo....",
    "..oggggggggo....",
    "...oggggggo.....",
    "....oggggo......",
    "....oggggo......",
    "....oggggo......",
    "....og..go......",
    "...og....go.....",
]

PLAYER_RUN2 = [
    "................",
    "................",
    "................",
    "....oooooo......",
    "...oggggggo.....",
    "...ogkggkgo.....",
    "...oggggggo.....",
    "..oggggggggo....",
    "..oggggggggo....",
    "..oggggggggo....",
    "...oggggggo.....",
    "....oggggo......",
    "....oggggo......",
    "...ogg..ggo.....",
    "...og....go.....",
    "..og......go....",
]

PLAYER_JUMP = [
    "................",
    "................",
    "................",
    "....oooooo......",
    "...oggggggo.....",
    "...ogkggkgo.....",
    "...oggggggo.....",
    "..oggggggggo....",
    "..oggggggggo....",
    "...oggggggo.....",
    "....oggggo......",
    "....oggggo......",
    "...ogg..ggo.....",
    "...og....go.....",
    "................",
    "................",
]

PLAYER_FALL = [
    "................",
    "................",
    "................",
    "....oooooo......",
    "...oggggggo.....",
    "...ogkggkgo.....",
    "...oggggggo.....",
    "..oggggggggo....",
    "..oggggggggo....",
    "..oggggggggo....",
    "...oggggggo.....",
    "....oggggo......",
    "...ogg..ggo.....",
    "..og......go....",
    "..o........o....",
    "................",
]

SLIME_IDLE = [
    "................",
    "................",
    "................",
    "................",
    "................",
    ".....dddddd.....",
    "....dssssssd....",
    "...dssssssssd...",
    "..dsswwsswwssd..",
    "..dsswksswkssd..",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    "dssssssssssssssd",
    "dddddddddddddddd",
    "................",
]

SLIME_RUN1 = [
    "................",
    "................",
    "................",
    "................",
    "................",
    ".....dddddd.....",
    "....dssssssd....",
    "...dssssssssd...",
    "..dsswwsswwssd..",
    "..dsswksswkssd..",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    "dssssssssssssssd",
    "dddddddddddddddd",
    "................",
]

SLIME_RUN2 = [
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    ".....dddddd.....",
    "....dssssssd....",
    "..ddsswwsswwssd.",
    "..dsswksswkssd..",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    "dssssssssssssssd",
    "dssssssssssssssd",
    "dddddddddddddddd",
    "................",
]

SLIME_JUMP = [
    "................",
    "................",
    "................",
    ".....dddddd.....",
    "....dssssssd....",
    "...dssssssssd...",
    "...dssssssssd...",
    "..dsswwsswwssd..",
    "..dsswksswkssd..",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    ".dssssssssssssd.",
    "dssssssssssssssd",
    "dddddddddddddddd",
    "................",
]

SLIME_FALL = [
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "....dddddddd....",
    "..ddsswwsswwssd.",
    ".dssssssssssssd.",
    "dssssssssssssssd",
    "dssssssssssssssd",
    "dssssssssssssssd",
    "dddddddddddddddd",
    "................",
]

DUST = [
    "tttt",
    "tttt",
    "tttt",
    "tttt",
]


def write_png(path, rows):
    height = len(rows)
    width = len(rows[0])
    assert all(len(r) == width for r in rows), f"ragged rows in {path}"
    raw = b""
    for row in rows:
        raw += b"\x00" + bytes(b for ch in row for b in PALETTE[ch])

    def chunk(tag, data):
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def main():
    os.makedirs(OUT, exist_ok=True)
    images = {
        "player_idle.png": PLAYER_IDLE,
        "player_run1.png": PLAYER_RUN1,
        "player_run2.png": PLAYER_RUN2,
        "player_jump.png": PLAYER_JUMP,
        "player_fall.png": PLAYER_FALL,
        "slime_idle.png": SLIME_IDLE,
        "slime_run1.png": SLIME_RUN1,
        "slime_run2.png": SLIME_RUN2,
        "slime_jump.png": SLIME_JUMP,
        "slime_fall.png": SLIME_FALL,
        "dust.png": DUST,
    }
    for name, rows in images.items():
        write_png(os.path.join(OUT, name), rows)
    print(f"wrote {len(images)} PNGs to {OUT}")


if __name__ == "__main__":
    main()
