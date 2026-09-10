# 第六期实施计划：战斗扩展批（魂量 / 剑气 / 弧线投掷物 / 反冲 / Pogo）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> 设计决议（2026-09-09 与用户对齐）：剑气资源 = HK 式魂量（近战命中积攒、剑气消耗）；
> 手感件 = 攻击反冲 + 下劈 Pogo（方向上劈后置）；敌方投掷物 = 新增轰炸型敌人（弧线炸弹）。

**Goal:** 战斗系统向空洞骑士手感靠拢：魂量循环（近战攒魂 → 剑气倾泻）、玩家剑气远程攻击、
轰炸敌人的弧线投掷物、攻击者反冲、空中下劈弹跳（Pogo，可劈碎弹体借力）。

**Architecture:** 全部复用既有三套基建（状态机 / 弹体 / 配置双形态）。魂量是纯 C# 组件挂在
Motor（路由层原子扣魂，逻辑层保持零引擎依赖）；剑气 = 新 CastState + CastEmitter（镜像
Attack/ProjectileEmitter 模式）；轰炸敌 = 复用 ShooterAI 输入源、差异全在 BombEmitter
（弧线落点解算）；反冲/Pogo = Motor 小冲动 API + Hitbox 下向朝向切换。物理层新增第 6 层
projectile（弹体可被近战劈碎）。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0、xUnit、CSharpier 1.3.0、美术脚本（stdlib）。

**环境事实（已核对现状）：**

- 基线：34 单元测试 + 探针 9/9 + CI 绿；第五期（存档/设置/重映射/手柄/导出/本地化）已交付
- `InputRemapStore.RemappableActions` 是**硬编码清单**——新增动作（cast/move_down）必须同步补，
  设置页改键行自动跟随
- `InputIntent` 现有字段：MoveAxis / JumpPressed / JumpHeld / DashPressed / AttackPressed
- `Projectile.Launch(direction, damage, kbH, kbV, targetHurtboxLayer)` 直线弹；
  `CollisionMask = 1(地形) | targetHurtboxLayer`，`CollisionLayer = 0`（发射时置）
- Hitbox mask 现状：BaseCharacter 默认 8（打敌受击盒），Slime.tscn override 4（打玩家受击盒）；
  Player 未 override（继承 8）
- 历史教训沿用：①构建 gate 用退出码（grep 会误放行）；②新增 png/cs 先
  `$GODOT --headless --import`；③编辑器开着会锁 `.godot`（headless 表现为 5MB 级挂起）
- joypad 事件手写序列化有前科——照抄 project.godot 既有条目格式，改完实机验手柄
- `$GODOT` = `D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe`

## 数值表（裁量点，实机后可调）

| 项 | 初值 |
|---|---|
| 魂上限 / 每次近战命中 / 剑气消耗 | 33 / 11 / 11（HK 原味三段） |
| 剑气：前摇 / 后摇 / 冷却 / 弹速 / 伤害 / 击退 | 0.08s / 0.22s / 0.4s / 420 / 1 / (200, 80) |
| 反冲速度（攻击者被弹开） | 120 px/s（地面被摩擦自然衰减，空中全额） |
| Pogo 弹跳 | -JumpVelocity × 0.75（空中下劈命中） |
| 炸弹：重力 / 飞行时间 / 射程上限 | 900 / 0.9s / 420px |
| 轰炸敌视野（ChaseDetector 半径） | 300 |

其他裁量点：剑气复用攻击动画（正式美术期再拆独立动画）；敌人无魂系统（SoulMax=0 即无）；
魂重生清零、不持久化；剑气不穿透（单次命中）；炸弹无 AoE；下劈可劈碎**任何**弹体含自己
剑气（HK 的 vengeful spirit 同样可 pogo——保留为特性）。

---

### Task 1: 弹体泛化（弧线弹道）+ 炸弹占位

**Files:**
- Modify: `Game/Gameplay/Characters/Combat/Projectile.cs`
- Create: `Game/Scenes/Bomb.tscn`
- Modify: `Tools/generate_placeholder_art.py`（+ bomb.png）

- [x] **Step 1: Projectile 弧线模式**

```csharp
    [Export]
    public float Gravity { get; set; } = 0f; // 0=直线（现状）；>0=抛物线

    private Vector2 _velocity;   // 弧线模式的速度向量
    private bool _isArc;

    /// <summary>弧线发射：初速度向量 + 可选重力（弹道由抛物线决定）。目标层语义同 Launch。</summary>
    public void LaunchArc(Vector2 initialVelocity, int damage, float kbH, float kbV, uint targetHurtboxLayer)
    {
        _isArc = true;
        _velocity = initialVelocity;
        // 数值/掩码赋值与 Launch 相同（抽私有方法共用）
    }

    // _PhysicsProcess 分支：
    //   弧线：GlobalPosition += _velocity * dt; _velocity.Y += Gravity * dt;
    //         Rotation = _velocity.Angle();   // 贴图随弹道旋转
    //   直线：现状不变
```

