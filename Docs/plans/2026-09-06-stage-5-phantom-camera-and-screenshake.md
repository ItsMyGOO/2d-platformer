# 阶段⑤实施计划：Phantom Camera 接入与震屏打磨

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 启用仓库内已存在的 Phantom Camera 插件（0.11.0.3，C# 移植版），把 Player 的原生 `Camera2D` 平滑替换为 `PhantomCamera2D` + `PhantomCameraHost`（跟随与边界对齐 ×2 后关卡），并以既有事件（出招/受击/落地）驱动噪声发射器实现震屏，强度/时长进配置资源、带总开关；新写 `Docs/camera-design.md`。

**Architecture:** 纯表现层改动，玩法逻辑零变化。相机三件套（Camera2D + Host 子节点 + PCam2D）与三个噪声发射器都放 `Player.tscn`（史莱姆不震屏）；唯一的 C# 新增是一个小型接线节点 `ScreenShake`，沿用 `CharacterPresenter` 的 Bind/Unbind 装配模式；Character 编排者补一行 Bind 调用。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、Phantom Camera 0.11.0.3（addons/ 内，GDScript 运行时 + C# 包装）、C# / net8.0、CSharpier 1.3.0。

**环境事实（本仓库已验证，2026-09-06）：**

- 插件已在 `addons/phantom_camera/`（0.11.0.3），`project.godot` 未注册（无 `[editor_plugins]`/`[autoload]` 段）
- 插件启用时 `plugin.gd` 会执行 `add_autoload_singleton("PhantomCameraManager", "res://addons/phantom_camera/scripts/managers/phantom_camera_manager.gd")`——本计划手动把等价内容写入 `project.godot`（编辑器里启用写入的就是这两段）
- **节点结构**（照抄插件示例场景 `examples/example_scenes/2D/`）：PCam2D = `Node2D` + 脚本 `phantom_camera_2d.gd`；Host = **`Camera2D` 的子节点** `Node` + `phantom_camera_host.gd`；噪声发射器 = `Node2D` + `phantom_camera_noise_emitter_2d.gd`，噪声本体 = `PhantomCameraNoise2D` 资源
- `follow_target` 是 **GDScript** 导出，在 .tscn 里以 `node_paths=PackedStringArray("follow_target")` + `NodePath("..")` 序列化（阶段①记录的「C# 导出 NodePath 静默丢弃」坑不适用于 gd 脚本）
- `FollowMode` 枚举：NONE=0 / GLUED=1 / **SIMPLE=2** / GROUP=3 / PATH=4 / FRAMED=5
- 阻尼是 **SmoothDamp 语义**：`follow_damping_value` 理想区间 0-1，越小越快越锐。与 Camera2D `position_smoothing_speed`（指数趋近速率）不等价；旧值 8 ≈ 时间常数 1/8s = 0.125，作为初值起点，**以实机手感为准微调**
- 边界：pcam 的 `limit_left/top/right/bottom`（int），运行时应用到 Camera2D；×2 关卡为 0/0/1280/720
- 噪声资源：`amplitude`（默认 10，像素）/`frequency`（默认 0.5）/`positional_noise`（默认 true）/`rotational_noise`（默认 false）/x·y 乘数；发射器：`noise`/`duration`/`growth_time`/`decay_time`/`continuous`，方法 `emit()`，层匹配 `noise_emitter_layer` ↔ `host_layers`（默认同为 1，即默认互通）
- `examples/`（743K）只被自身引用；宿主运行时引用 `panel/viewfinder`（保留 panel，不动）；CI 已在编译 addons 下的 C# 包装（现存的空引用警告即来自它，不影响绿）
- 探针 `Tools/headless_probe` 与autoload 在 headless 游戏运行中照常工作，相机替换不应影响物理——探针 8/8 是每任务的冒烟底线

## 裁量点（审阅时可否决）

