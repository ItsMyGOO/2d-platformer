# 工程地基实施计划（阶段⓪-③）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补齐仓库资源完整性，落地代码规范（CSharpier + 分析器），建立 xUnit 单元测试基建（含配置双形态重构）与 headless 回归探针，并接通 GitHub Actions CI。

**Architecture:** 沿用现有 输入→意图→逻辑→表现 四层架构，不改动任何玩法行为。唯一的结构性变更是「配置双形态」：编辑态 `CharacterConfig : Resource` 保留，新增纯 C# 运行态 `CharacterConfigData`，`Character._Ready` 一次性映射——因为 Godot Resource 构造依赖引擎原生运行时，纯 `dotnet test` 进程无法实例化。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0、xUnit、CSharpier 0.29.2、GitHub Actions。

**环境事实（本机已验证）：**
- `dotnet` 8.0.418 在 PATH
- Godot 可执行文件：`D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe`（下文以 `$godot` 指代）
- `.gitignore` 不忽略 `.config/`（tool manifest 可入库）；忽略 `*.sln`（测试项目用 csproj 直跑，不入 sln）
- 工作目录 `D:\GdProject\2d-platformer`，shell 为 Git Bash

---

### Task 1: 阶段⓪ — 补提交缺失资源

**Files:**
- 提交（已存在未跟踪）: `Game/Art/Placeholders/player_{attack,dash,death,hurt}.png`、`Game/Art/Placeholders/slime_{attack,death,hurt}.png`、`addons/phantom_camera/**`（资产/字体/图标/示例贴图）

这些文件已被 `player_frames.tres` / `slime_frames.tres` / 插件场景引用，但未入库，fresh clone 即坏。

- [ ] **Step 1: 确认待提交清单**

Run: `git status --short`
Expected: 18 行 `??`（8 张战斗占位图 + 10 个 phantom_camera 资源），无其他意外文件。

- [ ] **Step 2: 暂存并提交**

```bash
git add "Game/Art/Placeholders/*.png" "addons/phantom_camera/"
git commit -m "chore: 补提交战斗占位图与相机插件资源"
git status --short
```
Expected: `git status --short` 无输出（工作区干净）。

- [ ] **Step 3: 验证构建**

Run: `dotnet build`
Expected: `Build succeeded`，0 Error（Warning 数量记录但不阻塞，Task 3 的分析器会接手）。

---

### Task 2: 阶段① — 引入 CSharpier 并全量格式化

**Files:**
- Create: `.config/dotnet-tools.json`
- Modify: 全部 23 个 `.cs`（仅格式，不改语义）

- [ ] **Step 1: 创建工具清单并安装（锁定版本）**

```bash
dotnet new tool-manifest
dotnet tool install CSharpier --version 0.29.2
```
Expected: `dotnet tool restore` 之后可用；生成 `.config/dotnet-tools.json`。

- [ ] **Step 2: 确认 manifest 不会被 gitignore**

Run: `git check-ignore .config/dotnet-tools.json && echo IGNORED || echo OK`
Expected: `OK`（环境事实已验证，此步为防回归）。

- [ ] **Step 3: 全量格式化**

Run: `dotnet csharpier .`
Expected: 输出 `Formatted N files`（N≥20）。

- [ ] **Step 4: 幂等校验 + 构建无回归**

```bash
dotnet csharpier . --check
dotnet build
```
Expected: `--check` 退出码 0；`Build succeeded`。

- [ ] **Step 5: 独立提交（不混功能改动）**

```bash
git add .config/dotnet-tools.json Game/
git commit -m "style: 引入 CSharpier 0.29.2 并全量格式化"
```

---

### Task 3: 阶段① — 增强 .editorconfig（命名规则 + 语言风格）

**Files:**
- Modify: `.editorconfig`（在现有 `[*.cs]` 段内追加）

- [ ] **Step 1: 将 `[*.cs]` 段替换为以下内容**

```editorconfig
[*.cs]
indent_style = space
indent_size = 4

# 语言风格
csharp_prefer_braces = true:warning
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion
dotnet_style_require_accessibility_modifiers = always:warning
dotnet_style_readonly_field = true:warning

# 命名规则：私有字段 _camelCase；接口 I 前缀
dotnet_naming_rule.private_fields_underscore_camel.severity = warning
dotnet_naming_rule.private_fields_underscore_camel.symbols = private_fields
dotnet_naming_rule.private_fields_underscore_camel.style = underscore_camel
dotnet_naming_rule.interfaces_prefix_i.severity = warning
dotnet_naming_rule.interfaces_prefix_i.symbols = interfaces
dotnet_naming_rule.interfaces_prefix_i.style = prefix_i

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.required_prefix = _
dotnet_naming_style.underscore_camel.capitalization = camel_case
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_style.prefix_i.required_prefix = I
dotnet_naming_style.prefix_i.capitalization = pascal_case
```

