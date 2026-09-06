# 第三期实施计划：关卡加载与场景切换闭环（路线图候选 2）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 关卡自描述（相机边界 / 出口 / 下一关），Main 按线性队列加载关卡，触碰出口过场切换，末关回主菜单；相机边界随关卡走（验证宽于视口的可滚动关卡）；「主菜单 → 关卡 1 → 通关 → 关卡 2 → 回主菜单」闭环。

**Architecture:** 新增 `Level`（关卡数据根：边界 + 下一关路径 + 完成事件）与 `LevelExit`（出口 Area2D）进 `Gameplay/World/`；**相机边界从 Player.tscn 的 PCam 硬编码中移除**，改为关卡数据、由 `CameraRig` 沿祖先链发现关卡并应用（保证任何入口——Main 或探针——实例化关卡都生效）；Main 增加 fade 过渡与关卡队列。玩家每关全新实例（与现状一致）。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0、PCam `set_limit_*`（已确认存在）、CSharpier 1.3.0。

**环境事实（本机已验证）：**

- 基线：28 单元测试 + 探针 9/9 + CI 绿；出生点齐平规则已落地（TestLevel y=642）
- PCam 的 `set_limit_left/top/right/bottom` 为公开 GDScript 方法（C# 侧 `Call("set_limit_left", v)` 即可）
- 探针直接实例化 `TestLevel.tscn`（不经 Main）——关卡挂新脚本后探针路径不变，
  这正是「边界应用必须在关卡侧完成」的约束来源
- `TestLevel.tscn` 现根节点为纯 Node2D，无脚本

## 裁量点（审阅时可否决）

1. **TestLevel 即第一关，不改名不挪目录**（探针/文档引用零扰动）；第二关为 `Level2.tscn`（最小变体，验证加载与相机滚动，非正式内容）。
2. **死亡重生仍在关内**（不跨关卡、无检查点存续）；通关判定 = 触碰出口 Area2D，无结算页。
3. **关卡队 linear 写死在 Main**（数组两个字段），关卡选择 UI 不做。
4. **过渡为 0.2s 黑场淡入淡出**（Tween），不做异步加载（关卡场景极小，同步实例化即可；`LoadThreaded` 留给大关卡时代）。
5. **PCam 默认 limits 改为放宽值**（-100000..100000 即插件默认），加载关卡时按数据覆盖；无 Level 祖先的边缘情况保持默认大边界（不设限）。

---

### Task 1: Level 数据根 + LevelExit 出口

**Files:**
- Create: `Game/Gameplay/World/Level.cs`
- Create: `Game/Gameplay/World/LevelExit.cs`

- [ ] **Step 1: Level.cs（关卡数据根，挂关卡场景根节点）**

```csharp
using System;
using Godot;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 关卡数据根：世界边界（相机限制）、出口与下一关的声明。
/// 不驱动玩法——完成事件由出口触发、由应用编排（Main）消费；探针等无编排者入口可安全忽略。
/// </summary>
public partial class Level : Node2D
{
    /// <summary>世界左边界（相机 limit_left，像素）。</summary>
    [Export]
    public int LimitLeft { get; set; }

    /// <summary>世界上边界（相机 limit_top，像素）。</summary>
    [Export]
    public int LimitTop { get; set; }

    /// <summary>世界右边界（相机 limit_right，像素）。宽于视口即水平卷轴。</summary>
    [Export]
    public int LimitRight { get; set; } = 1280;

    /// <summary>世界下边界（相机 limit_bottom，像素）。</summary>
    [Export]
    public int LimitBottom { get; set; } = 720;

    /// <summary>通关后加载的下一关场景路径；空串表示末关（回主菜单）。</summary>
    [Export]
    public string NextLevelPath { get; set; } = string.Empty;

    /// <summary>玩家触碰出口时触发一次。</summary>
    public event Action Completed;

    private bool _completed;

    /// <summary>通关（出口触发；重复触碰只报一次）。</summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }
        _completed = true;
        Completed?.Invoke();
    }
}
```

- [ ] **Step 2: LevelExit.cs**

```csharp
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 关卡出口：玩家触碰即视为通关（沿祖先链通知所属 Level）。mask 由场景配置（玩家实体层）。
/// </summary>
public partial class LevelExit : Area2D
{
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Character)
        {
            return;
        }
        Node current = GetParent();
        while (current != null)
        {
            if (current is Level level)
            {
                level.Complete();
                return;
            }
            current = current.GetParent();
        }
    }
}
```

- [ ] **Step 3: 构建并提交**

```bash
dotnet csharpier format . && dotnet build
git add Game/Gameplay/World/
git commit -m "feat: Level 关卡数据根与 LevelExit 出口"
```

---

### Task 2: 相机边界随关卡（CameraRig 应用，Player 硬编码移除）