1. **震屏事件归属玩家**：路线图列了三个事件但未写归属；按「相机跟随谁、谁的落地才震屏」推断只接玩家——史莱姆每次跳落地都震屏不可接受。
2. **Camera2D 上的旧属性全部移除**（`position_smoothing_*`、`limit_*`）：避免双重平滑与双份事实源；旧参数值记录进 camera-design.md 供回滚对照。
3. **`follow_damping_value` 初值 0.12**：旧 speed 8 的时间常数换算，仅起点，验收以主观手感为准（路线图 3.4 的「不预设数值」指不声称等价映射）。
4. **震屏幅度初值**：attack 30 / hurt 50 / land 15（默认 10 是旧 640×360 观感，×2 世界等比放大约 20 起步）——需实机调。
5. **`priority = 1` 显式设置**：单 PCam 场景下避免 0 优先级的激活边界行为。
6. **删除 `examples/`**（路线图可选项，采纳）：743K，无外部引用。
7. **autoload 手动写入**：与编辑器启用行为等价，避免「必须先开编辑器」的隐藏步骤。

---

### Task 1: 启用插件 + autoload 注册

**Files:**
- Modify: `project.godot`（新增 `[autoload]` 与 `[editor_plugins]` 两段）

- [ ] **Step 1: 按字母序插入两段**（`[autoload]` 在 `[application]` 后；`[editor_plugins]` 在 `[dotnet]` 后）

```ini
[autoload]

PhantomCameraManager="*res://addons/phantom_camera/scripts/managers/phantom_camera_manager.gd"
```

```ini
[editor_plugins]

enabled=PackedStringArray("res://addons/phantom_camera/plugin.cfg")
```

- [ ] **Step 2: 构建与 headless 冒烟**

```bash
dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
```
Expected: 探针 8/8、输出无 PhantomCamera 相关脚本报错（manager autoload 缺失会在 pcam 脚本里报错，此处即验证注册正确）。

- [ ] **Step 3: 提交**

```bash
git add project.godot
git commit -m "chore: 启用 Phantom Camera 插件并注册管理器 autoload"
```

---

### Task 2: Player 相机替换为 PhantomCamera2D + Host

**Files:**
- Modify: `Game/Scenes/Player.tscn`

- [ ] **Step 1: 相机三件套替换**

新增两个 ext_resource（脚本路径如下），`Camera` 节点删掉全部属性覆盖（恢复裸 Camera2D），新增 Host 子节点与 PCam2D：

```ini
[ext_resource type="Script" path="res://addons/phantom_camera/scripts/phantom_camera/phantom_camera_2d.gd" id="5_pcam"]
[ext_resource type="Script" path="res://addons/phantom_camera/scripts/phantom_camera_host/phantom_camera_host.gd" id="6_host"]
```

```ini
[node name="Camera" type="Camera2D" parent="."]

[node name="PhantomCameraHost" type="Node" parent="Camera"]
script = ExtResource("6_host")

[node name="PlayerPhantomCamera2D" type="Node2D" parent="." node_paths=PackedStringArray("follow_target")]
script = ExtResource("5_pcam")
priority = 1
follow_mode = 2
follow_target = NodePath("..")
follow_damping = true
follow_damping_value = Vector2(0.12, 0.12)
limit_left = 0
limit_top = 0
limit_right = 1280
limit_bottom = 720
```

注意 `load_steps` 计数同步 +2。被移除的旧参数（记录进 camera-design.md）：
`position_smoothing_enabled = true`、`position_smoothing_speed = 8.0`、`limit_left/top = 0`、`limit_right = 1280`、`limit_bottom = 720`。

- [ ] **Step 2: 构建与 headless 冒烟**

```bash
dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
```
Expected: 8/8，无 pcam/host 脚本报错。

- [ ] **Step 3: 编辑器点检（有界面时）**

打开编辑器确认：节点面板无红色报错、PCam2D 出现跟随预览框、运行后相机平滑跟随且不越出关卡边界。

- [ ] **Step 4: 提交**

```bash
git add Game/Scenes/Player.tscn
git commit -m "feat: Player 相机接入 Phantom Camera（SIMPLE 跟随+边界限制），原生平滑移交阻尼"
```

---

### Task 3: 三处震屏（出招/受击/落地）

**Files:**
- Create: `Game/Config/shake_attack.tres`、`shake_hurt.tres`、`shake_land.tres`
- Create: `Game/Gameplay/Characters/ScreenShake.cs`
- Modify: `Game/Scenes/Player.tscn`（三个噪声发射器 + ScreenShake 节点）
- Modify: `Game/Gameplay/Characters/Character.cs`（Bind 接线，约第 51 行 Presenter 绑定之后）

- [ ] **Step 1: 三个噪声资源（幅度初值见裁量点 4，编辑器可继续调）**

