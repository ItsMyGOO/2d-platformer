#!/usr/bin/env bash
# 一键导出 Windows 版：构建 → headless 导出 → 产物校验
set -euo pipefail
GODOT="${GODOT:-D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe}"
dotnet build
"$GODOT" --headless --path . --export-release "Windows Desktop"
test -s bin/2d-platformer.exe && echo "EXPORT OK: bin/2d-platformer.exe ($(stat -c%s bin/2d-platformer.exe) bytes)"
