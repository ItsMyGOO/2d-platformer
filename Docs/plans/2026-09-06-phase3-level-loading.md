# 第三期实施计划：穿透碰撞 + 接触伤害 + 九宫格房间流式世界

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **本计划取代 2026-09-06 的初版第三期计划**（离散关卡 Level/队列/黑场/LevelExit 方案）——
> 经讨论确定：碰撞模型采用「全体穿透 + 接触伤害」，关卡加载采用「九宫格房间流式」
> （银河恶魔城式无缝世界的最小落地）。

**Goal:** 单位间全穿透（消除互挤/站头），敌人接触伤害常态化；世界改为 1280×720 房间网格，
玩家所在格 ±1（3×3 窗口）的房间在场景树、其余缓存（节点不销毁，状态保留），
相机边界升为世界级，主菜单 → 无缝世界 → 死亡重生（当前房间出生点）闭环。

**Architecture:** 物理层规范化（5 层各司其职，顺带修复层值/层名错位）；新增 `ContactDamager`
常驻接触伤害组件；新增 `World`（Node2D，房间流式管理器）与房间规范（Spawn 标记、门口开口）；
玩家成为世界的持久住民（不再是房间子节点）。状态机/帧序零改动。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0、PCam `set_limit_*`、CSharpier 1.3.0。

**环境事实（本机已验证）：**

- 基线：28 单元测试 + 探针 9/9 + CI 绿；出生点齐平规则已落地
- **现层值与层名错位**（审计发现）：玩家受击盒实际值 3、敌人受击盒实际值 4，是「值当层号」配的；
  project.godot 声明的 layer_3=player_hurtbox 并未被真正使用——本期一并归位
- 世界地形（StaticBody2D 默认）与玩家身体**共用 layer 1**，是互挤/站头的根因；
  穿透必须先把地形与玩家分izku Baba层
- `ReceiveHit`/`OnHurt` 已返回 bool（第二期 F2），`Health` 无敌帧天然限频——接触伤害直接复用
- 探针直接实例化关卡场景；改为实例化 World 后需同步改节点路径

**物理层规范（本期生效，全项目以此为准）：**

| 层号 | 位值 | 名称 | 用途 |
|---|---|---|---|
| 1 | 1 | terrain | 世界地形（唯一与身体碰撞的层） |
| 2 | 2 | enemy_body | 敌人实体 |
| 3 | 4 | player_hurtbox | 玩家受击盒 |
| 4 | 8 | enemy_hurtbox | 敌人受击盒 |
| 5 | 16 | player_body | 玩家实体（新） |

掩码：身体 mask=1（只撞地形→单位全穿透）；玩家 Hitbox mask=8；敌人 Hitbox/ContactDamager
mask=4；ChaseDetector mask=16；KillZone mask=1+2+16=19。

## 裁量点（审阅时可否决）

1. **接触伤害数值复用 AttackConfig**（史莱姆 1 伤害、击退 280/200）——贴脸威胁与挥击同源，
   不新增配置面；将来需要差异化再拆。
2. **房间尺寸 1280×720**（单屏一格）；门口开口宽 96px、贴地面高 160px，**边界墙体由左侧房间承担**
   （右侧房间不建左墙）——3×3 窗口数学上保证边界两侧房间同时在场，开口永远有效。
3. **验证世界为 2 间房**（TestLevel 房间化 + 新建 RoomB，线性排列）；2×2 及以上留待正式内容。
4. **重生点 = 当前房间的 Spawn 标记**（World 在玩家跨格时更新玩家重生点）；无存档系统。
5. **移出树用 `CallDeferred("remove_child"/"add_child")`**，房间节点永久缓存不销毁——
   击杀/位置状态天然保留；内存按测试世界规模可忽略。
6. **玩家离开房间格子才切窗**（不做预判），探针/实机验证窗口切换的几何正确性。
7. 相机边界硬编码从 Player.tscn 移除，改为 World 导出（缺 World 祖先时保持插件默认大边界）。

---

### Task 1: 物理层规范化与单位穿透

**Files:**
- Modify: `project.godot`（layer_names：layer_1 改名 terrain，新增 layer_5=player_body）
- Modify: `Game/Scenes/BaseCharacter.tscn`（Hurtbox layer 3→4；Hitbox mask 4→8）
- Modify: `Game/Scenes/Player.tscn`（根节点加 `collision_layer = 16`）
- Modify: `Game/Scenes/Slime.tscn`（Hitbox mask 3→4；Hurtbox layer 4→8；ChaseDetector mask 1→16）
- Modify: `Game/Scenes/TestLevel.tscn`（KillZone mask 3→19）

