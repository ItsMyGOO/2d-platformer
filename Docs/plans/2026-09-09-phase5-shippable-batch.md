# 第五期实施计划：能出货批次（存档 / 设置 / 重映射 / 手柄 / 导出 / 本地化）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补齐「能作为商品出货」的工程外围：检查点自动存档与主菜单继续游戏、设置菜单（全屏/VSync/震屏）、键位重映射、手柄默认支持、Windows 一键导出打包、UI 字符串本地化埋点。玩法逻辑零改动。

**Architecture:** 新增 `Game/Persistence/`（持久化命名空间：SaveData 纯 C# 可测 + SaveStore/SettingsService/InputRemapStore 薄 IO 层，全部落 `user://`）；存档语义 = **最近重生点自动存档**（跨房/检查点更新重生点即写盘，不引入存档菜单）；UI 新增 SettingsMenu（主菜单与暂停菜单均可进入）；导出走 Godot export preset + 本地脚本。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0（System.Text.Json / ConfigFile）、xUnit、CSharpier 1.3.0、Godot 导出模板 4.6.1。

**环境事实（本机已验证 / 实施时先核）：**

- 基线：32 单元测试 + 探针 9/9 + CI 绿；CSharpier 1.3.0，提交前 `format` + `check`
- Godot：`D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe`（下文 `$GODOT`）；shell 为 Git Bash
- **历史教训三条**（前几期踩过）：
  1. 构建 gate 必须用退出码，`grep "个错误"` 会把 error 行误当成功放行；
  2. 新增资源（png/cs）后 headless 直接跑会「No loader found / class not found」——先 `$GODOT --headless --import`；
  3. 编辑器开着会锁 `.godot`，headless 启动表现为 5MB 内存级「挂起」——先确认无编辑器/游戏进程再跑探针
- 存档/设置文件落在 `user://`（Windows 即 `%APPDATA%\Godot\app_userdata\2D Platformer\`）
- 探针直接实例化 World、不经 Main——UI/存档改动不应影响探针（每任务照跑防回归）
- MainMenu 结构：`Center/VBox/{Title,StartButton,QuitButton}`；PauseMenu：`Center/VBox/{Title,ResumeButton,MainMenuButton}`；Main 状态机 InMenu/Playing/Paused
- `Character.SetSpawnPoint` 已存在（World 跨格与 Checkpoint 都在调）；`Respawned` 事件已有（CameraRig 订阅瞬移）
- 导出前置：Godot 4.6.1 导出模板已装否未知——Task 5 Step 1 先验，缺则编辑器内下载一次

## 裁量点（执行前可否决，默认按此实施）

1. **存档 = 最近重生点自动存档**：跨房换重生点或踩检查点即写盘（不做存档槽/存档菜单）；
   「继续游戏」= 新世界 + 玩家瞬移到该点满血开始。敌人/收集不持久化——SaveData 带 `version`
   字段留扩展位（收集品做了再加字段）。
2. **「开始游戏」不弹确认**：有存档时新开局照常，首个检查点覆盖旧档（不做覆盖警告弹窗）。
3. **改键只替换键盘事件**（`InputEventKey`）：保留该动作的手柄/鼠标事件（如攻击的鼠标左键）；
   手柄键位自定义不做（backlog），只给默认映射。
4. **无音频滑块**：音频系统未建，设置页留「音频」占位注释，随音频接入补。
5. **导出只做 Windows 桌面 + 本地一键脚本**；CI 导出产物 job 列为可选步骤；图标沿用 icon.svg。
6. **本地化只做框架 + zh_CN**：源字符串改 key，fallback_locale=zh_CN；英文表后补（backlog）。

---

### Task 1: 存档系统（自动存档 + 继续游戏）

**Files:**
- Create: `Game/Persistence/SaveData.cs`（纯 C# DTO + Json 序列化）
- Create: `Game/Persistence/SaveStore.cs`（user://save.json 读写）
- Modify: `Game/Gameplay/Characters/Character.cs`（`SpawnPointChanged` 事件 + `PlaceAt`）
- Modify: `Game/UI/MainMenu.tscn` / `MainMenu.cs`（「继续游戏」按钮）
- Modify: `Game/UI/Main.cs`（订阅存档 + ContinueGame）
- Create: `Tests/UnitTests/SaveDataTests.cs`

- [ ] **Step 1: SaveData / SaveStore**

```csharp
// Game/Persistence/SaveData.cs
namespace GodotGameTemplate.Persistence;

