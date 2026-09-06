# 第二期实施计划：UI 闭环 + 落坑死亡 + 战斗代码审计加固

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 落地路线图 §5 候选 1「UI 框架与开始游戏闭环」（主菜单/暂停/HUD 血条，菜单→游玩→死亡→重开完整闭环），补上「掉坑不死」的缺陷，并按商业级中小型标准审计加固战斗/生命/攻击代码。

**Architecture:** 新增 `Game/UI/`（主菜单/暂停/HUD，CanvasLayer + 轻量 C# 脚本）与 `Game/Scenes/Main.tscn`（根编排：菜单↔关卡切换、暂停树）；新增 `Gameplay/World/KillZone.cs`（落坑即死）；战斗侧四处加固（见审计报告），均为小切口修改，状态机与帧序不动。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0、xUnit、CSharpier 1.3.0、Phantom Camera（重生瞬移用其 `teleport_position()`）。

**环境事实（本机已验证）：**

- 基线：25 单元测试 + 探针 8/8 + CI 绿；工程地基六阶段（⓪-⑥）已收官
- **PCam 有 `teleport_position()`**（phantom_camera_2d.gd:1329）——重生瞬移相机有现成 API
- `AttackConfig.Damage/Knockback*` 在 Combat 中只在 `ToData()` 映射里被读（grep 全仓确认）——
  实际结算用的是 `Hitbox` 自己的导出字段，即审计发现 F1（死配置）
- 主场景切换不影响探针：Probe.tscn 直接实例化 TestLevel，不经 Main

---

## 战斗/生命/攻击代码审计报告（商业级中小型标准）

### 已达标（审计确认，保持现状）

1. **伤害结算单一路径**：Hitbox → Hurtbox → Character.OnHurt → Health.TryApplyDamage，无旁路
2. **一挥一击**：HashSet 去重，同一次挥击对同一目标只结算一次
3. **拒伤规则完整**：无敌帧拒伤、死亡拒伤；拒伤时不进硬直（OnHurt 先过 Health 再 ForceHurt）
4. **打断安全**：受击打断冲刺/攻击；攻击被打断时 Exit 确保判定窗口关闭；死亡关闭判定
5. **状态互斥由结构保证**：只有 Grounded/Airborne 路由主动意图，行为状态互不读意图
6. **逻辑层零引擎依赖**：Motor/States/Health/DamageInfo 纯 C#，25 个单元测试直接驱动
7. **时长类全部配置驱动**（三段窗口/冷却/硬直/无敌帧），代码无魔法时长
8. **事件驱动表现层**：Jumped/Landed/AttackStarted/AttackActiveChanged/HitConfirmed/Damaged/Died
9. 击退方向由攻击者→受击者位置推导；死亡击退免疫；死亡小跳有界

### 不达标（本期修复）

| # | 问题 | 证据 | 修复 |
|---|---|---|---|
| F1 | **配置脱节（最严重）**：`AttackConfig.Damage/KnockbackHorizontal/KnockbackVertical` 是死配置——结算实际用 `Hitbox` 自己的导出字段（默认 1/160/120），.tres 里配的 360/240（玩家）与 280/200（史莱姆）从不生效。调配置改不动伤害 = 商业级红线 | grep 全仓：`.Damage` 仅出现于 ToData 映射与 Health 结算 | 单一事实源：`Character._Ready` 将 `_config.Attack` 的伤害/击退下发给 Hitbox（`Configure`），删除 Hitbox 的三个 `[Export]` 字段。**行为变化**：击退从此按配置生效（变大），探针回归兜底 |
| F2 | **命中确认语义不严**：`Hitbox` 在 `ReceiveHit` 后无条件触发 `HitConfirmed`——打尸体/无敌目标也被拒伤却照样震屏 | Hitbox._PhysicsProcess | `ReceiveHit`/`OnHurt` 返回是否真实结算，仅结算成功才触发 `HitConfirmed` |
| F3 | **缺统一生命变化通知**：HUD 只能用 Damaged/Died 拼凑，回满血（重生）无事件 | Health.cs | 新增 `HealthChanged(int currentHP)`：受伤结算、回满（RestoreFull）时触发；Damaged/Died 保留兼容 |
| F4 | **重生相机平滑飞越**：玩家重生瞬移后阻尼相机横穿全图数秒 | Character.Respawn 只复位玩家 | `Character` 增加 `Respawned` 事件；Player 场景加 `CameraRig`（表现层）订阅并调用 PCam `teleport_position()` |

