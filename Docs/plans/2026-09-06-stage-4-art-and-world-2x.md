# 阶段④实施计划：32×32 美术适配与世界 ×2

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 占位美术从 16×16 升级到 32×32（路线 B：世界整体 ×2，构图不变），TestLevel 几何与全部长度/速度类数值等比 ×2，AI 感知参数同步 ×2 并顺带修复「追击停止距离大于攻击命中距离」的僵持问题，探针阈值适配后回归全绿。

**Architecture:** 无结构性变更。全部改动为「资源 + 场景 + 数值」的等比标定，唯一触碰 C# 逻辑的是 `SlimeAIInputSource`（射线坐标 ×2 + 攻击距离重新标定）、`CharacterMotor.Kill` 的死亡小跳常量与探针本身。状态机、Motor、装配约定均不动。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0、Python 3（仅 stdlib，重生成占位图）、CSharpier 1.3.0、xUnit、GitHub Actions。

**环境事实（本机已验证，2026-09-06）：**

- 工作目录 `D:\GdProject\2d-platformer`，shell 为 Git Bash，工作区干净
- Godot 可执行文件：`D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe`（下文以 `$GODOT` 指代）
- 探针基线：5/5 通过（跳跃上升实测 52.2px ≥ 45px），`dotnet build` 0 错误
- Python 3.14.2 在 PATH；美术脚本只用 stdlib（无 PIL 依赖）
- CSharpier 本地工具清单钉版 1.3.0（`.config/dotnet-tools.json`），与本机全局一致；**动词是 `format`/`check`**（0.29 时代的 `dotnet csharpier .` 语法已弃用）
- `.gitattributes` 对 `*.cs` `*.tscn` `*.tres` `*.py` `*.md` 均强制 LF；新建/改写 .cs 后必须过一遍 `dotnet csharpier format .` 再提交（曾发生 CRLF 导致「本地过、CI 挂」，见 `Docs/code-standards.md` §5）

---

## ×2 标定总则（所有数值的推导依据）

| 量纲 | 规则 | 例 |
|---|---|---|
| 长度（尺寸/偏移/距离/半径/射线长） | ×2 | body 12×14→24×28、视野半径 90→180 |
| 速度/加速度（px/s、px/s²） | ×2 | MaxSpeed 130→260、击退 180→360、重力 980×GravityScale |
| 时间（时长/冷却/无敌帧） | 不变 | 土狼 0.1s、硬直 0.3s、攻击窗口 |
| 动画帧率 | 与移动耦合的 run ×2，其余不变 | run 8→16 fps（像素步长 16.25px/步 不变） |
| 粒子 lifetime/amount/scale | 不变 | 贴图已 ×2，视觉尺寸随之 ×2 |

**自洽性**（跳跃弧线，探针回归是最终裁决）：
起跳高度 h = v²/2g → (2v)²/(2·2g) = 2h；滞空时间 t = v/g 不变；
跳跃水平距离 = 速度×滞空 → ×2，与几何 ×2 自洽。
满跳理论高度：330²/1960 ≈ 55.6px → 660²/3920 ≈ 111.1px（基线实测 52.2px 为松键截断值，×2 后机动窗改满跳，预期 ~111px）。

**×2 后关卡地标**（探针断言与调试均以此为准）：
地面顶 y=656（站立中心 y≈642）、平台顶 y=560（站立 y≈546）、
窄坑 x∈[512,544]（32px）、宽坑 x∈[992,1120]（128px）、关卡右界 x=1280。

**僵持问题的几何根因**（Task 4 标定依据）：
史莱姆命中可达 = 命中盒偏移 + 命中盒半宽 + 玩家受击盒半宽 = 10+7+7 = 24px（×2 后 20+14+14 = 48px）；
追击停止距离 `_attackDistance` 旧值 26（×2 盲目放大得 52）> 可达距离 → 停在射程外空挥。
修复 = 把停止距离压进可达范围并留余量：**26 → 40**（≤48，留 8px 重叠余量；52 会原样复刻僵持）。

## 裁量点（超出路线图文字的决定，审阅时可否决）