`shake_attack.tres`：

```ini
[gd_resource type="Resource" script_class="PhantomCameraNoise2D" load_steps=2 format=3]

[ext_resource type="Script" path="res://addons/phantom_camera/scripts/resources/phantom_camera_noise_2d.gd" id="1_noise"]

[resource]
script = ExtResource("1_noise")
amplitude = 30.0
frequency = 1.5
```

`shake_hurt.tres`：`amplitude = 50.0`、`frequency = 1.0`
`shake_land.tres`：`amplitude = 15.0`、`frequency = 2.0`
（其余属性用资源默认：positional_noise=true、rotational_noise=false、随机种子。）

- [ ] **Step 2: 写入 ScreenShake.cs**

```csharp
using Godot;
using GodotGameTemplate.Gameplay.Characters.Combat;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 表现层：把玩家的出招/受击/落地事件转发给本场景内的 Phantom Camera 噪声发射器。
/// 事件源由编排者经 Bind() 注入；Enabled 是震屏总开关，关闭后事件仍订阅但不触发。
/// 发射器为 GDScript 节点，按约定名发现（AttackNoiseEmitter2D / HurtNoiseEmitter2D / LandNoiseEmitter2D）。
/// </summary>
public partial class ScreenShake : Node
{
    [Export]
    public bool Enabled { get; set; } = true;

    private CharacterMotor _motor;
    private Health _health;
    private Node _attackEmitter;
    private Node _hurtEmitter;
    private Node _landEmitter;

    /// <summary>由编排者在逻辑层创建后调用，绑定引用并订阅运动与受击事件。</summary>
    public void Bind(CharacterMotor motor, Health health)
    {
        Node parent = GetParent();
        _attackEmitter ??= parent?.GetNodeOrNull("AttackNoiseEmitter2D");
        _hurtEmitter ??= parent?.GetNodeOrNull("HurtNoiseEmitter2D");
        _landEmitter ??= parent?.GetNodeOrNull("LandNoiseEmitter2D");
        Unbind();
        _motor = motor;
        _health = health;
        _motor.AttackStarted += OnAttackStarted;
        _motor.Landed += OnLanded;
        _health.Damaged += OnDamaged;
    }

    private void Unbind()
    {
        if (_motor == null)
        {
            return;
        }
        _motor.AttackStarted -= OnAttackStarted;
        _motor.Landed -= OnLanded;
        _health.Damaged -= OnDamaged;
        _motor = null;
        _health = null;
    }

    public override void _ExitTree() => Unbind();

    private void OnAttackStarted() => Emit(_attackEmitter);

    private void OnDamaged(DamageInfo info) => Emit(_hurtEmitter);

    private void OnLanded() => Emit(_landEmitter);

    private void Emit(Node emitter)
    {
        if (Enabled && emitter != null)
        {
            emitter.Call("emit"); // GDScript 方法：触发一次噪声
        }
    }
}
```

- [ ] **Step 3: Player.tscn 增加发射器与接线节点**

新增 ext_resource：`phantom_camera_noise_emitter_2d.gd`、三个 shake .tres、`ScreenShake.cs`，`load_steps` 同步 +7：

```ini
[node name="AttackNoiseEmitter2D" type="Node2D" parent="."]
script = ExtResource("7_emit")
noise = ExtResource("8_atk")
duration = 0.2
decay_time = 0.1

[node name="HurtNoiseEmitter2D" type="Node2D" parent="."]
script = ExtResource("7_emit")
noise = ExtResource("9_hurt")
duration = 0.3
decay_time = 0.15

[node name="LandNoiseEmitter2D" type="Node2D" parent="."]
script = ExtResource("7_emit")
noise = ExtResource("10_land")
duration = 0.12
decay_time = 0.08

[node name="ScreenShake" type="Node" parent="."]
script = ExtResource("11_shake")
```

- [ ] **Step 4: Character.cs 编排接线（Presenter 绑定之后）**

```csharp
        _screenShake = this.FindDescendant<ScreenShake>();
        _screenShake?.Bind(Motor, Health);
```

字段声明 `private ScreenShake _screenShake;` 放在 `_presenter` 附近。

- [ ] **Step 5: 格式化、构建、全测、冒烟**