**Files:**
- Modify: `Game/Gameplay/Characters/CameraRig.cs`（Bind 时沿祖先链找 Level 应用边界）
- Modify: `Game/Scenes/Player.tscn`（PCam 的 limit_left/top/right/bottom 四行删除，load_steps 不变）
- Modify: `Game/Scenes/TestLevel.tscn`（根节点挂 Level.cs + 导出边界 0/0/1280/720 + NextLevelPath 指向 Level2）

- [ ] **Step 1: CameraRig 增加边界应用**

`Bind` 末尾追加：

```csharp
        ApplyLevelLimits();
```

新方法（放 `Snap` 附近）：

```csharp
    /// <summary>沿祖先链找关卡数据，把世界边界应用到 PCam（无 Level 祖先则保持默认）。</summary>
    private void ApplyLevelLimits()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is World.Level level && _pcam != null)
            {
                _pcam.Call("set_limit_left", level.LimitLeft);
                _pcam.Call("set_limit_top", level.LimitTop);
                _pcam.Call("set_limit_right", level.LimitRight);
                _pcam.Call("set_limit_bottom", level.LimitBottom);
                return;
            }
            current = current.GetParent();
        }
    }
```

- [ ] **Step 2: Player.tscn 移除硬编码边界；TestLevel 挂 Level**

Player.tscn 的 `PlayerPhantomCamera2D` 节点删除这四行：`limit_left/limit_top/limit_right/limit_bottom`。
TestLevel.tscn 加 ext_resource（Level.cs）并在根节点挂脚本与导出：

```ini
[ext_resource type="Script" path="res://Game/Gameplay/World/Level.cs" id="4_level"]

[node name="TestLevel" type="Node2D"]
script = ExtResource("4_level")
LimitLeft = 0
LimitTop = 0
LimitRight = 1280
LimitBottom = 720
NextLevelPath = "res://Game/Scenes/Level2.tscn"
```

（load_steps +1；探针的 GetNode 路径不受根节点挂脚本影响。）

- [ ] **Step 3: 验证与提交**

探针 9/9（探针玩家边界由 TestLevel 数据应用，行为与原硬编码一致）：

```bash
dotnet csharpier format . && dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
git add -A && git commit -m "feat: 相机边界改为关卡数据（CameraRig 沿祖先链应用），Player 硬编码移除"
```

---

### Task 3: 关卡出口落地 + Main 关卡队列与过渡

**Files:**
- Modify: `Game/Scenes/TestLevel.tscn`（加 LevelExit 于 GroundC 右端 x≈1240）
- Modify: `Game/UI/Main.cs`（关卡队列 + 通关接续 + 黑场过渡）
- Modify: `Game/Scenes/Main.tscn`（UiLayer 下加 Fade 层）

- [ ] **Step 1: TestLevel 出口**

```ini
[ext_resource type="Script" path="res://Game/Gameplay/World/LevelExit.cs" id="5_exit"]

[node name="LevelExit" type="Area2D" parent="."]
position = Vector2(1240, 600)
collision_layer = 0
collision_mask = 1
script = ExtResource("5_exit")

[node name="Shape" type="CollisionShape2D" parent="LevelExit"]
shape = SubResource("exit_shape")
```

`exit_shape` RectangleShape2D (48, 96)，占位即可（正式美术期换门形）。

- [ ] **Step 2: Main 关卡队列与过渡**

Main.cs 改造要点（保持既有事件接线不动）：

```csharp
    private static readonly string[] LevelPaths =
    [
        "res://Game/Scenes/TestLevel.tscn",
        "res://Game/Scenes/Level2.tscn",
    ];

    private int _levelIndex;
    private ColorRect _fade;
    private bool _transitioning;

    // StartGame → LoadLevel(0)；StartGame 内订阅关卡完成：
    private void LoadLevel(int index)
    {
        _levelIndex = index;
        _level = GD.Load<PackedScene>(LevelPaths[index]).Instantiate();
        _level.Name = "Level";
        _levelRoot.AddChild(_level);
        if (_level is World.Level level)
        {
            level.Completed += OnLevelCompleted;
        }
        var player = _levelRoot.GetNode<Character>("Level/Player");
        _hud = GD.Load<PackedScene>("res://Game/UI/Hud.tscn").Instantiate<Hud>();
        GetNode("UiLayer").AddChild(_hud);
        _hud.Bind(player.Health);
    }

    private async void OnLevelCompleted()
    {
        if (_transitioning)
        {
            return;
        }
        _transitioning = true;
        await FadeAsync(1f);                       // 黑场
        _level?.QueueFree();
        _level = null;
        if (_hud != null) { _hud.QueueFree(); _hud = null; }
        bool hasNext = _levelIndex + 1 < LevelPaths.Length;
        if (hasNext)
        {
            LoadLevel(_levelIndex + 1);
        }
        else
        {
            BackToMainMenu();
        }
        await FadeAsync(0f);                       // 亮场
        _transitioning = false;
    }

    private async Godot.Collections.Array FadeAsync(float targetAlpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "color:a", targetAlpha, 0.2);
        return await ToSignal(tween, Tween.SignalName.Finished);
    }
```