1. **`_attackDistance` 26→40 而非 ×2 的 52**：52 > 命中可达 48，会复刻僵持；40 = 48 − 8px 余量（路线图已写「重新标定」）。
2. **Acceleration/Friction ×2**（1000→2000 等）：属 px/s² 速度量纲，按总则推导，路线图未逐项列出。
3. **死亡小跳 -140→-280**：`CharacterMotor.Kill` 硬编码速度量，不同步则死亡动画只有半高；需同步改一条单元测试断言。
4. **探针从 5 项扩到 8 项**：验收要求「跳上平台」「追击攻击命中 HP 下降」，现探针没有这两项检查，须新增；阶段二脚本改为「助跑+满跳登平台+落回地面」。
5. **窗口覆盖 1280×720 → 1920×1080**：按路线图建议采纳。注意 1.5× 非整数缩放可能有像素不均观感，实施后肉眼检查，若不适改回 1280×720（此时恰为 1:1，屏上精灵尺寸与旧 2× 窗口完全一致），非阻塞项。
6. **run 动画帧率 ×2**（玩家 8→16、史莱姆 6→12）：对齐加倍后的地面速度；帧纹理引用同名覆盖、无需改动。

---

### Task 1: 占位美术 ×2 重生成 + 帧动画速度

**Files:**
- Modify: `Tools/generate_placeholder_art.py`
- Regenerate: `Game/Art/Placeholders/*.png`（18 张，同名覆盖）
- Modify: `Game/Art/Placeholders/player_frames.tres`（run speed 8.0→16.0）
- Modify: `Game/Art/Placeholders/slime_frames.tres`（run speed 6.0→12.0）

 ASCII 像素画保持 16×16 手绘网格不动，输出时最近邻 ×2——构图与 16×16 时代逐像素一致，满足验收「画面构图一致」。

- [ ] **Step 1: 脚本加 scale2x 并改输出调用**

模块 docstring 改为：

```python
"""Generate placeholder pixel-art PNGs for characters (no PIL, raw PNG via zlib).
Sprites are authored as 16x16 ASCII grids and emitted at 32x32 (dust 8x8)
via nearest-neighbour doubling, keeping the old composition exactly.
"""
```

在 `write_png` 之前加入：

```python
def scale2x(rows):
    """最近邻 ×2：每像素横向复制一次、每行纵向复制一次。"""
    doubled = ["".join(ch * 2 for ch in row) for row in rows]
    return [row for row in doubled for _ in range(2)]
```

`main()` 中的写入行改为：

```python
    for name, rows in images.items():
        write_png(os.path.join(OUT, name), scale2x(rows))
```

- [ ] **Step 2: 重生成并校验尺寸**

```bash
python Tools/generate_placeholder_art.py
file Game/Art/Placeholders/player_idle.png Game/Art/Placeholders/dust.png
```
Expected: `wrote 18 PNGs to ...`；`PNG image data, 32 x 32` 与 `8 x 8`。

- [ ] **Step 3: 帧资源 run 速度 ×2**

- `player_frames.tres`：`"name": &"run"` 动画块内 `"speed": 8.0` → `"speed": 16.0`
- `slime_frames.tres`：`"name": &"run"` 动画块内 `"speed": 6.0` → `"speed": 12.0`
- 其余动画速度与全部纹理引用不动（PNG 同名覆盖，引用自愈）。

- [ ] **Step 4: 构建（.import 由 Godot 首次运行时自动重导入，属预期）并提交**

```bash
dotnet build
git add Tools/generate_placeholder_art.py Game/Art/Placeholders/
git commit -m "feat: 占位美术 ×2 重生成为 32×32（dust 8×8），run 动画帧率同步加倍"
```

---

### Task 2: 世界几何、视口与相机边界 ×2

**Files:**
- Modify: `Game/Scenes/TestLevel.tscn`（几何全表 ×2 + 出生点）
- Modify: `project.godot`（视口 1280×720、窗口覆盖 1920×1080）
- Modify: `Game/Scenes/Player.tscn`（相机 limit_right/bottom）

- [ ] **Step 1: TestLevel.tscn 按下表逐项 ×2**