- [ ] **Step 1: 按上表逐项改值**（project.godot `[layer_names]` 段：

```ini
[layer_names]

2d_physics/layer_1="terrain"
2d_physics/layer_2="enemy_body"
2d_physics/layer_3="player_hurtbox"
2d_physics/layer_4="enemy_hurtbox"
2d_physics/layer_5="player_body"
```

- [ ] **Step 2: 构建、探针 9/9、提交**

（本任务无行为变化预期：掩码配对等值迁移。史莱姆不再与玩家实体碰撞即穿透达成。）

```bash
dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
git add -A && git commit -m "feat: 物理层规范化并改为单位间穿透（消除互挤/站头）"
```

---

### Task 2: 接触伤害（ContactDamager）

**Files:**
- Create: `Game/Gameplay/Characters/Combat/ContactDamager.cs`
- Modify: `Game/Scenes/Slime.tscn`（挂 ContactDamager，body 同尺寸矩形）
- Modify: `Game/Gameplay/Characters/Character.cs`（下发接触伤害数值，同 AttackConfig）

- [ ] **Step 1: ContactDamager.cs**

```csharp
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 常驻接触伤害：每物理帧轮询重叠的 Hurtbox 并尝试结算——
/// Health 无敌帧天然限频（每次受击后 0.8s 内重复接触无效）。
/// 与挥击 Hitbox 互不影响；数值由编排者从配置下发。
/// </summary>
public partial class ContactDamager : Area2D
{
    private int _damage;
    private float _knockbackHorizontal;
    private float _knockbackVertical;

    /// <summary>由编排者下发数值（来源与挥击相同的 AttackConfig）。</summary>
    public void Configure(int damage, float knockbackHorizontal, float knockbackVertical)
    {
        _damage = damage;
        _knockbackHorizontal = knockbackHorizontal;
        _knockbackVertical = knockbackVertical;
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (Area2D area in GetOverlappingAreas())
        {
            if (area is Hurtbox hurtbox)
            {
                float direction = MathF.Sign(hurtbox.GlobalPosition.X - GlobalPosition.X);
                if (direction == 0f)
                {
                    direction = 1f;
                }
                hurtbox.ReceiveHit(
                    DamageInfo.Create(_damage, _knockbackHorizontal, _knockbackVertical, direction)
                ); // 拒伤（无敌/尸体）由 Health 静默吞掉，接触伤害无命中确认事件
            }
        }
    }
}
```

（需 `using System;`。）

- [ ] **Step 2: Slime.tscn 挂载 + Character 下发**

Slime.tscn 新增（与身体同尺寸的矩形 Area2D，mask=4，layer=0）：

```ini
[node name="ContactDamager" type="Area2D" parent="."]
collision_layer = 0
collision_mask = 4
script = ExtResource("13_contact")

[node name="Shape" type="CollisionShape2D" parent="ContactDamager"]
shape = SubResource("contact_shape")
```

（`contact_shape` RectangleShape2D (24, 28)，与身体一致；load_steps 同步。）
Character._Ready 在 Configure 攻击的同一段落补：

```csharp
        if (_config.Attack != null)
        {
            _hitbox?.Configure(...);           // 既有
            this.FindDescendant<ContactDamager>()?
                .Configure(_config.Attack.Damage,
                    _config.Attack.KnockbackHorizontal,
                    _config.Attack.KnockbackVertical);
        }
```

- [ ] **Step 3: 验证与提交**

实机：贴脸史莱姆约 0.8s 掉 1 血并被弹开；受击震屏自动生效（走 Damaged）。
探针 9/9（跑坑段可能因接触伤害减速，帧常量按 `[trip]` 输出微调）：

```bash
dotnet csharpier format . && dotnet build && dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
git add -A && git commit -m "feat: 敌人接触伤害（ContactDamager 常驻，无敌帧限频）"
```

---

### Task 3: World 房间流式管理器

**Files:**
- Create: `Game/Gameplay/World/World.cs`
- Modify: `Game/Gameplay/Characters/Character.cs`（`SetSpawnPoint`）
- Modify: `Game/Gameplay/Characters/CameraRig.cs`（Level 改 World，应用世界边界）
- Modify: `Game/Scenes/Player.tscn`（PCam 四行硬编码 limits 删除）

- [ ] **Step 1: World.cs**

