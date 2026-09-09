"""ASCII room layout -> Godot TileMapLayer tile_map_data generator (stdlib only).

Usage:
    python Tools/generate_room_tiles.py < layout.txt

Layout: 40 columns x 22 rows of ASCII. Each char is one 32px tile.
    '.' empty
    '#' terrain (auto: terrain_top when the cell above is empty, else terrain_fill)
    'W' wall
    '-' platform (auto: platform_top when the cell above is empty, else platform_fill)

TileMapLayer must be placed at position (0, 16): row 0 covers world y 16..48,
row 20 covers world y 656..688 (the standard ground top).

Output: the decimal comma list for the scene's
    tile_map_data = PackedByteArray(<output>)
Format: uint16 version (0) + per cell 12 bytes little-endian
    int16 x, int16 y, int16 source_id, int16 atlas_x, int16 atlas_y, int16 alternative.
"""

import sys

TILE = 32
SOURCE_ID = 0
# 图集列：0 地形顶 / 1 地形填 / 2 墙 / 3 平台顶 / 4 平台身
ATLAS = {"terrain_top": 0, "terrain_fill": 1, "wall": 2, "platform_top": 3, "platform_fill": 4}

# 布局第 0 列对应的 tile 列（RoomA 左墙在世界 -32..0，故为 -1）
ORIGIN_X = 0
for _i, _a in enumerate(sys.argv):
    if _a == "--origin-x":
        ORIGIN_X = int(sys.argv[_i + 1])


def parse(lines):
    grid = []
    width = None
    for line in lines:
        line = line.rstrip("\n")
        if not line:
            continue
        if width is None:
            width = len(line)
        if len(line) != width:
            raise SystemExit(f"ragged layout: expected {width} cols, got {len(line)}: {line!r}")
        for ch in line:
            if ch not in ".#W-":
                raise SystemExit(f"unknown char {ch!r} in layout")
        grid.append(line)
    if not grid:
        raise SystemExit("empty layout")
    return grid


def cell_to_tile(grid, x, y):
    ch = grid[y][x]
    if ch == ".":
        return None
    above_empty = y == 0 or grid[y - 1][x] == "."
    if ch == "#":
        return ATLAS["terrain_top"] if above_empty else ATLAS["terrain_fill"]
    if ch == "W":
        return ATLAS["wall"]
    if ch == "-":
        return ATLAS["platform_top"] if above_empty else ATLAS["platform_fill"]
    raise SystemExit(f"unreachable char {ch!r}")


def serialize(grid):
    data = bytearray()
    data += (0).to_bytes(2, "little")  # format version
    for y, row in enumerate(grid):
        for i in range(len(row)):
            atlas_x = cell_to_tile(grid, i, y)
            if atlas_x is None:
                continue
            x = i + ORIGIN_X
            data += int(x).to_bytes(2, "little", signed=True)
            data += int(y).to_bytes(2, "little", signed=True)
            data += SOURCE_ID.to_bytes(2, "little", signed=True)
            data += int(atlas_x).to_bytes(2, "little", signed=True)
            data += (0).to_bytes(2, "little", signed=True)  # atlas y
            data += (0).to_bytes(2, "little", signed=True)  # alternative
    return ",".join(str(b) for b in data)


def main():
    grid = parse(sys.stdin.readlines())
    print(serialize(grid))


if __name__ == "__main__":
    main()