- [ ] **Step 2: 构建并核对警告**

Run: `dotnet build`
Expected: `Build succeeded`；现有代码基本满足规则（出现少量命名警告属预期——如有，修复后随本任务一并提交）。

- [ ] **Step 3: 提交**

```bash
git add .editorconfig
git commit -m "style: editorconfig 增加 C# 命名规则与语言风格"
```

---

### Task 4: 阶段① — 编写 Docs/code-standards.md

**Files:**
- Create: `Docs/code-standards.md`

- [ ] **Step 1: 写入以下完整内容**

```markdown
# 代码规范

> 强制项以「必须」标注，建议项以「建议」标注。工具强制项（CSharpier、editorconfig 命名规则）
> 由 CI 的 format-check 与构建警告兜底，人工评审关注工具管不到的部分。

## 1. 文件组织

- 目录即命名空间：`Game/Gameplay/Characters/Combat/` 下的类命名空间必须是
  `GodotGameTemplate.Gameplay.Characters.Combat`。新建目录时同步建命名空间。
- 一个文件一个顶层类型，文件名 = 类型名。
- 玩法代码放 `Game/Gameplay/`，场景放 `Game/Scenes/`，数值资源放 `Game/Config/`。
  逻辑层（Motor/Health/状态类）不得引用场景或资源路径。

## 2. 命名

| 成员 | 规则 | 示例 |
|---|---|---|
| 类型/方法/属性/公共字段 | PascalCase | `CharacterMotor`、`TryStartDash` |
| 私有字段 | `_camelCase` | `_dashCooldownTimer` |
| 常量 | PascalCase | `TimerExpired`、`DeathDuration` |
| 接口 | `I` 前缀 | `IDamageable` |
| 事件参数类 | 以 `EventArgs` 结尾 | — |

## 3. 注释

- 所有 public API 必须有中文 XML doc（`/// <summary>`），说明「做什么」与关键约束
  （如「null 表示不具备此能力」「由编排者每物理帧驱动」）。
- 注释解释意图与约束，不复述代码。无信息量的注释不写。
- 临时调试代码不提交；探针类验证工具放 `Tools/`。

## 4. 装配约定（本项目特有经验）

- **Resource 数值配置**：`[Export]` + 场景赋值；`.tres` 中的属性键必须与 C# 成员名完全一致
  （`MaxSpeed`，Godot 不做 snake_case 转换）。
- **节点引用**：C# 脚本的 Node 类型导出成员在场景实例化时无法解析前向 NodePath（静默丢弃）。
  因此节点引用在 `_Ready` 中按类型（`this.FindDescendant<T>()`）或约定名自动发现，
  导出成员仅作为显式覆盖入口。
- **逻辑层不碰引擎类型**：Motor/Health/状态类只允许 `Godot.Vector2` 等纯数学类型；
  Resource 数值经 `ToData()` 映射为 POCO（`CharacterConfigData`）后传入。

## 5. 格式化与提交

- 格式化由 CSharpier 强制（`dotnet csharpier .`），提交前跑 `dotnet csharpier . --check`。
- 提交信息用中文祈使句，前缀 type：`feat:` / `fix:` / `style:` / `refactor:` / `test:` /
  `docs:` / `chore:`。格式化提交独立成 commit，不与功能改动混合。
- 每个逻辑完整的任务单元提交一次；`dotnet build` 零 Error 后才可提交。
```

- [ ] **Step 2: 提交**

```bash
git add Docs/code-standards.md
git commit -m "docs: 新增代码规范文档"
```

---

### Task 5: 阶段② — headless 回归探针（基线工具）

**Files:**
- Create: `Tools/headless_probe/Probe.cs`
- Create: `Tools/headless_probe/Probe.tscn`

- [ ] **Step 1: 写入 Probe.cs**

```csharp
using System.Collections.Generic;
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Tools;

/// <summary>
/// headless 回归探针：脚本化驱动玩家输入并观察两只史莱姆，
/// 逐项输出 PROBE [PASS/FAIL]，全过退出码 0，否则 1。
/// 运行：$godot --headless --path . Tools/headless_probe/Probe.tscn
/// 注意：跳跃高度阈值针对当前世界尺度，阶段④世界×2 后需同步更新。
/// </summary>
public partial class Probe : Node
{
    private const int LandFrame = 90;     // 阶段一末帧：自然落地
    private const int JumpEndFrame = 200; // 阶段二末帧：跳跃观察窗
    private const int TotalFrames = 920;  // 阶段三末帧：史莱姆观察 ~12s

