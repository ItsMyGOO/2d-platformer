# 角色控制器设计文档

> 输入 → 意图 → 逻辑 → 表现 四层架构的 2D 平台跳跃角色控制器。
> 玩家与敌人（史莱姆）共用同一套逻辑代码，仅输入源、数值配置与外观不同。

## 架构总览

```
输入层            意图层             逻辑层                表现层
InputSource  ──▶  InputIntent  ──▶  CharacterMotor  ──▶  CharacterPresenter
(可替换 Node)     (纯数据 struct)    (纯 C# 类, 非Node)    (动画/朝向/粒子)
   │                                     ▲
   ├─ PlayerInputSource  (读 InputMap)   │ 每帧回喂 IsOnFloor
   └─ SlimeAIInputSource (射线感知)      ▼
                                  Character : CharacterBody2D (编排者)
```

每物理帧在 `Character._PhysicsProcess` 中按固定顺序串联（帧序确定，保证手感可复现）：

1. `intent = inputSource.Poll(dt)` —— 输入 → 意图
2. `motor.Process(intent, dt, gravity)` —— 意图 → 逻辑（推进计时器、计算速度）
3. `Velocity = motor.Velocity; MoveAndSlide()` —— 物理执行
4. `motor.PostPhysics(IsOnFloor(), Velocity)` —— 回喂物理事实（土狼计时、落地检测）
5. `presenter.Sync()` —— 逻辑 → 表现

**共用代码的关键**：`SlimeAIInputSource` 与 `PlayerInputSource` 产出完全相同的
`InputIntent`，逻辑层不知道也不关心意图来自键盘还是 AI。玩家/敌人的差异只有三处：
换输入源子节点、换 `CharacterConfig` 数值资源、换 SpriteFrames 外观。

## 各层职责

### 意图层 — `InputIntent`（readonly struct）
`MoveAxis`(-1..1)、`JumpPressed`(本帧脉冲)、`JumpHeld`(持续按住)。只描述"想做什么"，
不含任何执行细节；可序列化，便于将来录制回放或接网络输入。

### 输入层 — `InputSource`（abstract Node）
唯一职责：每物理帧被轮询一次，产出一帧意图。
- `PlayerInputSource`：包装 `Input.GetAxis / IsActionJustPressed / IsActionPressed`。
- `SlimeAIInputSource`：三根 RayCast2D 感知（前方墙、近端崖沿、远端崖沿）。
  撞墙或悬崖调头；近端探到沟、远端（起点已越过崖沿）仍探到地面 → 判定窄沟并起跳；
  远端也探不到 → 沟太宽 → 调头。AI 无任何 `Input` 单例依赖。
- 输入层可消费角色回喂的物理事实（`NotifyGrounded`），但不直接驱动角色。

### 逻辑层 — `CharacterMotor`（纯 C# 类，不挂场景树）
- 水平：`MoveTowards` 逼近 `MoveAxis * MaxSpeed`，无输入时按 Friction 减速。
- 跳跃手感三件套：
  - 土狼时间（离地 CoyoteTime 内仍可起跳）；
  - 跳跃缓冲（落地前 JumpBufferTime 内按跳生效）；
  - 可变跳跃高度（上升中松开跳跃键，上升速度乘 JumpCutMultiplier，一次性）。
- 状态 `Idle/Run/Jump/Fall` 为推导式：由地面事实 + 速度实时计算，不是独立状态机。
- 事件 `Jumped` / `Landed`（C# event）供表现层订阅。
- 起跳后立即烧掉土狼时间与缓冲，杜绝同帧二连跳。
- 数值全部来自 `CharacterConfig : Resource`；玩家与史莱姆各一份 `.tres`。
- 不依赖场景树，将来可直接写 xUnit 单元测试（仅需注入 delta 驱动）。

### 表现层 — `CharacterPresenter`
读 Motor 状态切换动画（idle/run/jump/fall）、按 Facing 翻转、订阅事件播落地/起跳尘土。
只读逻辑层，绝不反向写入。

### 编排者 — `Character : CharacterBody2D`
只做装配与按帧序调用，不含玩法规则。装配策略见下节。

## 装配策略（重要经验）

C# 脚本的 **Node 类型导出成员在场景实例化时无法解析前向 NodePath**：
属性按场景文件序应用，目标节点彼时尚未创建，赋值被静默丢弃（Resource 类型导出不受影响）。
因此本项目采用两类装配方式：

- **Resource（数值配置）**：正常用 `[Export]` + 场景赋值（`_config = ExtResource(...)`）。
  注意：.tres 中的属性键必须与 C# 成员名完全一致（`MaxSpeed`，不转 snake_case）。
- **节点引用（输入源/表现层/精灵/射线）**：在 `_Ready` 中按类型或约定名自动发现：
  - `Character` 深度优先查找后代中第一个 `InputSource` / `CharacterPresenter`；
  - `CharacterPresenter` 在角色根直接子节点中按类型发现 `AnimatedSprite2D` / `CpuParticles2D`；
  - `SlimeAIInputSource` 按约定名发现同级 `WallRay` / `LedgeNearRay` / `LedgeFarRay`。
  导出成员保留作为显式覆盖入口（编辑器内手动指定仍然可用）。

## 文件清单

```
Game/Gameplay/Characters/
├── Character.cs                  # 编排者（CharacterBody2D）
├── CharacterConfig.cs            # Resource 数值配置
├── CharacterMotor.cs             # 逻辑层（纯 C# 类）
├── CharacterState.cs             # 状态枚举（推导式）
├── InputIntent.cs                # 意图数据（readonly struct）
├── CharacterPresenter.cs         # 表现层
└── InputSources/
    ├── InputSource.cs            # 输入层抽象（abstract Node）
    ├── PlayerInputSource.cs      # 玩家键盘输入
    └── SlimeAIInputSource.cs     # 史莱姆 AI（射线感知巡逻）
Game/Config/
├── player_config.tres            # 玩家数值
└── slime_config.tres             # 史莱姆数值
Game/Art/Placeholders/            # 占位像素图（Tools/generate_placeholder_art.py 生成）
Game/Scenes/
├── BaseCharacter.tscn            # 通用角色基座（场景继承）
├── Player.tscn                   # 继承 + 玩家输入源 + 相机
├── Slime.tscn                    # 继承 + AI 输入源 + 三根感知射线（collision_layer=2）
└── TestLevel.tscn                # 主场景：平台、窄坑(16px)、宽坑(64px)、悬浮平台
Tools/
└── generate_placeholder_art.py   # 占位像素图生成脚本（纯 zlib，无 PIL 依赖）
```

## 操作方式

- 移动：A/D 或 ←/→
- 跳跃：空格 或 ↑（短按小跳、长按大跳）

## 验证记录（headless 运行）

- 编译零错误零警告；运行零脚本错误。
- 玩家落地静止 y=321（与地形几何吻合），状态推导正确。
- 史莱姆（AI 驱动，与玩家共用同一 Motor/场景代码）12 秒全周期行为：
  - 从 x=340 巡逻至左边界后调头 ✓
  - 16px 窄坑双向跳过 ✓
  - 64px 宽坑前调头，全程零落坑 ✓
- 修复过的 AI bug：远端崖沿线起点必须越过崖沿再探测，
  否则线段先命中近侧地面、把宽沟误判为窄沟而起跳落坑。

## 已知边界（后续可扩展）

- 角色落入坑底会被底部接住（y=500），未实现重生逻辑。
- 状态为推导式；将来加冲刺/二段跳/受击时再演进为显式状态机。
- Motor 保持纯 C# 类，补 xUnit 测试时无需改动。