### 明确不做（本期非目标）

连击/蓄力、hitstop 顿帧、伤害数字、暴击/属性、音效接线、Game Over 结算页
（死亡沿用现有自动重生设计——探针依赖且体验成立，主菜单闭环已覆盖「死亡→重开」）。

---

## 裁量点（审阅时可否决）

1. **落坑死亡不扣血**：KillZone 走独立即死通道（`Character.KillInstantly`，复用死亡动画/重生流程），不经过 Health——坠落死与战斗死是两种死法，商业惯例分列。
2. **史莱姆也落坑即死**（同一 KillZone，mask 含实体层 2）；尸体走既有 1s 消失流程。
3. **死亡→重开 = 现有自动重生**：不加 Game Over 页；主菜单闭环为「菜单→游玩→暂停→继续/回主菜单」。
4. **HUD 心形用占位图**：美术脚本新增 heart.png（16×16 网格 ×2 = 32×32），后续正式美术替换。
5. **探针 8→9 项**：新增「玩家落坑死亡并重生回出生点、血量回满」端到端检查，总帧数 1070→约 1370。
6. **UI 为占位级**：Label + Button 默认主题，布局可用即可；主题美化不在本期。
7. **`Game/Gameplay/World/` 新目录**放 KillZone（关卡类玩法的归属，为候选 2 关卡系统预铺命名空间）。

---

### Task 1: 战斗加固 F1-F3

**Files:**
- Modify: `Game/Gameplay/Characters/Combat/Hitbox.cs`（删除伤害导出字段，加 `Configure`；命中确认按结算结果）
- Modify: `Game/Gameplay/Characters/Combat/Hurtbox.cs`（`ReceiveHit` 返回 bool）
- Modify: `Game/Gameplay/Characters/Character.cs`（下发攻击数值；`OnHurt` 返回 bool）
- Modify: `Game/Gameplay/Characters/Combat/Health.cs`（`HealthChanged` 事件）
- Modify: `Tests/UnitTests/HealthTests.cs`（HealthChanged 用例 ×2）

- [ ] **Step 1: Hitbox 单一事实源 + 结算确认**

删除 `_damage/_knockbackHorizontal/_knockbackVertical` 三个 `[Export]` 字段，改为：

```csharp
    private int _damage;
    private float _knockbackHorizontal;
    private float _knockbackVertical;

    /// <summary>由编排者下发攻击数值（来源 AttackConfig，唯一事实源）。</summary>
    public void Configure(int damage, float knockbackHorizontal, float knockbackVertical)
    {
        _damage = damage;
        _knockbackHorizontal = knockbackHorizontal;
        _knockbackVertical = knockbackVertical;
    }
```

命中循环改为按结算结果触发：

```csharp
            if (area is Hurtbox hurtbox && _hitThisSwing.Add(hurtbox))
            {
                float direction = MathF.Sign(hurtbox.GlobalPosition.X - GlobalPosition.X);
                if (direction == 0f)
                {
                    direction = 1f;
                }
                if (
                    hurtbox.ReceiveHit(
                        DamageInfo.Create(_damage, _knockbackHorizontal, _knockbackVertical, direction)
                    )
                )
                {
                    HitConfirmed?.Invoke(); // 只在真实结算（非无敌/非尸体）时确认命中
                }
            }
```

`HitConfirmed` 的 XML doc 同步改为「真实结算成功后触发」。

- [ ] **Step 2: Hurtbox/Character 返回结算结果**

`Hurtbox.ReceiveHit` → `public bool ReceiveHit(DamageInfo info) => _character != null && _character.OnHurt(info);`
`Character.OnHurt` → `public bool OnHurt(DamageInfo info) => Health.TryApplyDamage(info) && (Motor.ForceHurt(info), true).Item2;`
——拆开写清晰版：

```csharp
    /// <summary>受击入口：先过无敌帧与死亡判定，结算成功才进硬直。返回是否真实结算。</summary>
    public bool OnHurt(DamageInfo info)
    {
        if (!Health.TryApplyDamage(info))
        {
            return false;
        }
        Motor.ForceHurt(info);
        return true;
    }
```

- [ ] **Step 3: Character._Ready 下发攻击数值（Presenter 绑定之前）**

