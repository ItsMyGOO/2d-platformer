"""Generate placeholder pixel-art PNGs for characters (no PIL, raw PNG via zlib).
Sprites are authored as 16x16 ASCII grids and emitted at 32x32 (dust 8x8)
via nearest-neighbour doubling, keeping the old composition exactly.
"""
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
    "r": (226, 84, 84, 255),  # hurt tint (red)
    "e": (150, 30, 40, 255),  # heart outline (dark red)
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

PLAYER_DASH = [
    "................",
    "................",
    "................",
    "................",
    ".....oooooo.....",
    "....oggggggo....",
    "....ogkggkgo....",
    "...oggggggggo...",
    "...oggggggggo...",
    "..goggggggggo...",
    ".ggogggggggo....",
    "....oggggo......",
    "....oggggo......",
    "....ogg.go......",
    "...oggo.ggo.....",
    "..gg.....gg.....",
]

PLAYER_ATTACK = [
    "................",
    "................",
    "................",
    "....oooooo......",
    "...oggggggo.....",
    "...ogkggkgo.....",
    "...oggggggo.....",
    "..oggggggggggggo",
    "..ogggggggggggo.",
    "...oggggggo.....",
    "....oggggo......",
    "....oggggo......",
    "....oggggo......",
    "....og..go......",
    "...ogg..ggo.....",
    "................",
]

PLAYER_HURT = [
    "................",
    "................",
    "................",
    "....rrrrrr......",
    "...orggggro.....",
    "...okggggko.....",
    "...oggggggo.....",
    "..orggggggro....",
    "..orggggggro....",
    "..orrggggrro....",
    "...orggggro.....",
    "....orggro......",
    "....orggro......",
    "....or..ro......",
    "...orr..rro.....",
    "................",
]

PLAYER_DEATH = [
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "...oooooooooo...",
    "..oggggggggggo..",
    "..okkoooooooko..",
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

SLIME_ATTACK = [
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
    "dsswwsssswwssssd",
    "dssssssssssssssd",
    "dddddddddddddddd",
    "................",
]

SLIME_HURT = [
    "................",
    "................",
    "................",
    "................",
    "................",
    ".....dddddd.....",
    "....drssssrd....",
    "...drssssssrd...",
    "..drswrssrwsrd..",
    "..drswrssrwsrd..",
    ".drssssssssssrd.",
    ".drssssssssssrd.",
    ".drrssssssssrrd.",
    "drssssssssssssrd",
    "dddddddddddddddd",
    "................",
]

SLIME_DEATH = [
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "....dddddddd....",
    "..ddssssssssdd..",
    ".dssssssssssssd.",
    "dddddddddddddddd",
]

DUST = [
    "tttt",
    "tttt",
    "tttt",
    "tttt",
]

HEART = [
    "................",
    "................",
    "..eee....eee....",
    ".errrreeerrre...",
    ".errrrrrrrrrre..",
    ".erwrrrrrrrrre..",
    ".errrrrrrrrrre..",
    "..errrrrrrrre...",
    "...errrrrrre....",
    "....errrrre.....",
    ".....errre......",
    "......ere.......",
    ".......e........",
    "................",
    "................",
    "................",
]


def scale2x(rows):
    """最近邻 ×2：每像素横向复制一次、每行纵向复制一次。"""
    doubled = ["".join(ch * 2 for ch in row) for row in rows]
    return [row for row in doubled for _ in range(2)]


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
        "player_dash.png": PLAYER_DASH,
        "player_attack.png": PLAYER_ATTACK,
        "player_hurt.png": PLAYER_HURT,
        "player_death.png": PLAYER_DEATH,
        "slime_idle.png": SLIME_IDLE,
        "slime_run1.png": SLIME_RUN1,
        "slime_run2.png": SLIME_RUN2,
        "slime_jump.png": SLIME_JUMP,
        "slime_fall.png": SLIME_FALL,
        "slime_attack.png": SLIME_ATTACK,
        "slime_hurt.png": SLIME_HURT,
        "slime_death.png": SLIME_DEATH,
        "dust.png": DUST,
        "heart.png": HEART,
    }
    for name, rows in images.items():
        write_png(os.path.join(OUT, name), scale2x(rows))
    print(f"wrote {len(images)} PNGs to {OUT}")


if __name__ == "__main__":
    main()