| 节点/子资源 | 属性 | 旧 | 新 |
|---|---|---|---|
| Sky | offset_right / offset_bottom | 640 / 360 | 1280 / 720 |
| ground_a | size | Vector2(256, 32) | Vector2(512, 64) |
| GroundA | position | Vector2(128, 344) | Vector2(256, 688) |
| GroundA/Fill | L/T/R/B | -128/-16/128/16 | -256/-32/256/32 |
| GroundA/Top | L/T/R/B | -128/-16/128/-12 | -256/-32/256/-24 |
| ground_b | size | Vector2(224, 32) | Vector2(448, 64) |
| GroundB | position | Vector2(384, 344) | Vector2(768, 688) |
| GroundB/Fill | L/T/R/B | -112/-16/112/16 | -224/-32/224/32 |
| GroundB/Top | L/T/R/B | -112/-16/112/-12 | -224/-32/224/-24 |
| ground_c | size | Vector2(80, 32) | Vector2(160, 64) |
| GroundC | position | Vector2(600, 344) | Vector2(1200, 688) |
| GroundC/Fill | L/T/R/B | -40/-16/40/16 | -80/-32/80/32 |
| GroundC/Top | L/T/R/B | -40/-16/40/-12 | -80/-32/80/-24 |
| platform | size | Vector2(48, 16) | Vector2(96, 32) |
| Platform | position | Vector2(152, 288) | Vector2(304, 576) |
| Platform/Fill | L/T/R/B | -24/-8/24/8 | -48/-16/48/16 |
| Platform/Top | L/T/R/B | -24/-8/24/-5 | -48/-16/48/-10 |
| wall | size | Vector2(16, 800) | Vector2(32, 1600) |
| WallLeft | position | Vector2(-8, 180) | Vector2(-16, 360) |
| WallRight | position | Vector2(648, 180) | Vector2(1296, 360) |
| catcher | size | Vector2(640, 16) | Vector2(1280, 32) |
| PitFloor | position | Vector2(320, 500) | Vector2(640, 1000) |
| Player | position | Vector2(64, 320) | Vector2(128, 640) |
| Slime1 | position | Vector2(340, 320) | Vector2(680, 640) |
| Slime2 | position | Vector2(430, 320) | Vector2(860, 640) |

几何锚点自检：GroundA 跨 0–512、GroundB 跨 544–992、GroundC 跨 1120–1280 →
窄坑 [512,544] 恰 32px、宽坑 [992,1120] 恰 128px；墙体内面 x=0 / x=1280；平台 256–352、顶 560。

- [ ] **Step 2: project.godot [display] 段**

```ini
window/size/viewport_width=1280
window/size/viewport_height=720
window/size/window_width_override=1920
window/size/window_height_override=1080
```
stretch mode `canvas_items`、aspect `keep` 不动。

- [ ] **Step 3: Player.tscn 相机边界**

```ini
[node name="Camera" type="Camera2D" parent="."]
limit_left = 0
limit_top = 0
limit_right = 1280
limit_bottom = 720
position_smoothing_enabled = true
position_smoothing_speed = 8.0
```

- [ ] **Step 4: 构建并提交**

```bash
dotnet build
git add Game/Scenes/TestLevel.tscn Game/Scenes/Player.tscn project.godot
git commit -m "feat: 关卡几何 ×2 并升级视口至 1280×720，相机边界随关卡"
```

---

### Task 3: 角色碰撞/尘土/数值配置 ×2（含死亡小跳）

**Files:**
- Modify: `Game/Scenes/BaseCharacter.tscn`
- Modify: `Game/Config/player_config.tres`
- Modify: `Game/Config/slime_config.tres`
- Modify: `Game/Gameplay/Characters/CharacterMotor.cs`（Kill 常量）
- Modify: `Tests/UnitTests/CharacterMotorTests.cs`（对应断言）

- [ ] **Step 1: BaseCharacter.tscn 碰撞等比与偏移**

| 项 | 旧 | 新 |
|---|---|---|
| RectangleShape2D_body size | Vector2(12, 14) | Vector2(24, 28) |
| RectangleShape2D_hit size | Vector2(14, 10) | Vector2(28, 20) |
| RectangleShape2D_hurt size | Vector2(14, 16) | Vector2(28, 32) |
| Sprite position | Vector2(0, -1) | Vector2(0, -2) |
| HitShape position | Vector2(11, 0) | Vector2(22, 0) |
| DebugVisual offsets L/T/R/B | 4/-5/18/5 | 8/-10/36/10 |
| Dust position | Vector2(0, 7) | Vector2(0, 14) |
| Dust gravity | Vector2(0, 600) | Vector2(0, 1200) |
| Dust initial_velocity_min / max | 30 / 80 | 60 / 160 |