```csharp
using System.Collections.Generic;
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 九宫格房间流式管理：世界 = 1280×720 房间网格（Rooms 子节点按网格坐标摆放）。
/// 玩家所在格 ±1 的房间在树，其余 RemoveChild 缓存（节点不销毁——击杀/位置状态保留，
/// 回窗即恢复）。跨格时更新玩家重生点为当前房间的 Spawn 标记。
/// </summary>
public partial class World : Node2D
{
    /// <summary>房间网格尺寸（像素）。</summary>
    [Export]
    public Vector2I RoomSize { get; set; } = new(1280, 720);

    /// <summary>世界相机边界（四值，加载时由 CameraRig 应用）。</summary>
    [Export]
    public int LimitLeft { get; set; }

    [Export]
    public int LimitTop { get; set; }

    [Export]
    public int LimitRight { get; set; } = 1280;

    [Export]
    public int LimitBottom { get; set; } = 720;

    private Node2D _rooms;
    private Character _player;
    private readonly Dictionary<Vector2I, Node2D> _roomsByCell = new();
    private Vector2I _currentCell;

    public override void _Ready()
    {
        _rooms = GetNode<Node2D>("Rooms");
        foreach (Node2D room in _rooms.GetChildren())
        {
            var cell = new Vector2I(
                (int)(room.Position.X / RoomSize.X),
                (int)(room.Position.Y / RoomSize.Y)
            );
            _roomsByCell[cell] = room;
        }
        _player = GetNode<Character>("Player");
        _currentCell = CellOf(_player.GlobalPosition);
        ApplyWindow();
        if (_roomsByCell.TryGetValue(_currentCell, out Node2D room))
        {
            _player.SetSpawnPoint(SpawnOf(room));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2I cell = CellOf(_player.GlobalPosition);
        if (cell == _currentCell)
        {
            return;
        }
        _currentCell = cell;
        ApplyWindow();
        if (_roomsByCell.TryGetValue(cell, out Node2D room))
        {
            _player.SetSpawnPoint(SpawnOf(room));
        }
    }

    private Vector2I CellOf(Vector2 global) => new(
        (int)(global.X / RoomSize.X),
        (int)(global.Y / RoomSize.Y)
    );

    private Vector2 SpawnOf(Node2D room) =>
        room.GetNodeOrNull<Marker2D>("Spawn")?.GlobalPosition ?? room.GlobalPosition;

    private void ApplyWindow()
    {
        foreach ((Vector2I cell, Node2D room) in _roomsByCell)
        {
            bool inWindow =
                Math.Abs(cell.X - _currentCell.X) <= 1
                && Math.Abs(cell.Y - _currentCell.Y) <= 1;
            bool inTree = room.GetParent() != null;
            if (inWindow && !inTree)
            {
                _rooms.CallDeferred("add_child", room);
            }
            else if (!inWindow && inTree)
            {
                _rooms.CallDeferred("remove_child", room);
            }
        }
    }
}
```

- [ ] **Step 2: Character.SetSpawnPoint**

```csharp
    /// <summary>更新重生点（世界流式下由房间管理器在跨格时调用；默认为初始出生点）。</summary>
    public void SetSpawnPoint(Vector2 globalPosition) => _spawnPosition = globalPosition;
```

- [ ] **Step 3: CameraRig 边界来源改 World**

`ApplyLevelLimits` 重命名 `ApplyWorldLimits`，祖先链匹配 `World.World`，四值
`set_limit_left/top/right/bottom`；Player.tscn PCam 删除 `limit_left/top/right/bottom` 四行。

- [ ] **Step 4: 构建提交**（World 尚无场景，纯编译级验证）

```bash
dotnet csharpier format . && dotnet build
git add -A && git commit -m "feat: World 九宫格房间流式管理器与重生点随房间"
```

---

### Task 4: 世界场景与两个房间（探针切 World）

**Files:**
- Create: `Game/Scenes/World.tscn`（World 根 + Rooms/ + 两个房间实例 + Player 实例）
- Create: `Game/Scenes/RoomB.tscn`（第二房间）
- Modify: `Game/Scenes/TestLevel.tscn`（房间化：加 Spawn 标记；右墙改为带门口开口两段）
- Modify: `Tools/headless_probe/Probe.cs`（实例化 World，节点路径改 World/Player 等）

- [ ] **Step 1: TestLevel 房间化**

- 右墙 `WallRight`（整墙 32×1600 @ (1296,360)）改为两段：上段 size (32, 496) @ (1296, 248)
  （跨 y 0–496），y 496–656 留作门口开口（宽 96 需两房间错位互补——A 的开口在 x 1280–1312 墙上
  开 96px 高 160px：上段 + 下段 size (32, 1600-496-160=944)?? **以实跑几何为准**：
  开口垂直范围 y 496–656（地面之上 160px），A 右墙 = 上段 (32,496)@y248 + 下段 (32,944)@y1128。
  RoomB **不建左墙**（边界墙由左房承担）。