    private Character _player;
    private Character _slime1;
    private Character _slime2;
    private int _frame;

    private float _playerRestY;
    private float _playerMinY = float.MaxValue;
    private float _slimeRestY;
    private bool _slime1CrossedNarrowGap;
    private bool _slime2Fell;
    private bool _slime1Fell;
    private readonly List<string> _failures = new();

    public override void _Ready()
    {
        var level = GD.Load<PackedScene>("res://Game/Scenes/TestLevel.tscn").Instantiate();
        AddChild(level);
        _player = GetNode<Character>("TestLevel/Player");
        _slime1 = GetNode<Character>("TestLevel/Slime1");
        _slime2 = GetNode<Character>("TestLevel/Slime2");
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;

        if (_frame <= LandFrame)
        {
            if (_frame == LandFrame)
            {
                _playerRestY = _player.GlobalPosition.Y;
                _slimeRestY = _slime1.GlobalPosition.Y;
            }
            return;
        }

        if (_frame <= JumpEndFrame)
        {
            if (_frame == LandFrame + 1)
            {
                Input.ActionPress("jump");
            }
            if (_frame == LandFrame + 15)
            {
                Input.ActionRelease("jump");
            }
            _playerMinY = Mathf.Min(_playerMinY, _player.GlobalPosition.Y);
            return;
        }

        // 阶段三：观察史莱姆（落到出生点下方 25px 视为落坑）
        float fellY = _slimeRestY + 25f;
        _slime2Fell |= _slime2.GlobalPosition.Y > fellY;
        _slime1Fell |= _slime1.GlobalPosition.Y > fellY;
        _slime1CrossedNarrowGap |= _slime1.GlobalPosition.X < 256f;

        if (_frame == TotalFrames)
        {
            Check(_player.IsOnFloor(), "玩家自然落地");
            Check(
                _playerRestY - _playerMinY >= 45f,
                $"跳跃上升 {_playerRestY - _playerMinY:F1}px ≥ 45px"
            );
            Check(!_slime2Fell, "Slime2 在观察期内未落入宽沟");
            Check(!_slime1Fell, "Slime1 在观察期内未落坑");
            Check(_slime1CrossedNarrowGap, "Slime1 在观察期内穿过窄沟 (x<256)");
            Finish();
        }
    }

    private void Check(bool ok, string name)
    {
        GD.Print($"PROBE [{(ok ? "PASS" : "FAIL")}] {name}");
        if (!ok)
        {
            _failures.Add(name);
        }
    }

    private void Finish()
    {
        GD.Print($"PROBE SUMMARY {5 - _failures.Count}/5 通过");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}
```

- [ ] **Step 2: 写入 Probe.tscn**

```ini
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Tools/headless_probe/Probe.cs" id="1_probe"]

[node name="Probe" type="Node"]
script = ExtResource("1_probe")
```

- [ ] **Step 3: 构建并跑基线**

```bash
dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn
```
（`GODOT=D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe`）

Expected: 5 行 `PROBE [PASS]` + `PROBE SUMMARY 5/5 通过`，退出码 0。
若个别 FAIL：先判断是探针阈值问题（如 Slime1 初始朝向导致 12s 内未穿沟）还是真缺陷——
阈值问题修探针常量，真缺陷停下记录并在提交信息注明，不得静默放宽断言。

- [ ] **Step 4: 保存基线输出并提交**

```bash
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn > /tmp/probe-baseline.txt; echo "exit=$?"
git add Tools/headless_probe/
git commit -m "test: 新增 headless 回归探针并验证基线 5/5"
```
Expected: `exit=0`；基线文件内容作为 Task 7 冒烟对照。

---

### Task 6: 阶段② — xUnit 项目 + Health 测试（先行绿灯）

**Files:**
- Create: `Tests/UnitTests/GodotGameTemplate.UnitTests.csproj`
- Create: `Tests/UnitTests/HealthTests.cs`

`Health` 是零 Godot 依赖的纯 C# 类，无需重构即可测试——先立绿灯验证测试基建本身可用。

- [ ] **Step 1: 写入测试项目文件**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\GodotGameTemplate.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 写入 HealthTests.cs**

```csharp
using GodotGameTemplate.Gameplay.Characters.Combat;
using Xunit;

namespace GodotGameTemplate.UnitTests;

public class HealthTests
{
    private static DamageInfo Hit(int damage = 1) =>
        DamageInfo.Create(damage, knockbackHorizontal: 10f, knockbackVertical: 5f, sourceDirection: 1f);