```csharp
        if (_config.Attack != null)
        {
            _hitbox?.Configure(
                _config.Attack.Damage,
                _config.Attack.KnockbackHorizontal,
                _config.Attack.KnockbackVertical
            );
        }
```

- [ ] **Step 4: Health.HealthChanged 事件**

```csharp
    /// <summary>生命值发生任何变化（受伤结算 / 回满）时触发，携带当前血量。</summary>
    public event Action<int> HealthChanged;
```

`TryApplyDamage` 成功分支与 `RestoreFull` 末尾各加 `HealthChanged?.Invoke(CurrentHP);`
（`Died` 分支不触发——血量归零由 Died 表达，避免双重通知）。

- [ ] **Step 5: 单元测试**

`HealthTests` 增加两个用例：受伤触发 HealthChanged 且参数为扣血后血量；致命一击不触发
HealthChanged（只触发 Died）；RestoreFull 触发且参数为 MaxHP。

- [ ] **Step 6: 格式化、构建、全测、探针（8/8）并提交**

```bash
dotnet csharpier format . && dotnet csharpier check .
dotnet build && dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
git add -A && git commit -m "fix: 攻击数值接通配置单一事实源，命中确认改为真实结算触发，新增生命变化事件"
```
注意：击退按配置生效后史莱姆/玩家互殴弹开更远，探针 8 项仍须全绿（动态已预演，兜底在此）。

---

### Task 2: 落坑即死

**Files:**
- Create: `Game/Gameplay/World/KillZone.cs`（新命名空间 `GodotGameTemplate.Gameplay.World`）
- Modify: `Game/Scenes/TestLevel.tscn`（加 KillZone 节点）
- Modify: `Game/Gameplay/Characters/Character.cs`（`KillInstantly` + `Respawned` 为 Task 5 预留）
- Modify: `Tools/headless_probe/Probe.cs`（8→9 项）

- [ ] **Step 1: KillZone.cs**

```csharp
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 即死区：进入的任何角色立即死亡（不经过血量——坠落死与战斗死分列）。
/// 敌人走既有尸体流程后消失，玩家回出生点重生。关卡自持，通常置于关卡底部之外。
/// </summary>
public partial class KillZone : Area2D
{
    public override void _Ready()
    {
        // mask 由场景配置：实体层 1|2
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Character character)
        {
            character.KillInstantly();
        }
    }
}
```

- [ ] **Step 2: Character.KillInstantly（重构 OnDied 共用）**

```csharp
    /// <summary>环境即死（落坑等）：不经过血量，直接进入死亡流程。</summary>
    public void KillInstantly()
    {
        if (Motor.VisualState == CharacterVisualState.Dead)
        {
            return;
        }
        Motor.Kill();
        _hitbox?.SetActive(false);
        _deathCountdown = DeathDuration;
    }
```

`OnDied` 改为调用同样的死亡调度（抽出私有 `DieAndSchedule()` 共用，`Health.Died` 订阅处保留）。
同时新增 `public event Action Respawned;`（Task 5 的相机瞬移订阅用），`Respawn()` 末尾触发。

- [ ] **Step 3: TestLevel 加 KillZone**

```ini
[sub_resource type="RectangleShape2D" id="killzone"]
size = Vector2(1600, 64)

[node name="KillZone" type="Area2D" parent="."]
position = Vector2(640, 960)
collision_layer = 0
collision_mask = 3

[node name="Shape" type="CollisionShape2D" parent="KillZone"]
shape = SubResource("killzone")
```

（横跨全关卡并留余量，位于 PitFloor 上方 40px——先触即死再被接住，尸体躺在坑底。）

- [ ] **Step 4: 探针 8→9 项**

阶段二机动窗延长：现脚本（助跑跳平台→落回地面 x≈445）之后追加「跳坑段」——
继续向右从 ~445 走到宽坑边（992）约 128 帧，松键走入坑（~30 帧），下落触 KillZone
（~35 帧），死亡调度 60 帧，重生回 (128,640)。ManeuverEndFrame 260→约 620，TotalFrames
1070→约 1370（阶段三史莱姆观察窗保持 ~12s 量级）。新增检查：

```csharp
            Check(_playerFallDeaths >= 1, "玩家落坑即死并重生");
```

