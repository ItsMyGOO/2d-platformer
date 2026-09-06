# 架构总览

> 2D 平台跳跃（Godot 4.6 + C#），输入 → 意图 → 逻辑 → 表现 四层架构。
> 本文是第一次接触本仓库的开发者的入口：读完即可定位任何代码、并按 §8/§9 扩展新内容，
> 无需阅读源码内部实现。

## 1. 目录结构

```
Game/
├── Art/Placeholders/        # 占位像素图（32×32，Tools/generate_placeholder_art.py 生成）+ frames .tres
├── Config/                  # 数值与震屏配置资源（.tres，编辑器内调参）
│   ├── player_config.tres / slime_config.tres
│   └── shake_attack.tres / shake_hurt.tres
├── Gameplay/Characters/     # 全部玩法代码（目录即命名空间 GodotGameTemplate.Gameplay.Characters）
│   ├── Character.cs             # 编排者（CharacterBody2D）：按帧序串联四层、装配战斗闭环
│   ├── CharacterMotor.cs        # 逻辑层宿主（纯 C# 类，可单元测试）
│   ├── CharacterConfig.cs       # 编辑态数值配置（Resource，嵌套 Dash/AttackConfig）
│   ├── CharacterConfigData.cs   # 运行态数值配置（纯 C# POCO，见 §3）
│   ├── DashConfig.cs / AttackConfig.cs
│   ├── CharacterVisualState.cs  # 表现动画枚举（idle/run/jump/fall/dash/attack/hurt/dead）
│   ├── InputIntent.cs           # 意图层（readonly struct）
│   ├── CharacterPresenter.cs    # 表现层：动画/朝向/尘土/无敌闪烁
│   ├── ScreenShake.cs           # 表现层：命中/受击 → 相机噪声发射器
│   ├── Combat/                  # 战斗子系统：DamageInfo / Health / Hitbox / Hurtbox
│   ├── InputSources/            # 输入层：InputSource 抽象 + Player / SlimeAI 两实现
│   └── States/                  # 类式状态机：CharacterState 基类 + 6 个行为状态
└── Scenes/                  # BaseCharacter（基座）+ Player / Slime（继承）+ TestLevel（主场景）
Tools/
├── generate_placeholder_art.py  # 占位图生成（16×16 ASCII 网格 → 最近邻 ×2 输出 32×32）
└── headless_probe/              # 回归探针：脚本化输入 + 8 项 PASS/FAIL（见 §7）
Tests/UnitTests/                 # xUnit 纯单元测试（25 个）
Docs/                            # 项目文档（索引见 §10）
addons/phantom_camera/           # Phantom Camera 插件（相机跟随与震屏，见 Docs/camera-design.md）
```

约定：目录即命名空间（`Game/Gameplay/Characters/Combat/` → `GodotGameTemplate.Gameplay.Characters.Combat`）；
一个文件一个顶层类型，文件名 = 类型名。规范细节见 [code-standards.md](code-standards.md)。

## 2. 四层架构与帧序

```
输入层            意图层           逻辑层                      表现层
InputSource  ──▶  InputIntent  ──▶  CharacterMotor + States   ──▶  CharacterPresenter
(可替换 Node)     (纯数据 struct)    (+ Combat 战斗子系统)           ScreenShake / 相机
   ▲                                                                ▲
   └────────────── Character : CharacterBody2D（编排者，按固定帧序调用）┘
```

`Character._PhysicsProcess` 每物理帧按固定顺序执行（帧序确定，手感可复现）：

1. `Health.UpdateTimers(dt)` —— 无敌帧等计时推进
2. `intent = _inputSource.Poll(dt)` —— 输入 → 意图（死亡时喂空意图）
3. `Motor.Process(intent, dt, GetGravity().Y)` —— 意图 → 逻辑（当前状态计算本帧速度）
4. `Velocity = Motor.Velocity; MoveAndSlide()` —— 物理执行
5. `Motor.PostPhysics(IsOnFloor(), Velocity)` —— 回喂物理事实（落地检测、土狼计时）
6. `_presenter?.Sync()` —— 逻辑 → 表现（动画/朝向/闪烁）

表现层的尘土与震屏不占帧序位置：由逻辑层事件（`Jumped`/`Landed`/`AttackStarted`/
`HitConfirmed`/`Damaged`）驱动，事件是逻辑层对表现层的唯一出口。

**关键原则：逻辑层不碰引擎类型**（`Godot.Vector2` 等纯数学类型除外）。
Motor、States、Health、DamageInfo 全是纯 C#，不引用任何 Node/Resource——这是 §7 单元测试的前提。

**玩家与敌人共用一切逻辑**：差异只有三处——输入源子节点（键盘 vs AI）、
数值配置 `.tres`、外观 `SpriteFrames`。AI 产出与键盘完全相同的 `InputIntent`。