不变：`scale_amount_min/max` 0.5/0.75（贴图已 ×2，视觉尺寸随之 ×2）、`lifetime` 0.35、`amount` 10、
方向/扩散、三段攻击窗口等一切时间量。
脚底对齐依据：32×32 精灵脚底在精灵中心下方 16px，×2 后身体半高 14 → 精灵中心上移 2px 至 (0,-2)。

- [ ] **Step 2: player_config.tres 数值 ×2**

```ini
[sub_resource type="Resource" id="Resource_dash"]
script = ExtResource("2_dash")
Speed = 840.0
Duration = 0.18
Cooldown = 0.6
EndSpeedKeepRatio = 0.4

[sub_resource type="Resource" id="Resource_attack"]
script = ExtResource("3_atk")
Damage = 1
KnockbackHorizontal = 360.0
KnockbackVertical = 240.0
Cooldown = 0.5

[resource]
script = ExtResource("1_cfg")
MaxSpeed = 260.0
Acceleration = 2000.0
Friction = 2800.0
JumpVelocity = -660.0
GravityScale = 2.0
JumpCutMultiplier = 0.45
CoyoteTime = 0.1
JumpBufferTime = 0.12
MaxFallSpeed = 1040.0
MaxHP = 5
InvincibilityTime = 0.8
HurtStunTime = 0.3
Dash = SubResource("Resource_dash")
Attack = SubResource("Resource_attack")
```

- [ ] **Step 3: slime_config.tres 数值 ×2**

```ini
[sub_resource type="Resource" id="Resource_attack"]
script = ExtResource("2_atk")
Damage = 1
KnockbackHorizontal = 280.0
KnockbackVertical = 200.0
Cooldown = 0.8

[resource]
script = ExtResource("1_cfg")
MaxSpeed = 120.0
Acceleration = 1000.0
Friction = 1400.0
JumpVelocity = -560.0
GravityScale = 2.0
JumpCutMultiplier = 0.5
CoyoteTime = 0.05
JumpBufferTime = 0.05
MaxFallSpeed = 960.0
MaxHP = 2
InvincibilityTime = 0.4
HurtStunTime = 0.25
Attack = SubResource("Resource_attack")
```

（两只角色 `JumpCutMultiplier`、土狼/缓冲、HP/无敌/硬直、冷却、伤害均不变。）

- [ ] **Step 4: 死亡小跳常量同步 ×2**

`CharacterMotor.cs` `Kill()` 内：

```csharp
        Velocity = new Vector2(0f, -280f); // 死亡小跳（随世界 ×2）
```

`CharacterMotorTests.cs` `Kill_EntersDeadState_WhichIgnoresHurtAndInput` 中：

```csharp
        Assert.Equal(-280f, motor.Velocity.Y); // 死亡小跳（随世界 ×2）
```

其余 24 个测试用旧尺度 TestConfig 只验证逻辑、与尺度无关，不动。

- [ ] **Step 5: 格式化、构建、测试并提交**

```bash
dotnet csharpier format .
dotnet csharpier check .
dotnet build
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
git add Game/Scenes/BaseCharacter.tscn Game/Config/ Game/Gameplay/Characters/CharacterMotor.cs Tests/UnitTests/CharacterMotorTests.cs
git commit -m "feat: 角色碰撞/尘土/数值配置 ×2，死亡小跳同步"
```
Expected: check 通过、Build succeeded、`Passed! - Failed: 0, Passed: 25`。

---

### Task 4: 史莱姆 AI 感知 ×2 + 攻击距离重新标定（僵持修复）

**Files:**
- Modify: `Game/Scenes/Slime.tscn`
- Modify: `Game/Gameplay/Characters/InputSources/SlimeAIInputSource.cs`

- [ ] **Step 1: Slime.tscn 场景侧参数**