    [Fact]
    public void TryApplyDamage_ReducesCurrentHp()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);

        bool applied = health.TryApplyDamage(Hit(damage: 2));

        Assert.True(applied);
        Assert.Equal(3, health.CurrentHP);
    }

    [Fact]
    public void TryApplyDamage_DuringInvincibility_IsRejected()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit());

        bool secondHit = health.TryApplyDamage(Hit());

        Assert.False(secondHit);
        Assert.Equal(4, health.CurrentHP);
    }

    [Fact]
    public void UpdateTimers_AfterInvincibilityExpires_AllowsDamageAgain()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit());

        for (int i = 0; i < 48; i++) // 0.8s 无敌帧
        {
            health.UpdateTimers(1f / 60f);
        }

        bool secondHit = health.TryApplyDamage(Hit());

        Assert.True(secondHit);
        Assert.Equal(3, health.CurrentHP);
    }

    [Fact]
    public void KillingBlow_FiresDiedOnce_AndSkipsDamaged()
    {
        var health = new Health(maxHP: 2, invincibilityTime: 0f); // 0 无敌帧便于连击
        int damagedCount = 0;
        int diedCount = 0;
        health.Damaged += _ => damagedCount++;
        health.Died += () => diedCount++;

        health.TryApplyDamage(Hit(damage: 1));
        bool killingBlow = health.TryApplyDamage(Hit(damage: 1));

        Assert.True(killingBlow);
        Assert.Equal(0, health.CurrentHP);
        Assert.True(health.IsDead);
        Assert.Equal(0, damagedCount);
        Assert.Equal(1, diedCount);
    }

    [Fact]
    public void TryApplyDamage_AfterDeath_IsRejected()
    {
        var health = new Health(maxHP: 1, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 1));

        bool after = health.TryApplyDamage(Hit());

        Assert.False(after);
    }

    [Fact]
    public void RestoreFull_RestoresHp()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 3));

        health.RestoreFull();

        Assert.Equal(5, health.CurrentHP);
        Assert.False(health.IsDead);
    }
}
```

- [ ] **Step 3: 运行测试**

Run: `dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj`
Expected: `Passed! - Failed: 0, Passed: 6`。

- [ ] **Step 4: 提交**

```bash
git add Tests/UnitTests/
git commit -m "test: 建立 xUnit 项目并覆盖 Health 行为"
```

---

### Task 7: 阶段② — Motor/状态机测试（TDD）+ 配置双形态重构

**Files:**
- Create: `Tests/UnitTests/CharacterMotorTests.cs`（先行，编译失败即红）
- Create: `Game/Gameplay/Characters/CharacterConfigData.cs`
- Modify: `Game/Gameplay/Characters/CharacterConfig.cs`（加 `ToData()`）
- Modify: `Game/Gameplay/Characters/CharacterMotor.cs:17,34,60`（字段/属性/构造参数换类型）
- Modify: `Game/Gameplay/Characters/States/DashState.cs:20`、`States/AttackState.cs:39`（局部变量改 `var`）
- Modify: `Game/Gameplay/Characters/Character.cs:48`（改传 `_config.ToData()`）

- [ ] **Step 1: 写入 CharacterMotorTests.cs（此时编译失败——CharacterConfigData 不存在）**

```csharp
using Godot;
using GodotGameTemplate.Gameplay.Characters;
using GodotGameTemplate.Gameplay.Characters.Combat;
using Xunit;

namespace GodotGameTemplate.UnitTests;

public class CharacterMotorTests
{
    private const float Dt = 1f / 60f;
    private const float Gravity = 980f;

    private static CharacterConfigData TestConfig(bool withDash = true, bool withAttack = true) => new()
    {
        MaxSpeed = 130f,
        Acceleration = 1000f,
        Friction = 1400f,
        JumpVelocity = -330f,
        GravityScale = 1f,
        JumpCutMultiplier = 0.45f,
        CoyoteTime = 0.1f,
        JumpBufferTime = 0.12f,
        MaxFallSpeed = 520f,
        MaxHP = 5,
        InvincibilityTime = 0.8f,
        HurtStunTime = 0.3f,
        Dash = withDash
            ? new CharacterConfigData.DashData
            {
                Speed = 420f,
                Duration = 0.18f,
                Cooldown = 0.6f,
                EndSpeedKeepRatio = 0.4f,
            }
            : null,
        Attack = withAttack
            ? new CharacterConfigData.AttackData
            {
                WindupTime = 0.12f,
                ActiveTime = 0.1f,
                RecoveryTime = 0.18f,
                Damage = 1,
                KnockbackHorizontal = 180f,
                KnockbackVertical = 120f,
                Cooldown = 0.5f,
            }
            : null,
    };