（公共赋值逻辑抽私有 `Setup(damage, kbH, kbV, targetLayer)`，两入口共用；MaxLifetime 两模式通用。）

- [x] **Step 2: 炸弹美术与场景**

美术脚本加 `BOMB` 网格（深色球体+引信+高光，新调色板字符），输出 32×32 `bomb.png`；
`Bomb.tscn`：Projectile 脚本 + bomb 贴图 + CircleShape r10 + 场景内 `Gravity = 900`、
`MaxLifetime = 3`。

- [x] **Step 3: 构建与提交**（新资源先 `--import`）

```bash
"$GODOT" --headless --import && dotnet build && dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn   # 9/9
git add -A && git commit -m "feat: 弹体支持弧线弹道（可选重力）与炸弹占位场景"
```

---

### Task 2: 魂量系统 + HUD 魂条

**Files:**
- Create: `Game/Gameplay/Characters/Combat/Soul.cs`（纯 C#）
- Modify: `CharacterMotor.cs`（持有 Soul / GainSoul / Reset 清魂）
- Modify: `CharacterConfig.cs` + `CharacterConfigData.cs`（SoulMax / SoulGainPerHit）
- Modify: `Game/Gameplay/Characters/Character.cs`（HitConfirmed → GainSoul）
- Modify: `Game/UI/Hud.tscn` / `Hud.cs`（魂条）
- Modify: `Game/Config/player_config.tres`（SoulMax=33, SoulGainPerHit=11）
- Create: `Tests/UnitTests/SoulTests.cs`

- [x] **Step 1: Soul 组件**：`Soul(max)`；`Current`；`TrySpend(int)→bool`（不足拒绝且不动值）；
  `Gain(int)`（封顶）；`Clear()`；事件 `SoulChanged(int current, int max)`。

- [x] **Step 2: Motor 接入**：构造时 `Soul = config.SoulMax > 0 ? new Soul(config.SoulMax) : null`；
  `public Soul Soul { get; }`；`public void GainSoul() => Soul?.Gain(Config.SoulGainPerHit);`
  `Reset()` 内 `Soul?.Clear();`（重生清零）。

- [x] **Step 3: 命中积攒**：Character._Ready 在既有攻击 Configure 段后：

```csharp
        if (Motor.Soul != null)
        {
            _hitbox.HitConfirmed += () => Motor.GainSoul(); // 近战命中攒魂
        }
```

- [x] **Step 4: HUD 魂条**：心形下方 (16, 56) 背景条 160×8 + 白色填充条；`Hud.Bind` 订阅
  `player.Motor.Soul.SoulChanged`（Soul 为 null 不订阅）按比例设填充宽。

- [x] **Step 5: 单测 + 回归 + 提交**

SoulTests：Gain 封顶与事件、TrySpend 成功/不足拒绝、Clear；Motor 集成：Reset 后魂归零。

```bash
git add -A && git commit -m "feat: HK 式魂量系统（近战命中积攒，HUD 魂条，重生清零）"
```

---

### Task 3: 玩家剑气（CastState + 新月弹）

**Files:**
- Modify: `project.godot`（input 加 `cast`：K(physical 75) + 手柄 Y(button 3)，照抄既有条目格式）
- Modify: `Game/Persistence/InputRemapStore.cs`（RemappableActions += "cast"）
- Modify: `InputIntent.cs`（CastPressed）与 `PlayerInputSource.cs`（读 cast）
- Create: `Game/Gameplay/Characters/CastConfig.cs`（Resource：见数值表 + SoulCost=11）
- Modify: `CharacterConfig.cs` / `CharacterConfigData.cs`（可空 Cast + ToData 映射）
- Modify: `CharacterMotor.cs`（_castState / TryStartCast / CastFired 事件 / 冷却计时 /
  RouteCombatActions 路由 CastPressed）
- Create: `Game/Gameplay/Characters/States/CastState.cs`（镜像 AttackState：前摇→CastFired→后摇，
  地面定身/空中惯性，互斥与打断同 Attack）
- Create: `Game/Gameplay/Characters/Combat/CastEmitter.cs`（订阅 CastFired 发新月弹，
  muzzle (22,-2)，目标层 = IsPlayer ? 8 : 4）