| 项 | 旧 | 新 |
|---|---|---|
| CircleShape2D_vision radius | 90.0 | 180.0 |
| HitShape position | Vector2(10, 0) | Vector2(20, 0) |
| WallRay position / target_position | (0, -1) / (14, 0) | (0, -2) / (28, 0) |
| LedgeNearRay position / target_position | (6, 6) / (8, 10) | (12, 12) / (16, 20) |
| LedgeFarRay position / target_position | (2, 6) / (30, 10) | (4, 12) / (60, 20) |

- [ ] **Step 2: SlimeAIInputSource.cs 运行时射线坐标 ×2**

`IsGapTooWide()` 内两行：

```csharp
        _ledgeFarRay.Position = new Vector2(16f * _direction, 12f);
        _ledgeFarRay.TargetPosition = new Vector2(52f * _direction, 20f);
```

`AimRays()` 整段替换为：

```csharp
    /// <summary>按当前方向摆好三根探测线，并强制同帧刷新命中结果。</summary>
    private void AimRays()
    {
        if (_wallRay != null)
        {
            _wallRay.Position = new Vector2(0f, -2f);
            _wallRay.TargetPosition = new Vector2(28f * _direction, 0f);
            _wallRay.ForceRaycastUpdate();
        }
        if (_ledgeNearRay != null)
        {
            _ledgeNearRay.Position = new Vector2(12f * _direction, 12f);
            _ledgeNearRay.TargetPosition = new Vector2(16f * _direction, 20f);
            _ledgeNearRay.ForceRaycastUpdate();
        }
        if (_ledgeFarRay != null)
        {
            _ledgeFarRay.Position = new Vector2(4f * _direction, 12f);
            _ledgeFarRay.TargetPosition = new Vector2(60f * _direction, 20f);
            _ledgeFarRay.ForceRaycastUpdate();
        }
    }
```

- [ ] **Step 3: 攻击距离重新标定（僵持修复）**

`_attackDistance` 字段与注释整段替换为：

```csharp
    /// <summary>
    /// 与玩家的水平距离小于该值时原地出攻击（像素）。
    /// 必须小于命中可达距离 48 = 命中盒偏移 20 + 命中盒半宽 14 + 玩家受击盒半宽 14，
    /// 否则会停在射程外空挥（阶段④前 26 > 24 即僵持根因）。
    /// </summary>
    [Export]
    private float _attackDistance = 40f;
```

（`_flipCooldown`/`_jumpCooldown`/`_attackInterval` 为时间量，不变。）

- [ ] **Step 4: 格式化、构建并提交**

```bash
dotnet csharpier format .
dotnet csharpier check .
dotnet build
git add Game/Scenes/Slime.tscn Game/Gameplay/Characters/InputSources/SlimeAIInputSource.cs
git commit -m "fix: 史莱姆感知射线 ×2 并重标定攻击距离，修复追击停距大于命中距的僵持"
```

---

### Task 5: 探针适配 ×2 世界 + 新增平台/追击命中检查

**Files:**
- Modify: `Tools/headless_probe/Probe.cs`（整文件重写，内容如下）

检查从 5 项扩到 8 项：新增「跳上平台」「Slime1 追击中出招」「史莱姆命中玩家（HP 曾下降）」，
直接对应验收标准。阶段二脚本化为「助跑 → 满跳登平台 → 走出平台落回地面 → 停住」，
落点推算：14 帧助跑至 x≈170 起跳、滞空 40 帧后落在平台上 x≈290、继续右行走出右缘（x=364）
落回地面 x≈445 后停住（距窄坑左沿 512 尚有 ~55px 安全余量）。帧号为理论推算，
**若平台/机动相关 FAIL，按 `[trip]` 输出微调四个按键帧常量再跑，不得放宽断言本身**。

- [ ] **Step 1: 用以下内容整文件替换 Probe.cs**