    private static InputIntent Intent(
        float moveAxis = 0f,
        bool jumpPressed = false,
        bool jumpHeld = false,
        bool dashPressed = false,
        bool attackPressed = false
    ) => InputIntent.Create(moveAxis, jumpPressed, jumpHeld, dashPressed, attackPressed);

    /// <summary>模拟「站在开阔地面」：每帧 Process + PostPhysics(onFloor: true)。</summary>
    private static CharacterMotor RunGrounded(CharacterMotor motor, in InputIntent intent, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            motor.Process(intent, Dt, Gravity);
            motor.PostPhysics(onFloor: true, motor.Velocity);
        }
        return motor;
    }

    /// <summary>模拟「空中」：重力由状态内部施加，PostPhysics(onFloor: false) 原样回喂。</summary>
    private static CharacterMotor RunAirborne(CharacterMotor motor, in InputIntent intent, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            motor.Process(intent, Dt, Gravity);
            motor.PostPhysics(onFloor: false, motor.Velocity);
        }
        return motor;
    }

    [Fact]
    public void Grounded_JumpInput_LiftsOffWithJumpVelocity()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 10);

        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(-330f, motor.Velocity.Y);
        Assert.False(motor.IsOnFloor);
        Assert.Equal(CharacterVisualState.Jump, motor.VisualState);
    }

    [Fact]
    public void Jump_AirborneJumpInput_CannotRejump_CoyoteBurned()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);
        motor.PostPhysics(false, motor.Velocity);
        Assert.Equal(1, jumps);

        RunAirborne(motor, Intent(jumpPressed: true, jumpHeld: true), 30);

        Assert.Equal(1, jumps); // 起跳瞬间烧掉土狼时间，空中新脉冲无效
        Assert.True(motor.Velocity.Y > -330f); // 速度已被重力衰减而非重置为起跳速度
    }

    [Fact]
    public void CoyoteTime_JumpWithinWindow_Works()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);

        // 走出平台边缘：地面事实消失
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(false, motor.Velocity);

        RunAirborne(motor, Intent(), 3); // 0.05s ≤ 土狼 0.1s
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(1, jumps);
        Assert.Equal(CharacterVisualState.Jump, motor.VisualState);
    }

    [Fact]
    public void CoyoteTime_AfterWindow_JumpRejected()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(false, motor.Velocity);

        RunAirborne(motor, Intent(), 12); // 0.2s > 土狼 0.1s
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(0, jumps);
        Assert.Equal(CharacterVisualState.Fall, motor.VisualState);
    }

    [Fact]
    public void JumpBuffer_PressedBeforeLanding_FiresOnLanding()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity); // 起跳
        motor.PostPhysics(false, motor.Velocity);

        RunAirborne(motor, Intent(), 40); // 下落 ~0.67s，土狼已过期
        RunAirborne(motor, Intent(jumpPressed: true, jumpHeld: true), 3); // 落地前 0.05s 按跳
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(true, motor.Velocity); // 落地帧 → 回地面状态

        motor.Process(Intent(), Dt, Gravity); // 落地后一帧：缓冲跳触发

        Assert.Equal(2, jumps);
        Assert.False(motor.IsOnFloor);
    }

    [Fact]
    public void VariableJumpHeight_ReleasingEarly_CutsRise()
    {
        var held = new CharacterMotor(TestConfig());
        RunGrounded(held, Intent(), 5);
        held.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);
        held.PostPhysics(false, held.Velocity);
        RunAirborne(held, Intent(jumpHeld: true), 2); // 持续按住

        var released = new CharacterMotor(TestConfig());
        RunGrounded(released, Intent(), 5);
        released.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);
        released.PostPhysics(false, released.Velocity);
        RunAirborne(released, Intent(jumpHeld: false), 2); // 立即松开

        Assert.True(held.Velocity.Y < -250f); // 未截断
        Assert.True(released.Velocity.Y > -200f); // 截断到 ~45%
    }

    [Fact]
    public void Dash_Grounded_EntersAndEndsWithKeepRatio()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);

        motor.Process(Intent(dashPressed: true), Dt, Gravity);
        Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
        Assert.Equal(420f, motor.Velocity.X);
        motor.PostPhysics(true, motor.Velocity);

        for (int i = 0; i < 10; i++) // 冲刺持续 ~0.18s
        {
            motor.Process(Intent(), Dt, Gravity);
            Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
            motor.PostPhysics(true, motor.Velocity);
        }

        motor.Process(Intent(), Dt, Gravity); // 耗尽帧
        Assert.NotEqual(CharacterVisualState.Dash, motor.VisualState);
        Assert.Equal(168f, motor.Velocity.X, precision: 1); // 420 × 0.4
    }

    [Fact]
    public void Dash_WhileActive_IgnoresJumpAndAttack()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(dashPressed: true), Dt, Gravity);
        motor.PostPhysics(true, motor.Velocity);

        motor.Process(
            Intent(jumpPressed: true, jumpHeld: true, attackPressed: true, dashPressed: true),
            Dt,
            Gravity
        );

        Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
    }

    [Fact]
    public void Dash_Cooldown_PreventsImmediateRedash()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        RunGrounded(motor, Intent(dashPressed: true), 12); // 冲刺并耗尽

        RunGrounded(motor, Intent(dashPressed: true), 5); // 冷却中（0.6s）

        Assert.NotEqual(CharacterVisualState.Dash, motor.VisualState);
        RunGrounded(motor, Intent(dashPressed: true), 30); // 冷却结束帧自动再冲
        Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
    }

    [Fact]
    public void Attack_Grounded_RunsWindows_ThenReturns()
    {
        var motor = new CharacterMotor(TestConfig());
        int started = 0;
        int activeOn = 0;
        int activeOff = 0;
        motor.AttackStarted += () => started++;
        motor.AttackActiveChanged += active =>
        {
            if (active)
            {
                activeOn++;
            }
            else
            {
                activeOff++;
            }
        };
        RunGrounded(motor, Intent(), 5);

        motor.Process(Intent(attackPressed: true), Dt, Gravity);
        Assert.Equal(CharacterVisualState.Attack, motor.VisualState);
        Assert.Equal(0f, motor.Velocity.X); // 地面攻击定身
        motor.PostPhysics(true, motor.Velocity);

        int attackFrames = 0;
        for (int i = 0; i < 30; i++)
        {
            motor.Process(Intent(), Dt, Gravity);
            motor.PostPhysics(true, motor.Velocity);
            if (motor.VisualState == CharacterVisualState.Attack)
            {
                attackFrames++;
            }
        }

        Assert.Equal(1, started);
        Assert.Equal(1, activeOn);
        Assert.Equal(1, activeOff);
        Assert.InRange(attackFrames, 20, 26); // 三段窗口总长 0.40s ≈ 24 帧
        Assert.NotEqual(CharacterVisualState.Attack, motor.VisualState);
    }

    [Fact]
    public void Attack_WhileActive_IgnoresDashAndJump()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(attackPressed: true), Dt, Gravity);
        motor.PostPhysics(true, motor.Velocity);

        motor.Process(Intent(dashPressed: true, jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(CharacterVisualState.Attack, motor.VisualState);
    }

    [Fact]
    public void ForceHurt_InterruptsDash_EntersHurt_ThenRecovers()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(dashPressed: true), Dt, Gravity);

        motor.ForceHurt(
            DamageInfo.Create(damage: 1, knockbackHorizontal: 180f, knockbackVertical: 120f, sourceDirection: -1f)
        );

        Assert.Equal(CharacterVisualState.Hurt, motor.VisualState);
        Assert.Equal(-180f, motor.Velocity.X);
        Assert.Equal(-120f, motor.Velocity.Y);

        RunGrounded(motor, Intent(), 20); // 0.33s > 硬直 0.3s

        Assert.NotEqual(CharacterVisualState.Hurt, motor.VisualState);
    }

    [Fact]
    public void Hurt_DuringStun_IgnoresAllInput()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.ForceHurt(
            DamageInfo.Create(damage: 1, knockbackHorizontal: 180f, knockbackVertical: 120f, sourceDirection: 1f)
        );

        motor.Process(
            Intent(moveAxis: 1f, jumpPressed: true, jumpHeld: true, dashPressed: true, attackPressed: true),
            Dt,
            Gravity
        );

        Assert.Equal(CharacterVisualState.Hurt, motor.VisualState);
    }

    [Fact]
    public void Kill_EntersDeadState_WhichIgnoresHurtAndInput()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);

        motor.Kill();

        Assert.Equal(CharacterVisualState.Dead, motor.VisualState);
        Assert.Equal(-140f, motor.Velocity.Y); // 死亡小跳

        motor.Kill();
        motor.ForceHurt(DamageInfo.Create(damage: 1, knockbackHorizontal: 100f, knockbackVertical: 100f, sourceDirection: 1f));
        motor.Process(Intent(moveAxis: 1f, jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(CharacterVisualState.Dead, motor.VisualState);
    }

    [Fact]
    public void Reset_RestoresGroundedLocomotion()
    {
        var motor = new CharacterMotor(TestConfig());
        motor.Kill();

        motor.Reset();

        Assert.Equal(CharacterVisualState.Idle, motor.VisualState);
        Assert.True(motor.IsOnFloor);
        Assert.Equal(Vector2.Zero, motor.Velocity);
    }

    [Fact]
    public void MoveAxis_UpdatesFacing()
    {
        var motor = new CharacterMotor(TestConfig());

        RunGrounded(motor, Intent(moveAxis: -1f), 3);

        Assert.Equal(-1, motor.Facing);
        Assert.True(motor.Velocity.X < 0f);
    }

    [Fact]
    public void ForceHurt_InterruptsAttack()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(attackPressed: true), Dt, Gravity);
        Assert.Equal(CharacterVisualState.Attack, motor.VisualState);

        motor.ForceHurt(DamageInfo.Create(damage: 1, knockbackHorizontal: 100f, knockbackVertical: 50f, sourceDirection: 1f));

        Assert.Equal(CharacterVisualState.Hurt, motor.VisualState);
    }

    [Fact]
    public void MoveAxis_Release_DeceleratesToStopByFriction()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(moveAxis: 1f), 30); // 1000/60 ≈ 16.7/帧，30 帧内到达 MaxSpeed
        Assert.Equal(130f, motor.Velocity.X, precision: 1);

        RunGrounded(motor, Intent(), 30); // 摩擦 1400/60 ≈ 23.3/帧，6 帧内归零

        Assert.Equal(0f, motor.Velocity.X);
    }

    [Fact]
    public void MissingDashConfig_DashInputIsIgnored()
    {
        var motor = new CharacterMotor(TestConfig(withDash: false));

        RunGrounded(motor, Intent(dashPressed: true), 5);

        Assert.Equal(CharacterVisualState.Idle, motor.VisualState);
    }
}
```

- [ ] **Step 2: 运行测试确认「红」（编译失败：类型不存在）**

Run: `dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj`
Expected: 编译错误 `CS0246: CharacterConfigData 未找到`（这就是失败态）。

- [ ] **Step 3: 创建 CharacterConfigData.cs（运行态 POCO）**

```csharp
namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 逻辑层数值配置的运行态（纯 C# POCO）。
/// 编辑器内仍用 CharacterConfig(Resource) 调参，Character._Ready 中经 ToData() 映射为本类型。
/// 拆分原因：Resource 构造依赖引擎原生运行时，纯单元测试进程无法实例化。
/// </summary>
public class CharacterConfigData
{
    public float MaxSpeed { get; init; }