- Create: `Game/Scenes/Crescent.tscn`（Projectile + 新月贴图，Speed=420，MaxLifetime=0.6，r10）
- Modify: `Tools/generate_placeholder_art.py`（+ crescent.png 淡蓝新月）
- Modify: `Game/Scenes/Player.tscn`（CastEmitter 节点）、`Game/Config/player_config.tres`（Cast 接入）
- Create: `Tests/UnitTests/CastTests.cs`

- [x] **Step 1: 输入与意图**（project.godot / RemapStore / InputIntent / PlayerInputSource）

- [x] **Step 2: 配置双形态**（CastConfig + POCO CastData + ToData；数值见表）

- [x] **Step 3: Motor 与状态机**：

```csharp
    /// <summary>剑气前摇结束瞬间触发（CastEmitter 发射弹体）。</summary>
    public event Action CastFired;

    internal bool TryStartCast()
    {
        var cast = _config.Cast;
        if (cast == null || _castCooldownTimer > 0f) return false;
        if (Soul != null && !Soul.TrySpend(cast.SoulCost)) return false; // 原子扣魂
        _castCooldownTimer = cast.Cooldown;
        ChangeState(_castState);
        return true;
    }
    internal void NotifyCastFired() => CastFired?.Invoke();
```

RouteCombatActions 追加 `if (intent.CastPressed) { Motor.TryStartCast(); }`；
UpdateTimers 加冷却递减；Reset 清冷却。

- [x] **Step 4: CastEmitter 与新月弹**（发射逻辑照 ProjectileEmitter 模式；弹速由场景
  Speed=420 决定，Launch 只传方向）

- [x] **Step 5: 单测**（CastTests）：无配置忽略 / 冷却拒绝 / 魂不足拒绝且不扣 / 成功扣魂并
  前摇末触发 CastFired / 攻击中发剑气被拒 / 剑气中攻击与冲刺被拒 / 受击打断

- [x] **Step 6: e2e 临时测试（用后删）**：隔离史莱姆 → 按 cast → 新月弹命中史莱姆掉血、
  魂 33→22；再近战命中 → 魂 +11

- [x] **Step 7: 回归 + 提交**

```bash
git add -A && git commit -m "feat: 玩家剑气（魂量消耗，CastState/CastEmitter/新月弹，改键与手柄接入）"
```

---

### Task 4: 轰炸型敌人（弧线炸弹）

**Files:**
- Create: `Game/Gameplay/Characters/Combat/BombEmitter.cs`
- Create: `Game/Scenes/Bomber.tscn`
- Modify: `Game/Gameplay/Characters/Character.cs`（玩家入组 `AddToGroup("player")`，_isPlayer 分支）
- Modify: `Game/Scenes/RoomC.tscn`（Bomber1 @ (620, 642)）

- [x] **Step 1: 玩家标记**：`if (_isPlayer) { AddToGroup("player"); }`（全树寻址入口）

- [x] **Step 2: BombEmitter（弧线落点解算）**

```csharp
    // 订阅 Motor.AttackActiveChanged(true)（与 ProjectileEmitter 同钩子）：
    // 玩家 = GetTree().GetFirstNodeInGroup("player") as Character; 无玩家 → 平射前方兜底
    // dx = Clamp(player.GlobalPosition.X - muzzle.X, -MaxRange, MaxRange);
    // dy = player.GlobalPosition.Y - muzzle.Y;
    // T = FlightTime; vx = dx / T; vy = (dy - 0.5f * Gravity * T * T) / T;   // y 向下为正
    // projectile.LaunchArc(new Vector2(vx, vy), 伤害/击退取 Motor.Config.Attack, 4);
```

（Gravity/FlightTime/MaxRange/muzzleOffset 均 [Export]；数值见表。）

- [x] **Step 3: Bomber.tscn**：借 Shooter.tscn 结构——BaseCharacter 继承、slime_frames 换
  modulate（橙棕）、Hitbox mask=4、Hurtbox layer=8、ContactDamager、ChaseDetector r300、
  **AIInput 直接复用 ShooterAIInputSource**（差异全在 Emitter：ProjectileEmitter → BombEmitter）、
  配置复用 slime_config.tres。

- [x] **Step 4: 入关 + e2e（用后删）**：轰炸敌与玩家隔 ~300px → 炸弹弧线飞行 → 玩家 HP 下降

- [x] **Step 5: 回归（探针 9/9——RoomC 加敌不进探针观察窗）+ 提交**

```bash
git add -A && git commit -m "feat: 轰炸型敌人（弧线炸弹，落点解算，复用射手 AI 模式）"
```

---

### Task 5: HK 手感件（攻击反冲 + 下劈 Pogo）