```csharp
using System.Collections.Generic;
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Tools;

/// <summary>
/// headless 回归探针：脚本化驱动玩家输入并观察两只史莱姆，
/// 逐项输出 PROBE [PASS/FAIL]，全过退出码 0，否则 1。
/// 运行：$godot --headless --path . Tools/headless_probe/Probe.tscn
/// 阈值针对阶段④ ×2 后的世界尺度：地面站立中心 y≈642、平台顶 560（站立 y≈546）、
/// 窄坑 [512,544]、宽坑 [992,1120]。
/// </summary>
public partial class Probe : Node
{
    private const int LandFrame = 90; // 阶段一末帧：自然落地
    private const int ManeuverEndFrame = 260; // 阶段二末帧：助跑跳平台机动窗
    private const int TotalFrames = 1070; // 阶段三末帧：史莱姆观察 ~13.5s

    // 机动窗按键帧（60fps 物理帧，理论推算值；相关 FAIL 时按实测输出微调）
    private const int RunPressFrame = 91; // 开始向右助跑
    private const int JumpPressFrame = 105; // 助跑 14 帧后满跳（不提前松键）
    private const int JumpReleaseFrame = 125; // 已过最高点，松键不影响高度
    private const int RunReleaseFrame = 165; // 落回地面前后停住，避免滑进窄坑

    private const float MinJumpRise = 90f; // 满跳理论 ~111px；登平台需 96px
    private const float PlatformTopY = 600f; // 站上平台判定线（介于 546 与地面 642 之间）
    private const float PlatformMinX = 250f;
    private const float PlatformMaxX = 360f;
    private const float FellOffsetY = 50f; // 落到出生点下方 50px 视为落坑
    private const float NarrowGapX = 512f; // 窄坑左沿，过线即穿坑

    private Character _player;
    private Character _slime1;
    private Character _slime2;
    private int _frame;

    private float _playerRestY;
    private float _playerMinY = float.MaxValue;
    private float _slimeSpawnY;
    private int _playerMinHP = int.MaxValue;
    private int _slime1AttackCount;
    private bool _playerWasOnPlatform;
    private bool _slime1CrossedNarrowGap;
    private bool _slime2Fell;
    private bool _slime1Fell;
    private readonly List<string> _failures = new();

    public override void _Ready()
    {
        var level = GD.Load<PackedScene>("res://Game/Scenes/TestLevel.tscn").Instantiate();
        AddChild(level);
        _player = GetNode<Character>("TestLevel/Player");
        _slime1 = GetNode<Character>("TestLevel/Slime1");
        _slime2 = GetNode<Character>("TestLevel/Slime2");
        _slime1.Motor.AttackStarted += () => _slime1AttackCount++;
        // 物理未运行，GlobalPosition 即场景出生点；用它做落坑判定基准
        // （不能用运行中的 y 采样：巡逻跳跃中的史莱姆会污染基准）
        _slimeSpawnY = _slime1.GlobalPosition.Y;
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        _playerMinHP = Mathf.Min(_playerMinHP, _player.Health.CurrentHP);

        if (_frame <= LandFrame)
        {
            if (_frame == LandFrame)
            {
                _playerRestY = _player.GlobalPosition.Y;
            }
            return;
        }

        if (_frame <= ManeuverEndFrame)
        {
            if (_frame == RunPressFrame)
            {
                Input.ActionPress("move_right");
            }
            if (_frame == JumpPressFrame)
            {
                Input.ActionPress("jump");
            }
            if (_frame == JumpReleaseFrame)
            {
                Input.ActionRelease("jump");
            }
            if (_frame == RunReleaseFrame)
            {
                Input.ActionRelease("move_right");
            }
            _playerMinY = Mathf.Min(_playerMinY, _player.GlobalPosition.Y);
            _playerWasOnPlatform |= _player.IsOnFloor()
                && _player.GlobalPosition.Y < PlatformTopY
                && _player.GlobalPosition.X > PlatformMinX
                && _player.GlobalPosition.X < PlatformMaxX;
            return;
        }

        // 阶段三：观察史莱姆与战斗闭环
        float fellY = _slimeSpawnY + FellOffsetY;
        if (!_slime2Fell && _slime2.GlobalPosition.Y > fellY)
        {
            _slime2Fell = true;
            GD.Print(
                $"[trip] frame={_frame} S2 首次越界 y={_slime2.GlobalPosition.Y:F1} x={_slime2.GlobalPosition.X:F1}"
            );
        }
        if (!_slime1Fell && _slime1.GlobalPosition.Y > fellY)
        {
            _slime1Fell = true;
            GD.Print(
                $"[trip] frame={_frame} S1 首次越界 y={_slime1.GlobalPosition.Y:F1} x={_slime1.GlobalPosition.X:F1}"
            );
        }
        _slime1CrossedNarrowGap |= _slime1.GlobalPosition.X < NarrowGapX;

        if (_frame == TotalFrames)
        {
            Check(_player.IsOnFloor(), "玩家自然落地");
            Check(
                _playerRestY - _playerMinY >= MinJumpRise,
                $"跳跃上升 {_playerRestY - _playerMinY:F1}px ≥ {MinJumpRise:F0}px"
            );
            Check(_playerWasOnPlatform, "玩家跳上 ×2 后平台");
            Check(!_slime2Fell, "Slime2 在观察期内未落入宽沟");
            Check(!_slime1Fell, "Slime1 在观察期内未落坑");
            Check(_slime1CrossedNarrowGap, "Slime1 在观察期内穿过窄坑 (x<512)");
            Check(_slime1AttackCount > 0, "Slime1 追击中出招");
            Check(_playerMinHP < _player.Health.MaxHP, "史莱姆命中玩家（HP 曾下降）");
            Finish();
        }
    }

    private void Check(bool ok, string name)
    {
        GD.Print($"PROBE [{(ok ? "PASS" : "FAIL")}] {name}");
        if (!ok)
        {
            _failures.Add(name);
        }
    }

    private void Finish()
    {
        GD.Print($"PROBE SUMMARY {8 - _failures.Count}/8 通过");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}
```