- 根节点下加 `[node name="Spawn" type="Marker2D"] position = Vector2(128, 642)`。

- [ ] **Step 2: RoomB.tscn（第二房间，x 世界 1280–2560）**

布局：地面 GroundA size (1280, 64) @ (1920, 688)（跨 1280–2560，地面顶同为 656）；
平台 (1900, 576)；右墙 (2576, 360) 整墙；左墙无；KillZone (1920, 960) size (1600, 64)；
Slime1 @ (1900, 642)；Spawn @ (1408, 642)；Sky 1280×720 @ offset (1280, 0)（纯装饰色块）。

- [ ] **Step 3: World.tscn**

```ini
[node name="World" type="Node2D"]
script = ExtResource("world")
LimitLeft = 0
LimitTop = 0
LimitRight = 2560
LimitBottom = 720

[node name="Rooms" type="Node2D" parent="."]

[node name="TestLevel" parent="Rooms" instance=ExtResource("room_a")]   # 位置 (0,0)

[node name="RoomB" parent="Rooms" instance=ExtResource("room_b")]      # 位置 (1280,0)

[node name="Player" parent="." instance=ExtResource("player")]          # @ (128, 642)
```

- [ ] **Step 4: 探针切 World**

Probe._Ready 改实例化 `World.tscn`；路径：`World/Player`、`World/Rooms/TestLevel/Slime1`、
`World/Rooms/TestLevel/Slime2`。断言不变（房间 A 几何 = 原 TestLevel）。注意 World 流式
会把 RoomB 移出树——探针无感。若落坑/跑坑帧数漂移按 `[trip]` 微调。

- [ ] **Step 5: 验证与提交**

```bash
dotnet csharpier format . && dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn   # 9/9
git add -A && git commit -m "feat: 两房间世界与九宫格流式（跨房间无缝行走、状态缓存）"
```

---

### Task 5: Main 接入世界

**Files:**
- Modify: `Game/UI/Main.cs`（StartGame 实例化 `World.tscn`；玩家路径 `LevelRoot/World/Player`）

- [ ] **Step 1: 替换加载路径并删除关卡队列残留**（第二期未实装队列——直接换路径即可），

菜单 → 开始 → 无缝世界（无过场）；回主菜单释放 World。headless 冒烟（菜单无报错）+
探针 9/9 + 提交：

```bash
dotnet csharpier format . && dotnet build
timeout 12 "$GODOT" --headless --path . Game/Scenes/Main.tscn 2>&1 | grep -iE "error|exception"
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
git add -A && git commit -m "feat: 主菜单接入无缝世界"
```

---

### Task 6: 文档同步、全量回归与勾记

- [ ] **Step 1: 文档**

- `architecture.md`：§1 目录补 `World`；**§4 物理层表格更新为五层规范**（并记录历史错位已修复）；
  新增 §12「世界与房间流式」（网格/窗口/缓存/重生点语义/新增一个房间的 checklist：
  复制房间场景 → 边界几何对齐 → 摆进 World.tscn 网格坐标 → Spawn 标记）
- `camera-design.md`：边界一节改为「世界级边界，World 导出，CameraRig 应用」
- `README.md`：玩法段（穿透/接触伤害/无缝房间）；操作表不变
- `engineering-roadmap.md`：候选 2 标注 ✅（第三期：九宫格房间流式）；§5.1 追加第三期小节
  （穿透+接触伤害 / 房间流式两项）

- [ ] **Step 2: 全量回归（CI 等价三连 + 探针 9/9）并推送**

```bash
dotnet csharpier check . && dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn; echo "exit=$?"
git add -A && git commit -m "docs: 第三期文档同步与路线图勾记" && git push
gh run view $(gh run list --limit 1 --json databaseId --jq '.[0].databaseId')
```

- [ ] **Step 3: 实机验收（用户参与）**

完整走查：主菜单 → 世界（与史莱姆贴脸验证接触伤害节奏/弹开）→ 穿过门口进 RoomB
（无缝、无黑场；回走验证 RoomA 状态保留——被击杀过的史莱姆不再复活）→ RoomB 落坑重生
（回 RoomB 出生点、相机瞬移）→ 回主菜单。Esc 暂停、HUD、命中/受击震屏正常。

---

## 非目标（本期不做）

- 房间内敌人冻结策略（3×3 全模拟，规模大了再引入休眠）
- 跨房间持久化存档、小地图、房间解锁/锁门机制
- 世界规模扩充（正式房间内容设计）与 TileMap
- AI 无敌期攻击问题（已记录路线图，随 AI 行为完善处理）