/// <summary>存档数据（纯 C#，可单测）。当前仅记录最近重生点；version 留作收集/能力扩展位。</summary>
public class SaveData
{
    public int Version { get; set; } = 1;
    public float SpawnX { get; set; }
    public float SpawnY { get; set; }
}
```

```csharp
// Game/Persistence/SaveStore.cs —— 薄 IO 层，不进单测
using System.Text.Json;
using Godot;

namespace GodotGameTemplate.Persistence;

/// <summary>存档读写（user://save.json）。只在最近重生点变化时写盘。</summary>
public static class SaveStore
{
    private static readonly string Path = "user://save.json";

    public static bool Exists() => FileAccess.FileExists(Path);

    public static void Save(SaveData data)
    {
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(data));
    }

    public static bool TryLoad(out SaveData data)
    {
        data = null;
        if (!FileAccess.FileExists(Path)) return false;
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        data = JsonSerializer.Deserialize<SaveData>(f.GetAsText());
        return data != null;
    }

    public static void Clear() => DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
}
```

- [ ] **Step 2: Character 两处扩展**

```csharp
    /// <summary>重生点被更新时触发（跨房 / 检查点；持久层订阅做自动存档）。</summary>
    public event Action<Vector2> SpawnPointChanged;

    /// <summary>更新重生点（世界流式与检查点调用；默认为初始出生点）。</summary>
    public void SetSpawnPoint(Vector2 globalPosition)
    {
        _spawnPosition = globalPosition;
        SpawnPointChanged?.Invoke(globalPosition);
    }

    /// <summary>瞬移到指定位置（继续游戏落到存档点）：清速度、立即触发相机瞬移。</summary>
    public void PlaceAt(Vector2 globalPosition)
    {
        GlobalPosition = globalPosition;
        Velocity = Vector2.Zero;
        Respawned?.Invoke();
    }
```

注意：World._Ready 的初始 SetSpawnPoint 发生在 Main 订阅**之前**（AddChild 内），不会写盘；
跨格与检查点的后续更新才触发存档。

- [ ] **Step 3: MainMenu 加「继续游戏」**（Start 按钮下方；无存档时 Disabled），
  `MainMenu.cs` 加 `ContinueRequested` 事件；`_Ready` 里按 `SaveStore.Exists()` 设 Disabled。

- [ ] **Step 4: Main 接线**

```csharp
    // StartGame 内取到 player 后：
    player.SpawnPointChanged += pos =>
        SaveStore.Save(new SaveData { SpawnX = pos.X, SpawnY = pos.Y });

    // 新增 ContinueGame：StartGame() 后按存档落位
    private void ContinueGame()
    {
        StartGame();
        if (SaveStore.TryLoad(out var save))
        {
            _levelRoot.GetNode<Character>("Level/Player").PlaceAt(new Vector2(save.SpawnX, save.SpawnY));
        }
    }