判定依据：采样发现 `Y > 900`（触达即死区高度）一次、且此后 `IsOnFloor && Y≈640 && X≈128`
（重生回出生点）一次。帧常量实跑微调，方法同前（不得放宽断言）。
阶段三开始时玩家已在出生点，史莱姆动态与原 8 项一致。

- [ ] **Step 5: 格式化、构建、全测、探针（9/9）并提交**

```bash
dotnet csharpier format . && dotnet csharpier check .
dotnet build && dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
git add -A && git commit -m "feat: 落坑即死（KillZone），玩家重生敌人消失，探针新增落坑死亡检查"
```

---

### Task 3: UI 框架与主循环（Main + 主菜单 + 暂停）

**Files:**
- Create: `Game/Scenes/Main.tscn` + `Game/UI/Main.cs`
- Create: `Game/UI/MainMenu.tscn` + `Game/UI/MainMenu.cs`
- Create: `Game/UI/PauseMenu.tscn` + `Game/UI/PauseMenu.cs`
- Modify: `project.godot`（`run/main_scene` → `Main.tscn`；新增 `pause` 输入动作 Esc）
- Modify: `Game/UI/GodotGameTemplate.UI.csproj`? 不需要——UI 脚本并入主 csproj（同仓库通配包含）

- [ ] **Step 1: Main.tscn + Main.cs**

根 `Node`（`ProcessMode = Always`，暂停时仍能响应菜单），三个挂点：
`UiLayer`（CanvasLayer，放菜单/HUD）、`LevelRoot`（Node，放关卡实例）。Main.cs 职责：

```csharp
/// <summary>应用编排：主菜单 → 关卡 → 暂停 → 回主菜单 的生命周期切换。</summary>
public partial class Main : Node
{
    // 开始游戏：释放菜单 → 实例化 TestLevel 到 LevelRoot → 实例化 Hud 到 UiLayer → Bind(玩家 Health)
    // Esc（仅游玩中）：get_tree().Paused = true + PauseMenu 显示
    // 继续：恢复暂停树；回主菜单：先 Unpause → 释放关卡与 HUD → 显示主菜单
}
```

细节约定：关卡实例名固定 `Level`（HUD 绑定路径 `LevelRoot/Level/Player` 由此而来）；
`MainMenu/PauseMenu` 用 C# 事件（`StartRequested` / `ResumeRequested` / `MainMenuRequested`）
上报给 Main，菜单节点自身不碰树切换。

- [ ] **Step 2: MainMenu / PauseMenu（占位级）**

MainMenu：居中 `Label`（游戏名占位「2D PLATFORMER」）+ 「开始游戏」「退出」按钮；
PauseMenu：半透明底 + 「继续」「回主菜单」。脚本只连按钮信号转事件。
`process_mode`：MainMenu 默认（菜单期未暂停）；PauseMenu 必须 `Always`（暂停树中可见可点）。

- [ ] **Step 3: project.godot**

`run/main_scene="res://Game/Scenes/Main.tscn"`；`[input]` 增加：

```ini
pause={
"deadzone": 0.2,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194305,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)
]
}
```

（physical_keycode 4194305 = Esc，格式照抄现有动作。）

- [ ] **Step 4: 验证与提交**

编辑器实跑：开始→游玩→Esc 暂停（物理停）→继续→回主菜单→再开始，全链路两次。
探针不受影响（直连 TestLevel）：

```bash
dotnet build && dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
git add -A && git commit -m "feat: 主菜单与暂停闭环（Main 编排、菜单/关卡切换、Esc 暂停树）"
```

---

### Task 4: HUD 血条（心形占位）

**Files:**
- Modify: `Tools/generate_placeholder_art.py` + 重生成（新增 heart.png，32×32）
- Create: `Game/UI/Hud.tscn` + `Game/UI/Hud.cs`
- Modify: `Game/Scenes/Main.tscn`（UiLayer 下预挂 Hud 实例，游玩时显示）

- [ ] **Step 1: heart.png**

美术脚本加 `HEART` 16×16 网格（红色像素心，含描边），与角色图一起 ×2 输出；
`heart.png.import` 由 Godot 首次运行自动生成。

- [ ] **Step 2: Hud.cs / Hud.tscn**

CanvasLayer + `HBoxContainer`（anchor 顶部左侧，offset (16,16)，separation 6）+
5 个 `TextureRect`（heart.png，custom_minimum_size (32,32)）：