## 3. 配置双形态（编辑态 Resource ↔ 运行态 POCO）

- **编辑态** `CharacterConfig : Resource`（嵌套 `DashConfig`/`AttackConfig`）：
  `[Export]` 属性 + 场景赋值，`.tres` 内调参。属性键必须与 C# 成员名完全一致
  （`MaxSpeed`，Godot 不做 snake_case 转换）。
- **运行态** `CharacterConfigData`（纯 C# POCO，嵌套 `DashData`/`AttackData`）：
  `Character._Ready` 中 `Motor = new CharacterMotor(_config.ToData())` 一次性映射。
- **为什么拆**：Godot Resource 的构造依赖引擎原生运行时，纯 `dotnet test` 进程里
  `new CharacterConfig()` 会崩溃。Motor 只持有 POCO，逻辑层因此零引擎依赖。

## 4. 战斗闭环

```
Hitbox(Area2D, 攻击方) ──每物理帧轮询 GetOverlappingAreas──▶ Hurtbox(Area2D, 受击方)
    │  Motor.AttackStarted → BeginSwing() 清已命中集合                    │
    │  Motor.AttackActiveChanged(true/false) → SetActive 开关判定窗口      ▼
    │                                              Character.OnHurt(DamageInfo)
    │                                                    │
    │                                    Health.TryApplyDamage（无敌帧/死亡拒伤）
    │                                            │ 是                │ 致命
    │                                            ▼                   ▼
    └──命中结算成功──▶ HitConfirmed 事件        Motor.ForceHurt     Health.Died
                  （表现层命中震屏）          （击退+硬直）        （Motor.Kill，1s 后
                                                              敌人 QueueFree / 玩家重生）
```

- 同一次挥击对同一目标只结算一次（`Hitbox` 内 HashSet）；`HitConfirmed` 只在真实结算后触发，
  挥空不触发——表现层据此做命中反馈。
- `DamageInfo.Create(damage, knockbackHorizontal, knockbackVertical, sourceDirection)`：
  击退方向由 `sourceDirection`（攻击者→受击者的水平符号）决定。
- 死亡差异：敌人 `QueueFree` 消失；玩家回出生点、回满 HP、`Motor.Reset()`。
  玩家身份由输入源类型推导（`_inputSource is PlayerInputSource`），无标记字段。
- **物理层**（project.godot 命名）：

| 层 | 名称 | 用途 |
|---|---|---|
| 1 | player_body | 玩家实体（与地形/敌人实体碰撞） |
| 2 | enemy_body | 敌人实体 |
| 3 | player_hurtbox | 玩家受击盒（敌人 Hitbox 的 mask） |
| 4 | enemy_hurtbox | 敌人受击盒（玩家 Hitbox 的 mask） |

## 5. 表现层

- `CharacterPresenter`：读 `Motor.VisualState` 播放同名动画（枚举名小写即动画名）、
  按 `Facing` 翻转 Pivot（精灵与命中盒一起换边）、无敌帧闪烁、跳/落地尘土。
- `ScreenShake`：`Hitbox.HitConfirmed` → 攻击抖动、`Health.Damaged` → 受击抖动；
  `Enabled` 总开关。噪声参数在 `Game/Config/shake_*.tres`。
- 相机跟随、边界限制、噪声到屏幕的链路：见 [camera-design.md](camera-design.md)。

## 6. 装配策略

| 装配对象 | 方式 |
|---|---|
| 数值配置（Resource） | `[Export]` + 场景赋值，正常导出不受限制 |
| C# 脚本的 Node 引用 | **不能**用导出 NodePath（场景实例化时前向解析静默丢弃），在 `_Ready` 按类型 `FindDescendant<T>()` 或按约定名 `GetNodeOrNull` 发现；导出成员保留作显式覆盖入口 |
| GDScript 的 Node 引用 | 导出 NodePath 正常工作（Phantom Camera 的 `follow_target` 即此形式） |
| 噪声发射器 | `ScreenShake` 按约定名发现同级 `AttackNoiseEmitter2D` / `HurtNoiseEmitter2D` |
| 感知射线 | `SlimeAIInputSource` 按约定名发现同级 `WallRay` / `LedgeNearRay` / `LedgeFarRay` |

## 7. 测试与验证

- **单元测试**（25 个）：`Tests/UnitTests/`，纯 C# 零场景依赖。
  测试配置用 `TestConfig()` 直接构造 `CharacterConfigData`（固定尺度数值），
  以 delta 逐帧驱动 Motor 断言速度/状态/事件。覆盖：跳跃手感三件套、
  冲刺/攻击互斥、受击打断、无敌帧、死亡终态、配置缺失降级。
