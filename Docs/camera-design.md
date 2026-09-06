# 相机与震屏设计

> 阶段⑤引入 Phantom Camera（`addons/phantom_camera/`，0.11.0.3，GDScript 运行时 + C# 包装层）
> 替换原生 Camera2D 的平滑跟随，并接通三处事件驱动震屏。本文记录节点结构、参数迁移与调整方法。

## 1. 节点结构（全部在 `Game/Scenes/Player.tscn`）

```
Player (CharacterBody2D, 继承 BaseCharacter.tscn)
├── Camera (Camera2D, 裸节点——平滑/边界属性已全部移除)
│   └── PhantomCameraHost (Node, phantom_camera_host.gd)   ← 宿主驱动这颗 Camera2D
├── PlayerPhantomCamera2D (Node2D, phantom_camera_2d.gd)   ← 相机逻辑载体
│     follow_mode = SIMPLE(2), follow_target = ../Player
├── AttackNoiseEmitter2D / HurtNoiseEmitter2D / LandNoiseEmitter2D
└── ScreenShake (Node, ScreenShake.cs)                     ← 事件→发射器接线
```

- 插件启用写在 `project.godot`：`[editor_plugins]` 注册 plugin.cfg；
  `[autoload]` 的 `PhantomCameraManager` 由 `plugin.gd` 启用逻辑注入，与编辑器内启用等价。
- **只有玩家有相机与震屏**；史莱姆复用 `BaseCharacter.tscn`，不含以上节点，落地/出招不会震屏。

## 2. 与旧 Camera2D 的参数映射

| 旧（Camera2D，已移除） | 新（PhantomCamera2D） | 说明 |
|---|---|---|
| `position_smoothing_enabled = true` | `follow_damping = true` | 开关对应 |
| `position_smoothing_speed = 8.0` | `follow_damping_value = (0.12, 0.12)` | **数学语义不等价**：前者是指数趋近速率（越大越快），后者是 SmoothDamp 时间常数（越小越锐）。0.12 ≈ 1/8s，按手感对齐选定，非公式换算 |
| `limit_left/top = 0`、`limit_right = 1280`、`limit_bottom = 720` | pcam 同名属性（同值） | 边界单一事实源移至 pcam，运行时应用到 Camera2D |

调跟随手感只动 `follow_damping_value`（Player.tscn 内），改小更跟手、改大更绵。

## 3. 震屏事件接线

`ScreenShake.cs`（表现层，沿用 `CharacterPresenter` 的 Bind/Unbind 装配模式）订阅逻辑层事件，
转发给同场景的噪声发射器（GDScript `emit()` 一次触发）。
攻击抖动用**命中确认**（`Hitbox.HitConfirmed`）而非出招（`Motor.AttackStarted`）触发——挥空不震屏：

| 事件源 | 发射器 | 噪声资源 | amplitude / frequency | duration / decay |
|---|---|---|---|---|
| `Hitbox.HitConfirmed`（攻击命中结算） | AttackNoiseEmitter2D | `Game/Config/shake_attack.tres` | 30 / 1.5 | 0.2s / 0.1s |
| `Health.Damaged`（受击） | HurtNoiseEmitter2D | `Game/Config/shake_hurt.tres` | 50 / 1.0 | 0.3s / 0.15s |
| `Motor.Landed`（落地） | LandNoiseEmitter2D | `Game/Config/shake_land.tres` | 15 / 2.0 | 0.12s / 0.08s |

- **可调**：抖动幅度/频率在 `Game/Config/shake_*.tres`（amplitude 为像素，×2 世界尺度下
  默认值 10 偏小）；时长/衰减在 Player.tscn 发射器节点属性。
- **可关**：Player.tscn 里 `ScreenShake` 节点的 `Enabled` 总开关；关闭后事件仍订阅但不触发。
- 噪声层匹配：发射器 `noise_emitter_layer` 默认 1，但 **PCam 的 `noise_emitter_layer` 默认为 0
  （不接收任何发射器）**，必须在 PCam 上显式设为 1（当前 PlayerPhantomCamera2D 已设置）。
  新增发射器或 PCam 时两侧至少要有一层对应，否则噪声被静默丢弃。
- amplitude/frequency/duration 当前值为手感初值（阶段⑤实机标定），后续可继续微调。

## 4. 边界与限制用法

- pcam 的 `limit_left/top/right/bottom` 为整数像素，必须与关卡几何对齐；
  当前关卡 ×2 后可活动区域为 (0,0)-(1280,720)，与视口一致，相机实际不滚动。
- 换正式关卡时：随关卡尺寸设置四值（衔接下一期「关卡加载与场景切换」候选——
  相机边界应随关卡数据走，而不是硬编码在场景里；届时多 PCam + priority 可做区域运镜）。
- 调试建议：编辑器里选中 pcam 可开 `draw_limits` 可视化边界框。
