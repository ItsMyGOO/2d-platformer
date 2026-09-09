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
│   ├── CameraRig.cs             # 表现层：重生 → 相机瞬移
│   ├── Combat/                  # 战斗子系统：DamageInfo / Health / Hitbox / Hurtbox
│   ├── InputSources/            # 输入层：InputSource 抽象 + Player / SlimeAI 两实现
│   └── States/                  # 类式状态机：CharacterState 基类 + 6 个行为状态
├── Gameplay/World/          # 关卡类玩法物件（World 九宫格房间流式 / KillZone 即死区）
└── Scenes/                  # Main（应用编排）+ World（三房间无缝世界）+ BaseCharacter 基座
│                              + Player / Slime（继承）+ TestLevel / RoomB / RoomC（房间）
Game/UI/                     # UI 层（命名空间 GodotGameTemplate.UI）：Main 编排 + 主菜单/暂停/HUD
Tools/
├── generate_placeholder_art.py  # 占位图生成（16×16 ASCII 网格 → 最近邻 ×2 输出 32×32）
└── headless_probe/              # 回归探针：脚本化输入 + 8 项 PASS/FAIL（见 §7）
Tests/UnitTests/                 # xUnit 纯单元测试（25 个）
Docs/                            # 项目文档（索引见 §11）
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
`HitConfirmed`/`Damaged`/`Respawned`）驱动，事件是逻辑层对表现层的唯一出口。

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
  挥空与拒伤（无敌/尸体）都不触发——表现层据此做命中反馈。
- **攻击数值唯一事实源是 `AttackConfig`**（伤害/击退），由编排者在装配时经 `Hitbox.Configure`
  下发；`Hitbox` 自身不持有可调数值。
- `DamageInfo.Create(damage, knockbackHorizontal, knockbackVertical, sourceDirection)`：
  击退方向由 `sourceDirection`（攻击者→受击者的水平符号）决定。
- **死亡来源有两种**：血量归零（`Health.Died`）与环境即死（`World/KillZone`，落坑不扣血直接
  `KillInstantly`）；两者汇入同一条死亡调度（死亡动画 → 敌人 1s 消失 / 玩家回出生点重生）。
- 死亡差异：敌人 `QueueFree` 消失；玩家回出生点、回满 HP、`Motor.Reset()`。
  玩家身份由输入源类型推导（`_inputSource is PlayerInputSource`），无标记字段。
- **物理层**（project.godot 命名，第三期规范化——身体只撞 terrain，**单位之间全穿透**，
  伤害完全由判定框/接触组件结算）：

| 层号 | 位值 | 名称 | 用途 |
|---|---|---|---|
| 1 | 1 | terrain | 世界地形（唯一与身体碰撞的层） |
| 2 | 2 | enemy_body | 敌人实体 |
| 3 | 4 | player_hurtbox | 玩家受击盒（敌人 Hitbox/ContactDamager 的 mask） |
| 4 | 8 | enemy_hurtbox | 敌人受击盒（玩家 Hitbox 的 mask） |
| 5 | 16 | player_body | 玩家实体（ChaseDetector/KillZone 的 mask） |

- **接触伤害**：敌人身上常驻 `ContactDamager`（Area2D，与身体同尺寸），每帧轮询重叠并结算——
  `Health` 无敌帧天然限频（0.8s/次）；数值与挥击同源（AttackConfig 经 Configure 下发）。

## 5. 表现层与 UI

- `CharacterPresenter`：读 `Motor.VisualState` 播放同名动画（枚举名小写即动画名）、
  按 `Facing` 翻转 Pivot（精灵与命中盒一起换边）、无敌帧闪烁、跳/落地尘土。
- `ScreenShake`：`Hitbox.HitConfirmed` → 攻击抖动、`Health.Damaged` → 受击抖动；
  `Enabled` 总开关。噪声参数在 `Game/Config/shake_*.tres`。
- `CameraRig`：订阅玩家 `Respawned`，调用 PCam `teleport_position()` 硬切相机。
- 相机跟随、边界限制、噪声到屏幕的链路：见 [camera-design.md](camera-design.md)。
- **UI 层（`Game/UI/`）**：`Main`（应用编排，process_mode=Always）持有主菜单/暂停菜单/HUD；
  关卡实例挂 Pausable 的 LevelRoot——Esc 暂停 = `get_tree().Paused`，菜单仍可交互。
  `Hud` 订阅 `Health.HealthChanged/Died` 渲染心形血条，死亡重生回满自洽。详见 §11。