    public float Acceleration { get; init; }

    public float Friction { get; init; }

    public float JumpVelocity { get; init; }

    public float GravityScale { get; init; }

    public float JumpCutMultiplier { get; init; }

    public float CoyoteTime { get; init; }

    public float JumpBufferTime { get; init; }

    public float MaxFallSpeed { get; init; }

    public int MaxHP { get; init; }

    public float InvincibilityTime { get; init; }

    public float HurtStunTime { get; init; }

    /// <summary>冲刺能力；null 表示不具备。</summary>
    public DashData Dash { get; init; }

    /// <summary>攻击能力；null 表示不具备。</summary>
    public AttackData Attack { get; init; }

    public class DashData
    {
        public float Speed { get; init; }

        public float Duration { get; init; }

        public float Cooldown { get; init; }

        public float EndSpeedKeepRatio { get; init; }
    }

    public class AttackData
    {
        public float WindupTime { get; init; }

        public float ActiveTime { get; init; }

        public float RecoveryTime { get; init; }

        public int Damage { get; init; }

        public float KnockbackHorizontal { get; init; }

        public float KnockbackVertical { get; init; }

        public float Cooldown { get; init; }
    }
}
```

- [ ] **Step 4: CharacterConfig.cs 追加 ToData()（放在类末尾、`}` 之前）**

```csharp
    /// <summary>映射为逻辑层运行态配置（见 <see cref="CharacterConfigData"/>）。</summary>
    public CharacterConfigData ToData() => new()
    {
        MaxSpeed = MaxSpeed,
        Acceleration = Acceleration,
        Friction = Friction,
        JumpVelocity = JumpVelocity,
        GravityScale = GravityScale,
        JumpCutMultiplier = JumpCutMultiplier,
        CoyoteTime = CoyoteTime,
        JumpBufferTime = JumpBufferTime,
        MaxFallSpeed = MaxFallSpeed,
        MaxHP = MaxHP,
        InvincibilityTime = InvincibilityTime,
        HurtStunTime = HurtStunTime,
        Dash = Dash == null
            ? null
            : new CharacterConfigData.DashData
            {
                Speed = Dash.Speed,
                Duration = Dash.Duration,
                Cooldown = Dash.Cooldown,
                EndSpeedKeepRatio = Dash.EndSpeedKeepRatio,
            },
        Attack = Attack == null
            ? null
            : new CharacterConfigData.AttackData
            {
                WindupTime = Attack.WindupTime,
                ActiveTime = Attack.ActiveTime,
                RecoveryTime = Attack.RecoveryTime,
                Damage = Attack.Damage,
                KnockbackHorizontal = Attack.KnockbackHorizontal,
                KnockbackVertical = Attack.KnockbackVertical,
                Cooldown = Attack.Cooldown,
            },
    };