**Files:**
- Modify: `project.godot`（input 加 `move_down`：S(83) + ↓(4194322) + 左摇杆 Y+，照抄格式）
- Modify: `Game/Persistence/InputRemapStore.cs`（+= "move_down"）
- Modify: `InputIntent.cs`（DownHeld）与 `PlayerInputSource.cs`
- Modify: `AttackConfig.cs` / POCO（RecoilVelocity=120）
- Modify: `CharacterMotor.cs`（`ApplyImpulse(Vector2)` / `Bounce()` /
  `AttackDownOriented` 标记：TryStartAttack 时 `!IsOnFloor && LastIntent.DownHeld`）
- Modify: `Combat/Hitbox.cs`（`SetDownOrientation(bool)`：HitShape position (22,0)↔(0,26)）
- Modify: `Projectile.cs`（发射时 `CollisionLayer = 32`——第 6 层 projectile）
- Modify: `project.godot` layer_names（layer_6="projectile"）
- Modify: `Game/Scenes/Player.tscn`（Hitbox override `collision_mask = 40`：8|32）
- Modify: `Combat/Hitbox.cs` 命中循环（`else if (area is Projectile p) { p.Struck(); HitConfirmed?.Invoke(); }`，
  Projectile 加 `public void Struck() => QueueFree();`）
- Modify: `Game/Gameplay/Characters/Character.cs`（HitConfirmed 订阅：反冲
  `Motor.ApplyImpulse(new(-Motor.Facing * RecoilVelocity, 0))`；
  `if (Motor.AttackDownOriented && !Motor.IsOnFloor) Motor.Bounce();`；
  AttackStarted 时 `_hitbox?.SetDownOrientation(Motor.AttackDownOriented)`）

- [x] **Step 1: 输入与意图**（move_down 三端 + DownHeld 进意图）

- [x] **Step 2: Motor 三件**（ApplyImpulse / Bounce(−JumpVelocity×0.75) / AttackDownOriented）

- [x] **Step 3: Hitbox 下向与劈弹**（SetDownOrientation；命中循环加 Projectile 分支；弹体层 32；
  Player Hitbox mask 40；layer_6 命名）

- [x] **Step 4: Character 接线**（反冲 + Pogo 弹跳 + 攻击开始时切命中盒朝向）

- [x] **Step 5: 单测**：DownHeld 进意图并透传；空中+DownHeld 攻击 → AttackDownOriented=true、
  地面为 false；Bounce 设置上升速度；ApplyImpulse 叠加速度

- [x] **Step 6: e2e（用后删）**：①空中下劈史莱姆 → 玩家 Vy<0 弹起且史莱姆掉血；
  ②下劈飞行中的炸弹 → 炸弹消失且玩家弹起

- [x] **Step 7: 回归 + 提交**

```bash
git add -A && git commit -m "feat: HK 手感件——攻击反冲与下劈 Pogo（可劈碎弹体借力弹跳）"
```

---

### Task 6: 文档同步、全量回归与推送

- [x] **Step 1: 文档**

- `architecture.md`：§2 意图字段表补 CastPressed/DownHeld；§4 战斗闭环补魂量循环/剑气/
  反冲/Pogo/弹体层（物理层表加 6=projectile 值 32）；§8 新增敌人指南补 Bomber 参照
  （AI 复用 ShooterAI，差异在 Emitter）；§9 新增状态指南补 CastState 参照；§1 目录更新
- `README.md`：操作表补 剑气 K/手柄Y、下移 S/↓/摇杆Y；玩法段补魂量循环一句
- `backlog.md`：勾记本批（魂量/剑气/轰炸敌/反冲/Pogo）；挂账新增：剑气穿透、上劈、
  炸弹 AoE 与引信动画、剑气蓄力、敌人劈弹免疫开关

- [ ] **Step 2: 全量回归（CI 等价三连 + 探针 9/9）**

```bash
dotnet csharpier check .
dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn; echo "exit=$?"
```

- [ ] **Step 3: 推送并确认 CI 绿**

- [ ] **Step 4: 实机验收（用户参与）**：K 剑气（魂不足拒发、魂条消耗）；近战命中攒魂；
  贴脸砍中敌人自己被轻弹开（反冲）；空中 S/↓+J 下劈史莱姆与炸弹弹跳（Pogo）；
  RoomC 轰炸敌弧线炸弹落点追踪；手柄 Y 剑气 / 摇杆下+攻击 下劈；改键页出现 cast/move_down 两行。

---

## 非目标（本期不做）

- 剑气穿透/蓄力、上劈、三向攻击全套（backlog 挂账）
- 炸弹 AoE 爆炸与引信动画（落点单次命中）
- 剑气独立动画（复用攻击动画，正式美术期拆分）、战斗音效（等音频素材）
- 敌人魂系统、魂的持久化（重生清零即可）
- Boss / 连击段数（backlog）