- [ ] **Step 2: 构建、格式化并跑探针**

```bash
dotnet build
dotnet csharpier format .
dotnet csharpier check .
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
```
（`GODOT=D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe`；
首次运行会重导入 ×2 后的 PNG，耗时变长属预期。）

Expected: 8 行 `PROBE [PASS]` + `PROBE SUMMARY 8/8 通过`，退出码 0；
跳跃上升实测 ~111px。若个别 FAIL：
- 平台/跳跃类 → 微调四个按键帧常量重跑（见文件头注释）；
- 史莱姆落坑/穿沟类 → 先核对 Task 4 射线坐标是否漏改，再判断是否真缺陷；真缺陷停下记录，不得静默放宽断言；
- 出招/命中类 → 核对 `_attackDistance=40`、Slime HitShape (20,0)、vision 180 三处标定。

- [ ] **Step 3: 提交**

```bash
git add Tools/headless_probe/Probe.cs
git commit -m "test: 探针适配 ×2 世界并新增平台落地与追击命中检查（5→8 项）"
```

---

### Task 6: 全量回归、视觉抽检与路线图勾记

- [ ] **Step 1: 本地全量回归（CI 等价三连 + 探针）**

```bash
dotnet csharpier check .
dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn; echo "exit=$?"
```
Expected: 全部通过；探针 8/8、`exit=0`。

- [ ] **Step 2: 视觉抽检（人工，非阻塞）**

编辑器或 `$GODOT --path .` 运行一次：精灵 32×32 与碰撞盒贴合、脚底对齐、
构图与 16×16 时代一致、1920×1080 窗口观感无像素不均（不适则改窗口覆盖为 1280×720 并注明）。

- [ ] **Step 3: 路线图勾记**

`Docs/engineering-roadmap.md`：
- 头部「当前进度/下一步」更新为阶段④完成、下一步阶段⑤
- `### 阶段④ 32×32 美术适配（世界 ×2）` 标题追加 `✅（2026-09-06 完成）`

```bash
git add Docs/engineering-roadmap.md
git commit -m "docs: 勾记路线图阶段④完成"
```

- [ ] **Step 4: 推送并确认 CI 绿**

```bash
git push
gh run watch   # 或在 GitHub 页面确认两个 job 全绿
```
Expected: `format-check` 与 `build-and-test` 两个 job 全绿。

---

## 非目标（本期不做）

- 设计文档（character-controller / combat-state-machine）中的旧尺度数值表修订 → 归入阶段⑥文档收尾
- TileMap 正式关卡、UI、音频、新敌人（路线图 §5 不变）
- Phantom Camera 接入与震屏（阶段⑤）