- **headless 探针**（8 项）：`Tools/headless_probe`，脚本化输入驱动真实场景做端到端回归：

  ```bash
  dotnet build
  "D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" \
      --headless --path . Tools/headless_probe/Probe.tscn
  ```

  覆盖：自然落地、跳跃高度（≥90px）、跳上平台、宽坑不落、窄坑穿越、
  追击出招、命中玩家 HP 下降。全过退出码 0。
- **CI**（GitHub Actions）：`format-check`（CSharpier）+ `build-and-test`（Release 构建 + 单测）。

## 8. 扩展指南：新增一种敌人（约 1 小时）

以史莱姆为参照（`Game/Scenes/Slime.tscn`）。全程不改 Motor/States/Character/Presenter——
**输入源 + 数值配置 + 外观就是敌人之间的全部差异**。

1. **数值**：复制 `Game/Config/slime_config.tres` → `your_enemy_config.tres`，编辑器里调数值
   （属性键与 `CharacterConfig` 成员名一致；`Attack = null` 表示不会攻击）。
2. **外观**：准备 `your_enemy_frames.tres`（照抄 `slime_frames.tres` 结构）。
   动画名固定八个：`idle` / `run` / `jump` / `fall` / `attack` / `hurt` / `dead`
   （Presenter 按枚举名小写查找；缺哪个该状态就播不出来）。贴图放 `Game/Art/Placeholders/`。
3. **场景**：新建 `Game/Scenes/YourEnemy.tscn`，根节点**继承** `BaseCharacter.tscn`（右键 → 新建继承场景），然后：
   - 根节点：`collision_layer = 2`、`collision_mask = 1`、`_config = your_enemy_config.tres`
   - `Pivot/Sprite`：`sprite_frames = your_enemy_frames.tres`
   - `Pivot/Hitbox`：`collision_mask = 3`（打玩家）；`Pivot/Hitbox/HitShape` 的 `position.x`
     按攻击距离标定——命中可达 = 偏移 + 命中盒半宽 14 + 玩家受击盒半宽 14，
     AI 的 `_attackDistance`（SlimeAIInputSource 导出）必须小于该值，否则会停在射程外空挥
   - `Pivot/Hurtbox`：`collision_layer = 4`（被玩家打）
   - AI（照抄 Slime.tscn）：`AIInput` 节点挂 `SlimeAIInputSource.cs`；
     三个 `RayCast2D`（`WallRay` / `LedgeNearRay` / `LedgeFarRay`）与 `ChaseDetector`
     （Area2D，mask=1）+ `VisionShape`——节点名是约定名，不能改
4. **入关**：在关卡场景里实例化 `YourEnemy.tscn`。
5. **验证**：`dotnet build` + 探针回归（§7）；编辑器里跑一圈观察巡逻/追击/攻击。
6. **可选**：行为不同才需要新输入源——新写 `YourAIInputSource.cs` 继承 `InputSource`
   （只实现 `Poll(delta)` 产出 `InputIntent`，绝不直接操控角色），场景里替换 AIInput 节点脚本即可。

## 9. 扩展指南：新增一个状态

以 `DashState`（冲刺）为参照：

1. **状态类**：`States/YourState.cs` 继承 `CharacterState`，实现 `Process(intent, delta, gravity)`
   （每帧计算速度）与 `VisualState`；需要的数据放 `Motor` 上下文（新增 internal 属性/字段），
   状态对象本身不存每实例数据。`Enter()`/`Exit()` 做进出场动作（如开关判定、清计时）。
2. **表现枚举**：`CharacterVisualState` 加同名值（小写即动画名）。
3. **路由**：`CharacterMotor` 加 `YourState` 字段与进入方法（参照 `TryStartDash`：检查配置存在
   与冷却，成功则 `ChangeState`）。互斥规则靠结构性路由保证——只有 `Grounded`/`Airborne`
   读取主动意图，行为状态互不读对方意图（参考 combat 文档 §2 的允许/禁止表）。
4. **动画**：两个 `frames.tres` 加同名动画。
5. **测试**：`CharacterMotorTests` 补互斥用例（进入条件、被谁打断、结束去向），`dotnet test` + 探针。

## 10. 文档索引

| 文档 | 定位 |
|---|---|
| 本文（architecture.md） | 架构入口：结构、帧序、扩展指南 |
| [character-controller-design.md](character-controller-design.md) | 角色控制器设计：四层职责推导过程、装配策略的经验来源 |
| [combat-state-machine-design.md](combat-state-machine-design.md) | 战斗状态机设计：状态切换规则、战斗闭环、AI 三层行为 |
| [camera-design.md](camera-design.md) | 相机与震屏：Phantom Camera 接线、参数迁移、调整方法 |
| [code-standards.md](code-standards.md) | 代码规范：命名、注释、装配约定、格式化与提交 |
| [engineering-roadmap.md](engineering-roadmap.md) | 工程路线图：六阶段任务与验收、下一期候选 |
| plans/ | 各阶段实施计划（历史记录，含推导与裁量点） |