## 6. 装配策略

| 装配对象 | 方式 |
|---|---|
| 数值配置（Resource） | `[Export]` + 场景赋值，正常导出不受限制 |
| C# 脚本的 Node 引用 | **不能**用导出 NodePath（场景实例化时前向解析静默丢弃），在 `_Ready` 按类型 `FindDescendant<T>()` 或按约定名 `GetNodeOrNull` 发现；导出成员保留作显式覆盖入口 |
| GDScript 的 Node 引用 | 导出 NodePath 正常工作（Phantom Camera 的 `follow_target` 即此形式） |
| 噪声发射器 | `ScreenShake` 按约定名发现同级 `AttackNoiseEmitter2D` / `HurtNoiseEmitter2D` |
| 感知射线 | `SlimeAIInputSource` 按约定名发现同级 `WallRay` / `LedgeNearRay` / `LedgeFarRay` |

## 7. 测试与验证

- **单元测试**（28 个）：`Tests/UnitTests/`，纯 C# 零场景依赖。
  测试配置用 `TestConfig()` 直接构造 `CharacterConfigData`（固定尺度数值），
  以 delta 逐帧驱动 Motor 断言速度/状态/事件。覆盖：跳跃手感三件套、
  冲刺/攻击互斥、受击打断、无敌帧、死亡终态、配置缺失降级、生命变化事件。
- **headless 探针**（8 项）：`Tools/headless_probe`，脚本化输入驱动真实场景做端到端回归：

  ```bash
  dotnet build
  "D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" \
      --headless --path . Tools/headless_probe/Probe.tscn
  ```

  覆盖：自然落地、跳跃高度（≥90px）、跳上平台、落坑即死并重生回出生点、宽坑不落、
  窄坑穿越、追击出招、命中玩家 HP 下降。全过退出码 0。
- **CI**（GitHub Actions）：`format-check`（CSharpier）+ `build-and-test`（Release 构建 + 单测）。

## 8. 扩展指南：新增一种敌人（约 1 小时）

以史莱姆（`Game/Scenes/Slime.tscn`，近战接触+挥击）与射手（`Game/Scenes/Shooter.tscn`，
站桩远程弹体）为参照。全程不改 Motor/States/Character——
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
4. **入关**：在关卡场景里实例化 `YourEnemy.tscn`。**出生点必须与地面齐平**
   （角色中心 y = 地面顶 − 身体半高 14，如 TestLevel 的 642）：悬空出生会在物理沉降时
   误触发一次落地事件（尘土/土狼计时被污染）。
5. **验证**：`dotnet build` + 探针回归（§7）；编辑器里跑一圈观察巡逻/追击/攻击。
6. **可选**：行为不同才需要新输入源——新写 `YourAIInputSource.cs` 继承 `InputSource`
   （只实现 `Poll(delta)` 产出 `InputIntent`，绝不直接操控角色），场景里替换 AIInput 节点脚本即可。
   远程敌人参照 Shooter：`ShooterAIInputSource`（视野内周期发攻击脉冲）+
   `ProjectileEmitter`（订阅 AttackActiveChanged 在判定窗口开启瞬间朝面朝方向发射弹体）+
   `Projectile.tscn`（直线弹体：命中受击盒结算、撞地形销毁、超时自毁）。

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

## 10. UI 与关卡流

`Game/Scenes/Main.tscn` 是应用根（主场景）：

```
Main (Node, process_mode=Always)          ← Esc 处理、生命周期切换
├── UiLayer (CanvasLayer, Always)         ← 暂停时菜单仍可交互
│   ├── MainMenu（开始/退出）
│   ├── PauseMenu（继续/回主菜单，默认隐藏）
│   └── Hud（开局后实例化并 Bind 玩家 Health）
└── LevelRoot (Node, Pausable)            ← 关卡实例（名字固定 Level）
```