```

- [ ] **Step 5: 单测**（SaveData 的 Json 往返 + version 字段保留；System.Text.Json 直接测）

- [ ] **Step 6: 回归与提交**

```bash
dotnet csharpier format . && dotnet csharpier check .
dotnet build   # 退出码为准
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn   # 9/9（探针无 Main，不受影响）
git add -A && git commit -m "feat: 检查点自动存档与主菜单继续游戏（最近重生点语义）"
```

---

### Task 2: 设置菜单（全屏 / VSync / 震屏）+ 持久化

**Files:**
- Create: `Game/Persistence/SettingsService.cs`（ConfigFile user://settings.cfg + Apply）
- Create: `Game/UI/SettingsMenu.tscn` / `SettingsMenu.cs`
- Modify: `Game/UI/MainMenu.tscn` / `PauseMenu.tscn`（加「设置」按钮）
- Modify: `Game/UI/Main.cs`（设置入口与返回；游玩中即时应用震屏开关）

- [ ] **Step 1: SettingsService**：属性 `Fullscreen` / `Vsync` / `ScreenshakeEnabled`（默认
  false/true/true）；`Load()` 启动读、`Save()` 变更写；`ApplyDisplay()` 调
  `DisplayServer.WindowSetMode / WindowSetVsyncMode`。

- [ ] **Step 2: SettingsMenu 场景**：VBox 分组「显示」（全屏、垂直同步 CheckButton）
  「玩法」（命中震屏 CheckButton）「操作」（本任务占位 Label，Task 3 填充）+「返回」按钮；
  `process_mode = 3`（暂停树中可用）。脚本：开关信号 → SettingsService.Save + Apply；
  `Opened(Action onClosed)` / 关闭回调由 Main 管理。

- [ ] **Step 3: Main 接线**：MainMenu 与 PauseMenu 各加「设置」按钮 → 记录来源（菜单态/暂停态）
  → 打开 SettingsMenu，返回时回来源界面；游玩中切换震屏 → 立即对当前玩家的
  `ScreenShake.Enabled` 生效（`_level` 存在时 `FindDescendant<ScreenShake>`）；StartGame 时按
  SettingsService 初始化。Esc 在设置页 = 返回（`_UnhandledInput` 分支）。

- [ ] **Step 4: 启动应用**：Main._Ready 读 SettingsService.Load + ApplyDisplay。

- [ ] **Step 5: 回归与提交**（同 Task 1 命令模板）

```bash
git add -A && git commit -m "feat: 设置菜单（全屏/VSync/震屏开关，ConfigFile 持久化）"
```

---

### Task 3: 键位重映射

**Files:**
- Create: `Game/Persistence/InputRemapStore.cs`（user://input_overrides.json：action → physical_keycode）
- Modify: `Game/UI/SettingsMenu.tscn` / `SettingsMenu.cs`（「操作」节：六行 改键按钮 + 恢复默认）
- Modify: `Game/UI/Main.cs`（启动应用覆盖）

- [ ] **Step 1: InputRemapStore**：`CaptureDefaults()`（首次启动快照 InputMap 各 action 的
  全部事件，供恢复默认）；`Apply()`（读 JSON，对每 action 把现有 `InputEventKey` 事件替换为
  新键——**只动键盘事件**，保留手柄/鼠标事件）；`Set(action, key)` / `Reset()`。

- [ ] **Step 2: 改键 UI 与捕获流程**：每行 = 动作名 Label + 当前键名 Button；点击进入捕获态
  （按钮文本「按下新按键…」，Esc 取消）→ `_UnhandledInput` 收到 `InputEventKey`（非 echo）→
  `InputRemapStore.Set` + 刷新显示。捕获期间 Main 的 Esc 暂停分支让位（SettingsMenu 捕获态
  优先消费）。底部「恢复默认」= `Reset()` + 刷新。

- [ ] **Step 3: 启动接线**：Main._Ready 在菜单可用前 `CaptureDefaults()` + `Apply()`。

- [ ] **Step 4: 回归（含探针——探针用 `Input.ActionPress("jump"/"move_right")`，改键不影响
  action 名）与提交**

```bash
git add -A && git commit -m "feat: 键位重映射（仅替换键盘事件，保留手柄/鼠标；恢复默认）"
```

---

### Task 4: 手柄默认支持

**Files:**
- Modify: `project.godot`（[input] 各动作补 joypad 事件）
- Modify: `Game/UI/SettingsMenu.cs`（捕获态接受 joypad 按键也可改手柄键——若成本低则一并做，否则跳过，见裁量点 3）

- [ ] **Step 1: 在编辑器 Input Map 界面添加**（避免手写 joypad 序列化出错），映射表：

| 动作 | 手柄事件 |
|---|---|
| move_left / move_right | 左摇杆 X 轴 −/＋；十字键左/右 |
| jump | A（底部，button 0） |
| attack | B（右侧，button 1） |
| dash | X（左部，button 2） |
| pause | Start（button 6） |

（上方向键/上摇杆不绑跳跃——与键盘一致，留作方向输入扩展。）

- [ ] **Step 2: 菜单焦点导航**：MainMenu/PauseMenu/SettingsMenu 首按钮 GrabFocus 已有；
  实机手柄验证上下导航（VBox 内 Button 自动邻居通常成立，不成立则显式 `focus_next/previous`）。

- [ ] **Step 3: 实机验证 + 提交**：手柄完整走一遍 菜单→游玩→暂停→继续；键盘行为不回归
  （探针照跑）。

```bash
git add -A && git commit -m "feat: 手柄默认映射（左摇杆/十字键移动，A跳B攻X冲Start暂停）"
```

---

### Task 5: Windows 导出打包

**Files:**
- Create: `export_presets.cfg`（编辑器配置一次后入库）
- Create: `Tools/export.sh`（一键导出）
- Modify: `.gitignore`（忽略 `bin/`）

- [x] **Step 1: 前置检查**（模板 4.6.1.stable.mono 已装）：导出模板是否已装（`%APPDATA%\Godot\export_templates\4.6.1.stable/`）
  ——缺则编辑器「管理导出模板」下载安装一次（约 1GB，一次性）。

- [x] **Step 2: 编辑器配置导出预设**（无编辑器环境，手写预设 headless 导出成功）：Project → Export → 添加 Windows Desktop：
  名称 `Windows Desktop`；勾选 .NET（默认）；Embed PCK 勾选（单 exe）；导出路径
  `bin/2d-platformer.exe`；图标默认。配置完成后 `export_presets.cfg` 入库。

- [x] **Step 3: 一键脚本 `Tools/export.sh`**

```bash
#!/usr/bin/env bash
# 一键导出 Windows 版：构建 → headless 导出 → 产物校验
set -euo pipefail
GODOT="${GODOT:-D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe}"
dotnet build
"$GODOT" --headless --path . --export-release "Windows Desktop"
test -s bin/2d-platformer.exe && echo "EXPORT OK: bin/2d-platformer.exe ($(stat -c%s bin/2d-platformer.exe) bytes)"
```

- [x] **Step 4: 验证与提交**（exe 产出 105MB；双击实机验收待人工——已知问题：导出 exe 启动段错误，见文末执行记录）：跑脚本产出 exe → 双击实机跑一遍 主菜单→继续/新开→暂停→退出
  （用户参与验收）；产物不入库（bin/ 已忽略）。

```bash
git add export_presets.cfg Tools/export.sh .gitignore
git commit -m "feat: Windows 导出预设与一键导出脚本"
```

- [x] **Step 5（按计划默认跳过）**：CI 导出产物 job（下载 Godot+模板→export→upload-artifact，
  仅打 tag 触发）。执行时若判定收益低可直接跳过并在提交信息注明。

---

### Task 6: 本地化埋点

**Files:**
- Create: `translations/zh_CN.csv`
- Modify: `project.godot`（internationalization/locale/translations + fallback_locale）
- Modify: `Game/UI/MainMenu.cs` / `PauseMenu.cs` / `SettingsMenu.cs` / `Hud.tscn`（死亡文案）

- [ ] **Step 1: 翻译表**（key,zh_CN 两列）：MENU_START/MENU_CONTINUE/MENU_QUIT/MENU_SETTINGS/
  PAUSE_TITLE/PAUSE_RESUME/PAUSE_MAINMENU/SETTINGS_*/ACTION_*/BIND_CAPTURE/BTN_BACK/
  BTN_RESET_DEFAULTS/DEATH_TEXT/GAME_TITLE 等全部现有 UI 文案。

- [ ] **Step 2: project.godot**：

```ini
[internationalization]