```bash
dotnet csharpier format .
dotnet csharpier check .
dotnet build
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
```
Expected: 全绿（25 测试 + 8/8 探针），无报错。

- [ ] **Step 6: 实机验证与提交**

运行游戏逐项确认：J 出招有短促抖动、受击（挨史莱姆打）有明显晃动、跳起落地有轻微顿挫、
`ScreenShake.Enabled` 置 false 后全部消失且跟随不受影响；震屏不打断跟随（无镜头跳变）。
幅度/时长在编辑器里按手感微调 .tres 与发射器属性，调完的值随本任务提交。

```bash
git add Game/Config/shake_*.tres Game/Gameplay/Characters/ScreenShake.cs Game/Scenes/Player.tscn Game/Gameplay/Characters/Character.cs
git commit -m "feat: 玩家出招/受击/落地三处震屏接入噪声发射器，参数进配置资源可调可关"
```

---

### Task 4: 删除插件 examples/ 目录（路线图可选项，已采纳）

**Files:**
- Delete: `addons/phantom_camera/examples/`（743K，仅被自身引用；宿主运行时引用的 `panel/` 保留）

- [ ] **Step 1: 删除并验证**

```bash
git rm -r -q addons/phantom_camera/examples
dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
```
Expected: 构建通过、探针 8/8（首次运行 Godot 清理已删资源的导入缓存属预期，无脚本报错）。

- [ ] **Step 2: 提交**

```bash
git commit -q -m "chore: 删除 phantom_camera examples 目录瘦身仓库（743K，无外部引用）"
```

---

### Task 5: 文档收尾、全量回归与勾记

**Files:**
- Create: `Docs/camera-design.md`
- Modify: `Docs/engineering-roadmap.md`

- [ ] **Step 1: 写 Docs/camera-design.md**（须覆盖验收要求四项）

1. **节点结构**：Camera2D（裸）← PhantomCameraHost 子节点；PlayerPhantomCamera2D（SIMPLE 跟随父级 Player）；
   三个噪声发射器 + ScreenShake 接线节点均在 Player.tscn，史莱姆复用基座但不震屏
2. **与旧 Camera2D 的参数映射**：`position_smoothing_speed = 8.0`（指数趋近）→ `follow_damping_value = 0.12`
   （SmoothDamp 语义，越小越锐）；二者数学不等价，当前值为手感对齐结果；旧 limit_* 移至 pcam（0/0/1280/720 对齐关卡几何）
3. **震屏事件接线**：Motor.AttackStarted → AttackNoiseEmitter2D、Health.Damaged → HurtNoiseEmitter2D、
   Motor.Landed → LandNoiseEmitter2D，经 ScreenShake（Enabled 总开关）；参数表（amplitude/frequency/duration/decay 当前值）
4. **边界与限制用法**：pcam limit_* 运行时应用到 Camera2D；换关卡时随关卡尺寸设置（衔接下一期「关卡加载」候选）

- [ ] **Step 2: 全量回归（CI 等价三连 + 探针）**

```bash
dotnet csharpier check .
dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn; echo "exit=$?"
```
Expected: 全部通过，探针 8/8、exit=0。

- [ ] **Step 3: 人工验收（用户参与，主观项）**

- 编辑器无插件报错（含重开一次编辑器，确认 autoload/plugin 注册稳定）
- 跟随手感与替换前主观一致（平滑、无越界）；不满意则微调 `follow_damping_value` 后复测
- 三处震屏触发正确且不打断跟随；`Enabled=false` 全关

- [ ] **Step 4: 路线图勾记并推送**

`Docs/engineering-roadmap.md`：头部进度更新为阶段⓪-⑤完成、下一步阶段⑥；`### 阶段⑤` 标题追加 `✅（2026-09-06 完成）`。

```bash
git add Docs/
git commit -m "docs: 新增相机设计文档并勾记路线图阶段⑤完成"
git push
gh run view $(gh run list --limit 1 --json databaseId --jq '.[0].databaseId')   # 确认两 job 全绿
```

---

## 非目标（本期不做）

- 相机 lookahead / dead zone / Framed 模式等进阶跟随（当前 SIMPLE + 阻尼已对齐旧手感）
- 关卡切换时多 PCam 优先级编排（下一期「关卡加载」候选再设计）
- 受击盒贴合美术剪影的标定（已记入正式美术接入时的待办，见路线图 §5 备注）