注意：`BackToMainMenu` 在过渡中调用时不重复淡出（黑场下切菜单）；`_UnhandledInput` 的 Esc
在 `_transitioning` 期间忽略。`Main.tscn` UiLayer 下加：

```ini
[node name="Fade" type="ColorRect" parent="UiLayer"]
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
color = Color(0, 0, 0, 0)
mouse_filter = 2
```

（`mouse_filter=2` 忽略鼠标，透明时不遮挡；Main._Ready 缓存 `_fade` 引用。）

- [ ] **Step 3: 构建冒烟与提交**

```bash
dotnet csharpier format . && dotnet build
timeout 12 "$GODOT" --headless --path . Game/Scenes/Main.tscn 2>&1 | grep -iE "error|exception"   # 菜单加载无报错
git add -A && git commit -m "feat: 关卡队列与黑场过渡（通关接续下一关，末关回主菜单）"
```

---

### Task 4: Level2（验证关卡加载与相机滚动）

**Files:**
- Create: `Game/Scenes/Level2.tscn`

- [ ] **Step 1: 关卡内容（最小变体但必须证明两件事：加载切换、宽视口边界滚动）**

世界宽 1600（相机需水平卷轴），布局：

| 节点/资源 | 值 |
|---|---|
| 根 Level（挂 Level.cs） | Limit 0/0/**1600**/720；NextLevelPath = ""（末关→回菜单） |
| Sky | 1600×720 |
| GroundA | size (800, 64) @ (400, 688)，跨 0–800 |
| 宽坑 | 800–928（128px） |
| GroundB | size (672, 64) @ (1264, 688)，跨 928–1600 |
| Platform | size (96, 32) @ (560, 576) |
| 墙 | 左 (-16, 360)、右 (1616, 360)，size (32, 1600) |
| PitFloor / KillZone | @ (800, 1000) / (800, 960)，横向覆盖 0–1600 |
| Slime1 | @ (1080, 642) |
| Player | @ (128, 642)（齐平规则） |
| LevelExit | @ (1544, 600) |
| Fill/Top 色块 | 与 TestLevel 同风格，随几何 ×2 对应 |

（Fill/Top 的 offsets 按 TestLevel 现值等比换算：GroundA Fill ±400/±32、Top -32/-24，以此类推。）

- [ ] **Step 2: 编辑器实跑验证并提交**

主菜单开始 → 通关 TestLevel（触碰出口）→ 黑场 → Level2 加载（相机随玩家横向滚动、
边界 1600 生效、玩家不越界）→ 通关 Level2 → 回主菜单。落坑死亡重生在两关内各自正常。

```bash
git add -A && git commit -m "feat: 第二关卡（宽视口卷轴，验证关卡加载与相机边界随关卡）"
```

---

### Task 5: 文档同步、全量回归与勾记

- [ ] **Step 1: 文档**

- `architecture.md`：§1 目录注明 Levels 相关；§8 新增敌人指南补「出生齐平 + 关卡根挂 Level.cs、
  填 Limit 与 NextLevelPath」；新增 §12「关卡系统」小节（Level/LevelExit/CameraRig 边界链路、
  关卡队列与过渡、新增一关的步骤 checklist）
- `camera-design.md`：边界一节改写（「边界属于关卡数据，Player 不再持有；CameraRig 加载时应用」）
- `README.md`：玩法段补「触碰出口进入下一关」；文档履历更新
- `engineering-roadmap.md`：候选 2 标注 ✅（第三期交付）；§5.1 追加第三期小节

- [ ] **Step 2: 全量回归（CI 等价三连 + 探针 9/9）并推送**

```bash
dotnet csharpier check . && dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn; echo "exit=$?"
git add -A && git commit -m "docs: 第三期文档同步与路线图勾记" && git push
gh run view $(gh run list --limit 1 --json databaseId --jq '.[0].databaseId')
```

- [ ] **Step 3: 实机验收（用户参与）**

完整走查：菜单 → 关卡 1（探索/战斗/落坑重生）→ 触碰出口过场 → 关卡 2（相机卷轴、
边界不越界）→ 通关回主菜单；期间 Esc 暂停、HUD、震屏、死亡重生均正常。

---

## 非目标（本期不做）

- 关卡选择 UI、关卡解锁存档、Game Over 结算页
- 异步/流式加载（关卡体量增大后引入 `ResourceLoader.LoadThreaded`）
- 正式关卡内容设计（Level2 仅为验证用最小变体）与 TileMap 关卡格式
- AI 无敌期攻击问题（已记录，随 AI 行为完善处理）