- 开始游戏：释放菜单 → 实例化 `TestLevel` 到 LevelRoot → 实例化 Hud 并绑定玩家 Health。
- Esc：游玩中 `get_tree().Paused = true` + 暂停菜单；暂停中 Esc 或「继续」恢复。
- 回主菜单：Unpause → 释放关卡与 HUD → 显示主菜单。
- 玩家死亡 → 现有自动重生（回出生点、回满血、相机瞬移）即「死亡→重开」闭环。
- 探针（`Tools/headless_probe`）直接实例化 World，不经过 Main——UI 改动不影响探针。

## 11. 世界与房间流式

世界 = 1280×720 **房间网格**（`Game/Scenes/World.tscn`：`World` 根 + `Rooms/` 下按网格坐标
摆放的房间实例 + 世界级持久 Player）。`World`（`Gameplay/World/World.cs`）每帧检查玩家所在格：

- 玩家所在格 ±1（**3×3 窗口**）的房间在场景树，其余 `CallDeferred` **RemoveChild 缓存**——
  节点不销毁，击杀/位置状态天然保留，走回窗口即原样恢复；
- **门 = 相邻房间墙体的几何开口**（贴地面高 160px），无传送脚本；边界墙体由左侧房间承担，
  右侧房间不建左墙（窗口数学保证边界两侧房间同时在场）；
- **房间几何 = TileMapLayer**（第四期起）：`Game/Config/placeholder_tiles.tres` 图集
  （5 tile：地形顶/地形填/墙/平台顶/平台身，全格碰撞，collision_layer=1 terrain），
  TileMapLayer 统一 `position = (0, 16)`（行 20 = 世界地面顶 656）。碰撞与视觉天然一致，
  墙即所见、门口即墙上留空的行。**TileSet 必须设 `tile_size = Vector2i(32, 32)`**
  （漏设默认 16 → 碰撞体挤在 1/4 网格上，表现为全员穿地坠落）；
- **新增房间几何的流程**：画 ASCII 布局（40×22，`.`空 `#`地形自动顶/填 `W`墙 `-`平台自动顶/身，
  布局参考 `Tools/room_layouts/*.txt`）→ `python Tools/generate_room_tiles.py --origin-x <列偏移>
  < 布局.txt` 得 tile_data 串 → 粘进房间场景的 TileMapLayer → 探针回归；
- **房间背景 Sky 必须 `z_index = -10`**：房间加入场景树的顺序随流式变化，
  树序在后房间的不透明背景会盖住邻房跨越边界的几何（门口墙「消失/出现」的根因）；
  z 序优先于树序，背景永远垫底；
- **房间卸载时敌人不刷新**：房间节点整体缓存（含尸体/位置/击杀状态），敌人在树外冻结、
  回窗即原样恢复；只有 World 被销毁（回主菜单）才全部重置。若将来要「进房重刷」语义，
  改为卸载时只缓存静态几何、销毁敌人节点；
- 跨格时把玩家重生点更新为当前房间的 `Spawn` 标记（死亡 → 回当前房间出生点 + 相机瞬移）；
- 相机边界是**世界级**导出（`LimitLeft/Top/Right/Bottom`），由 CameraRig 装配时应用到 PCam
  ——房间不持有相机数据；
- `World._ExitTree` 显式释放缓存中的房间（不在树上的节点不随场景树销毁）。

**新增一个房间的 checklist**：画 ASCII 布局跑生成器得地形（见上）→ 复制现有房间场景替换
Terrain 的 tile_data → 边界开口/地面高度与邻房对齐 → 在 `World.tscn` 的 `Rooms/` 下实例化
并摆到网格坐标 → 放 `Spawn` 标记 → 探针回归。

## 12. 文档索引

| 文档 | 定位 |
|---|---|
| 本文（architecture.md） | 架构入口：结构、帧序、扩展指南 |
| [character-controller-design.md](character-controller-design.md) | 角色控制器设计：四层职责推导过程、装配策略的经验来源 |
| [combat-state-machine-design.md](combat-state-machine-design.md) | 战斗状态机设计：状态切换规则、战斗闭环、AI 三层行为 |
| [camera-design.md](camera-design.md) | 相机与震屏：Phantom Camera 接线、参数迁移、调整方法 |
| [code-standards.md](code-standards.md) | 代码规范：命名、注释、装配约定、格式化与提交 |
| [engineering-roadmap.md](engineering-roadmap.md) | 工程路线图：六阶段任务与验收、下一期候选 |
| plans/ | 各阶段实施计划（历史记录，含推导与裁量点） |
