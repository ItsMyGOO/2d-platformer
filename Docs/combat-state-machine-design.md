# 战斗状态机设计文档

> 在现有 输入→意图→逻辑→表现 四层架构上，将角色/敌人的状态管理升级为类式状态机，
> 新增冲刺（玩家）、近战攻击（双方）、完整伤害闭环（HP/受击/死亡/重生），
> 并明确状态切换互斥规则。目标水准：商业级中小型项目。

## 1. 状态机架构（选型：类式状态模式）

- 状态机宿主：`CharacterMotor`（纯 C# 类，非节点，保持可单测）。
- 每个行为状态一个类（`Game/Gameplay/Characters/States/`），状态对象不持有每实例数据，
  全部共享数据（速度、朝向、计时器、配置）放在 Motor 上下文中。
- **行为状态与表现动画分离**：状态类报告 `CharacterVisualState`
  （idle/run/jump/fall/dash/attack/hurt/dead）；Grounded 内按 |vx| 细分 idle/run，
  Airborne 内按 vy 细分 jump/fall。动画永远与真实运动一致。
- 原 `CharacterState` 枚举删除，由状态类 + VisualState 取代。

## 2. 状态集合与切换规则

| 当前状态 | 可进入 | 明确禁止 |
|---|---|---|
| Grounded | 跳跃 / 冲刺(冷却好且有 DashConfig) / 攻击(冷却好且有 AttackConfig) / 受击 / 死亡 | — |
| Airborne | 冲刺(空中可) / 攻击(空中可) / 受击 / 死亡 / 落地→Grounded | 攻击中不可跳 |
| Dash | 自然结束→Grounded/Airborne（按落地事实）；可被受击打断 | 攻击输入忽略；跳跃输入忽略 |
| Attack | 自然结束→Grounded/Airborne；可被受击打断 | 冲刺输入忽略（与 Dash 双向互斥）；跳跃忽略；不可连击 |
| Hurt | 硬直计时结束→Grounded/Airborne；期间受击无敌帧 | 一切主动输入 |
| Dead | 终态；仅玩家重生时重置 | 一切输入 |

- 互斥由状态机**结构性保证**：只有 Grounded/Airborne 路由冲刺/攻击意图，
  Dash/Attack 状态根本不读这些意图。
- 跳跃手感三件套（土狼时间、跳跃缓冲、可变跳跃高度）逻辑在状态类中，
  计时器与「烧掉」规则保留在 Motor 上下文。

## 3. 冲刺（DashConfig，玩家配置）

- 面朝方向水平冲刺；进入瞬间若 MoveAxis 非零则朝该方向（可反身冲刺）。
- 速度 420 px/s、时长 0.18s、冷却 0.6s；期间重力关闭、vy 置 0。
- 结束保留部分水平速度（×0.4）衔接移动；无无敌帧。

## 4. 战斗闭环（Combat/ 子系统）

### Health（纯 C#）
- `MaxHP / CurrentHP`；`TryApplyDamage(DamageInfo)`（无敌帧期间拒绝）。
- 事件：`Damaged(DamageInfo)`、`Died`；`UpdateTimers(delta)` 由编排者驱动。

### Hitbox（Area2D）
- 攻击激活窗口内 `SetActive(true)`；每物理帧轮询 `GetOverlappingAreas`，
  命中 Hurtbox 调 `ReceiveHit`；同一次挥击对同一目标只结算一次（HashSet）。
- 自带激活时可见的调试色块。

### Hurtbox（Area2D）
- 受击入口：`ReceiveHit` 转发给所属 Character（_Ready 沿祖先链发现）。

### 逻辑层不碰节点
- Motor 事件 `AttackStarted`（清空已命中集合）/ `AttackActiveChanged(bool)`（开关判定），
  编排者订阅并驱动 Hitbox。

### AttackConfig（三段窗口）
- WindupTime（前摇）/ ActiveTime（判定开）/ RecoveryTime（后摇）/ Damage /
  KnockbackHorizontal / KnockbackVertical / Cooldown。
- 地面攻击锁水平速度；空中攻击保持惯性。受击可打断（Exit 时关判定）。

### 死亡与重生
- 敌人：Dead 状态 1s 后 QueueFree。
- 玩家：Dead 状态 1s 后回出生点、回满 HP、`Motor.Reset`（并带重生无敌帧）。
- 玩家身份由输入源类型推导（`_inputSource is PlayerInputSource`），不加标记导出。

### 物理分层
- 1=玩家实体，2=敌人实体，3=玩家判定，4=敌人判定。
- 玩家 Hitbox(mask 4) 打敌人 Hurtbox(layer 4)；敌人 Hitbox(mask 3) 打玩家 Hurtbox(layer 3)。

## 5. 敌人 AI 三层行为（输入层内部小状态机）

- Patrol：现有墙/崖探测巡逻（窄沟跳、宽沟调头）。
- Chase：ChaseDetector(Area2D, mask=玩家实体层) 发现玩家 → 朝玩家移动；
  遇宽沟停下不调头；遇墙跳；玩家脱离视野回 Patrol。
- Attack：水平距离 ≤ AttackDistance 且 AI 攻击间隔好 → `AttackPressed` 脉冲（原地出招）。
- AI 产出与玩家完全相同的 `InputIntent`，共用同一状态机。

## 6. 意图与输入

- `InputIntent` 增加 `DashPressed`、`AttackPressed`。
- InputMap 新增：`dash`（Shift/X）、`attack`（J/鼠标左键）。

## 7. 配置

- `CharacterConfig` 嵌入可空 `[Export] DashConfig Dash` / `[Export] AttackConfig Attack`（null=无此能力）。
- 新增 `MaxHP`、`InvincibilityTime`、`HurtStunTime`。
- 玩家：HP5/有冲刺/有攻击；史莱姆：HP2/无冲刺/有攻击(伤害1)。

## 8. 验证（headless 探针，验证后移除）

用 `Input.ActionPress/ActionRelease` 脚本化输入，逐项输出 PASS/FAIL：
冲刺中攻击被拒、攻击中冲刺被拒、攻击中跳跃被拒、命中扣 HP+硬直、
无敌帧拒绝重复伤害、死亡后敌人节点释放、AI 追击与攻击触发、巡逻回归。

## 9. 边界与非目标

- 不做连击段数、蓄力攻击、远程弹体、buff 系统、UI 血条。
- 敌人种类仍只有史莱姆一种（新增敌人=新 InputSource+配置+外观）。