```csharp
/// <summary>游玩 HUD：按 Health.HealthChanged/Died 渲染心形血量。由 Main 在开局后 Bind。</summary>
public partial class Hud : CanvasLayer
{
    public void Bind(Health health) { /* 订阅 HealthChanged/Died + 初始刷新 */ }
    private void Refresh(int currentHP) { /* 心形逐个显隐 */ }
}
```

死亡→重生链路自洽：重生时 `RestoreFull` 触发 HealthChanged → 心形回满。

- [ ] **Step 3: Main 接线 + 验证**

开始游戏后 `hud.Bind(playerHealth)`；实机验证：挨打掉心、落坑死亡重生回满、
回主菜单再开始血量正确重置。提交：

```bash
git add -A && git commit -m "feat: HUD 心形血条（HealthChanged 驱动，死亡重生回满自洽）"
```

---

### Task 5: 重生相机瞬移

**Files:**
- Create: `Game/Gameplay/Characters/CameraRig.cs`（表现层）
- Modify: `Game/Scenes/Player.tscn`（挂 CameraRig）

- [ ] **Step 1: CameraRig**

```csharp
/// <summary>
/// 表现层：订阅玩家重生事件，让 Phantom Camera 瞬移到重生点
/// （阻尼相机会平滑横穿全图，重生必须硬切）。按约定名发现同级 PhantomCamera2D。
/// </summary>
public partial class CameraRig : Node
{
    private Node _pcam;

    public void Bind(Character player)
    {
        _pcam ??= GetParent()?.GetNodeOrNull("PlayerPhantomCamera2D");
        player.Respawned += Snap;
    }

    private void Snap() => _pcam?.Call("teleport_position");
    public override void _ExitTree() { /* 退订 */ }
}
```

- [ ] **Step 2: 接线与验证**

`Character._Ready` 加 `_cameraRig = this.FindDescendant<CameraRig>(); _cameraRig?.Bind(this);`
（CameraRig 放 Player.tscn，史莱姆没有）。实机验证：故意落坑，死亡重生后相机立即在出生点、
无横穿动画；探针（9/9，相机瞬移影响 camera 相关采样——探针未断言相机，仅确认无报错）。

```bash
git add -A && git commit -m "feat: 重生相机瞬移（CameraRig 订阅 Respawned 调用 PCam teleport_position）"
```

---

### Task 6: 文档更新、全量回归与勾记

- [ ] **Step 1: 文档同步**

- `architecture.md`：§2 帧序补 `_screenShake/CameraRig` 表现层事件出口；§4 战斗闭环补
  「攻击数值唯一来源是 AttackConfig（经 Configure 下发）」与 KillZone；
  §5 表现层补 HUD/CameraRig 一句；§1 目录补 `UI/` 与 `World/`；新增 §11「UI 与关卡流」小节
  （Main 生命周期、暂停树策略）；文档索引加本计划
- `camera-design.md`：补重生瞬移一段（CameraRig → teleport_position）
- `README.md`：操作表补 Esc 暂停；玩法段补落坑即死一句
- `engineering-roadmap.md`：§5 候选 1 标注完成；新增「第二期」小节记录三项交付
  （UI 闭环 / 落坑即死 / 战斗审计加固）与日期

- [ ] **Step 2: 全量回归（CI 等价三连 + 探针 9/9）**

- [ ] **Step 3: 推送并确认 CI 绿**

```bash
git add -A && git commit -m "docs: 第二期文档同步与路线图勾记"
git push   # 代理不可用时: git -c http.proxy= -c https.proxy= push
gh run view $(gh run list --limit 1 --json databaseId --jq '.[0].databaseId')
```

- [ ] **Step 4: 实机验收（用户参与）**

完整闭环走查：主菜单开始 → 移动/跳跃/攻击/冲刺 → 挨打掉心 → 故意落坑（死亡重生、相机瞬移、
血条回满）→ Esc 暂停 → 继续 → 回主菜单 → 再开始；确认攻击/受击震屏仍为命中/受击触发。

---

## 非目标（本期不做）

- Game Over 结算页、关卡选择、设置菜单（音量/键位）
- UI 主题美化与九宫格、手柄导航焦点链
- 候选 2（关卡加载与场景切换闭环）——相机边界随关卡走的基建留给下一期
- 连击/hitstop/伤害数字/音效（战斗打磨池）