```

- [ ] **Step 5: CharacterMotor.cs 三处换类型**

- 第 17 行：`private readonly CharacterConfig _config;` → `private readonly CharacterConfigData _config;`
- 第 34 行：`public CharacterConfig Config => _config;` → `public CharacterConfigData Config => _config;`
- 第 60 行：`public CharacterMotor(CharacterConfig config)` → `public CharacterMotor(CharacterConfigData config)`

- [ ] **Step 6: 两个状态类的局部变量改 var（引用类型随 Config 属性自动切换）**

- `DashState.cs` 第 20 行：`DashConfig dash = Motor.Config.Dash;` → `var dash = Motor.Config.Dash;`
- `AttackState.cs` 第 39 行：`AttackConfig attack = Motor.Config.Attack;` → `var attack = Motor.Config.Attack;`

- [ ] **Step 7: Character.cs 第 48 行改传运行态**

`Motor = new CharacterMotor(_config);` → `Motor = new CharacterMotor(_config.ToData());`

- [ ] **Step 8: 全绿**

Run: `dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj`
Expected: `Passed! - Failed: 0, Passed: 25`（6 Health + 19 Motor）。

- [ ] **Step 9: headless 冒烟对照基线**

```bash
GODOT="D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
dotnet build
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn > /tmp/probe-refactor.txt; echo "exit=$?"
diff /tmp/probe-baseline.txt /tmp/probe-refactor.txt && echo "BEHAVIOR UNCHANGED"
```
Expected: `exit=0` 且 diff 无差异。

- [ ] **Step 10: 提交**

```bash
git add Tests/UnitTests/CharacterMotorTests.cs Game/Gameplay/Characters/
git commit -m "refactor: 配置拆分编辑态/运行态双形态，Motor 与状态机接入 POCO（含 19 个状态机测试）"
```

---

### Task 8: 阶段③ — GitHub Actions CI

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: 写入 ci.yml**

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  format-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - name: 还原工具
        run: dotnet tool restore
      - name: 检查代码格式
        run: dotnet csharpier . --check

  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - name: 还原依赖
        run: dotnet restore GodotGameTemplate.csproj
      - name: 构建
        run: dotnet build GodotGameTemplate.csproj -c Release --no-restore
      - name: 测试
        run: dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
```

- [ ] **Step 2: 本地预演 CI 三个命令**

```bash
dotnet csharpier . --check
dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
```
Expected: 三个命令全部成功（格式通过、Build succeeded、23 个测试通过）。

- [ ] **Step 3: 确认远端并提交**

```bash
git remote -v
git add .github/workflows/ci.yml
git commit -m "ci: 新增格式检查与构建测试流水线"
```
Expected: 有 remote 时直接 `git push` 触发 CI 观察结果；无 remote 则停在此处，
向用户说明需要先建 GitHub 仓库并配置 remote（推送属外发操作，须用户确认）。

- [ ] **Step 4: 阶段收尾**

在 `Docs/engineering-roadmap.md` 的阶段⓪-③验收标准处勾记，提交：

```bash
git add Docs/engineering-roadmap.md
git commit -m "docs: 勾记路线图阶段⓪-③完成"
```