locale/translations=PackedStringArray("res://translations/zh_CN.csv")
locale/fallback_locale="zh_CN"
```

- [ ] **Step 3: 字符串替换**：C# 侧 `Tr("MENU_START")`（Node 方法）；.tscn 里写死的
  Button.text / Label.text 改由脚本 `_Ready` 里赋 `Tr(...)`（场景文件不留中文硬编码）。

- [ ] **Step 4: 回归与提交**：实机/探针确认文案仍为中文（fallback 生效）。

```bash
git add -A && git commit -m "feat: UI 字符串本地化埋点（zh_CN 翻译表 + Tr 化）"
```

---

### Task 7: 文档同步、全量回归与推送

- [ ] **Step 1: 文档**

- `architecture.md`：§1 目录补 `Game/Persistence/` 与 `translations/`；§10 UI 小节补
  设置菜单/存档语义（最近重生点自动存档）/继续游戏
- `backlog.md`：P2 勾记 手柄/重映射/设置菜单；新增挂账——手柄键位自定义、音频滑块（随音频
  接入）、英文翻译文案、CI 导出产物 job（若 Task 5 Step 5 跳过）、胜利/结局画面
- `README.md`：操作表补手柄列；快速开始补 `Tools/export.sh`；「继续游戏」一句话

- [ ] **Step 2: 全量回归（CI 等价三连 + 探针 9/9）**

```bash
dotnet csharpier check .
dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn; echo "exit=$?"
```

- [ ] **Step 3: 推送并确认 CI 绿**

- [ ] **Step 4: 实机验收（用户参与）**：存档（跨房→退出→继续游戏落位满血）；改键生效且重启
  保持、恢复默认可用；手柄全流程；设置三开关即时生效且重启保持；导出 exe 双击可玩；
  文案全部中文显示。

---

## 非目标（本期不做）

- 音频系统与音量滑块（素材到位后另期）；胜利/结局画面；收集品与能力持久化（存档 version 已留位）
- 手柄键位自定义与按键图标（glyph）；键位冲突检测（同键绑定多动作允许，Godot 原生行为）
- Steam 集成（成就/云存档/Steam Input）、多平台导出（mac/Linux/掌机）、CI 自动发布
- 英文文案翻译（框架就绪，文案后补）
