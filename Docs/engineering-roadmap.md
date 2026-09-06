# 工程质量提升路线图

> 目标：在现有玩法基础上，按「工程质量优先」路线把项目向商业级标准完善。
> 三项硬要求：代码规范落地、项目架构与核心代码文档化、美术统一 32×32。
> 本文档是后续所有实施计划的总纲，各阶段完成后在「验收标准」处勾记。
>
> **当前进度（2026-09-06）**：阶段⓪-③ 已完成并勾记（25 个单元测试 + 探针基线 5/5 + CI 绿）。
> **下一步：阶段④**。实施时先写该阶段的详细计划（参照 `Docs/plans/` 中阶段⓪-③ 计划的格式），
> 审阅后再动手。下一期候选见 §5。

## 1. 现状盘点（2026-09-06）

**已具备（质量较好，直接沿用）：**

- Godot 4.6 + C#（net8.0），视口 640×360 像素风（nearest 过滤），窗口 1280×720
- 四层架构：输入 → 意图 → 逻辑 → 表现；`CharacterMotor` 为纯 C# 类，类式状态机
- 玩法已实现：移动/跳跃（土狼时间/缓冲/可变跳高）、冲刺、近战攻击、受击/死亡/重生闭环
- 史莱姆 AI（巡逻/追击/攻击）与玩家共用同一状态机，仅输入源/配置/外观不同
- 物理层命名齐全（1=player_body, 2=enemy_body, 3=player_hurtbox, 4=enemy_hurtbox）
- `Docs/` 已有两份高质量设计文档；`.editorconfig` 有基础规则；核心类有中文 XML doc

**与商业级的差距：**

1. 占位美术实际是 16×16（要求 32×32）
2. Phantom Camera 插件未启用（文件已在 `addons/`，但 `project.godot` 未注册，场景仍用原生 `Camera2D`）
3. 零测试（`Tests/` 为空）、无 CI、无代码格式化工具
4. `README.md` 与 `Docs/README.md` 仍是模板残留；两份既有设计文档局部过时
5. **仓库当前对新克隆不完整**：战斗升级新增的占位图（`player_attack/dash/death/hurt.png`、`slime_*` 系列）及 phantom_camera 部分资源未提交，但已被 `player_frames.tres` 等引用

## 2. 关键决策记录

| 决策点 | 结论 | 理由 |
|---|---|---|
| 首要目标 | 工程质量优先 | 用户拍板；玩法框架（UI/音频/新内容）后置 |
| 任务顺序 | 方案一：规范→测试→CI→美术→相机→文档 | 先立规矩、再建验证，行为类改动放在 CI 保护之后 |
| 美术尺度 | **路线 B：世界整体 ×2** | 画面构图与现状一致、仅素材精度提升；TestLevel 本就是 ColorRect 占位，重建成本低；坑宽 ×2（32/128px）保持 AI 窄坑/宽沟行为有意义 |
| 测试策略 | xUnit 纯单元测试 | Motor/Health/状态机为纯 C# 可测；不引入 GdUnit4（场景级集成测试后置） |
| 命名空间 | 保留 `GodotGameTemplate` | 重命名是无收益的全树 churn，文档记录即可；接受此外观债务 |
| 配置形态 | **Resource 编辑态 + POCO 运行态双形态**（见 3.2） | Resource 构造依赖引擎原生运行时，纯 `dotnet test` 进程无法实例化，必须拆分 |

## 3. 审阅修订（相对初版设计的修正）

### 3.1 新增阶段⓪：补提交缺失资源

初版遗漏。git 工作区中存在**已被引用但未提交**的资源，必须最先入库，否则后续每阶段都基于坏状态。

### 3.2 阶段②前置重构：配置双形态

`CharacterMotor` 直接持有 `CharacterConfig : Resource`（嵌套 `DashConfig`/`AttackConfig` 亦为 Resource）。
Godot 的 Resource 构造函数调用原生运行时，**纯 dotnet test 进程中 `new CharacterConfig()` 会崩溃**，
单元测试无法构造配置。重构方案：

- **编辑态**：`CharacterConfig : Resource` 保留，`.tres` 调参工作流不变
- **运行态**：新增纯 C# `CharacterConfigData`（含嵌套 POCO Dash/Attack 数据），
  `Character._Ready` 中一次性 Resource → POCO 映射后交给 Motor
- 原则升级：「逻辑层不碰节点」→「逻辑层不碰引擎类型」（Vector2 等纯数学类型除外）
- 改动机械性：字段类型替换，`_config.MaxSpeed` 等调用点不变

### 3.3 headless 探针持久化

战斗文档的验证探针「验证后移除」已删除，而阶段②（配置重构冒烟）与阶段④⑤（手感回归）都需要它。
修订：探针重建为持久工具 `Tools/headless_probe`（脚本化输入 + PASS/FAIL 输出），
**随阶段②建立**并首次用于重构冒烟，④⑤复用，本文档记录运行命令。

### 3.4 相机参数不预设数值

PhantomCamera2D 的阻尼语义与 Camera2D `position_smoothing_speed` 不等价，
阶段⑤目标写「调参对齐现手感」，不预设数字。

## 4. 阶段任务与验收标准

### 阶段⓪ 补提交缺失资源 ✅（2026-09-06 完成）

**任务**：提交 git status 中全部未跟踪的游戏资源（战斗占位图、phantom_camera 资源/字体/图标）。

**验收**：`git status` 干净；fresh clone 后 `dotnet build` 通过、headless 运行无缺贴图报错。

### 阶段① 代码规范落地 ✅（2026-09-06 完成）

**任务**：

1. CSharpier：`dotnet new tool-manifest` + 安装，全量格式化现有 23 个 .cs（独立提交，不混功能改动）
2. `.editorconfig` 补充 C# 命名与分析器规则；确保 `.config/dotnet-tools.json` 入库
3. 新写 `Docs/code-standards.md`：命名约定（公共成员 PascalCase、私有字段 `_camelCase`、文件名=类型名）、
   public API 必须中文 XML doc、装配约定（Resource 导出 + `_Ready` 类型发现）、目录职责

**验收**：`dotnet csharpier . --check` 通过；code-standards.md 覆盖上述四项；游戏行为零变化。

### 阶段② xUnit 测试基建（含配置双形态重构）✅（2026-09-06 完成）

**任务**：

1. 重建 headless 探针为持久工具 `Tools/headless_probe`，记录重构前基线
2. 配置双形态重构（见 3.2），以探针做前后行为对照冒烟
3. 新建 `Tests/UnitTests/GodotGameTemplate.UnitTests.csproj`（xunit + ProjectReference）
4. 最小用例集（全部 delta 驱动、纯 C#）：
   - **CharacterMotor**：土狼时间窗口内/外起跳、跳跃缓冲、可变跳高（松键截断）、
     起跳烧掉土狼+缓冲防二连跳、加减速逼近与摩擦归零
   - **状态机互斥**：Dash 中攻击被拒、Attack 中冲刺/跳跃被拒、受击打断 Dash/Attack、
     Hurt 期间无敌、Dead 为终态
   - **Health**：扣血、无敌帧拒伤、Died 事件、计时推进

**验收**：`dotnet test` 全绿；探针重构前后输出一致；用例不依赖场景树与引擎单例；主项目编译零警告。

### 阶段③ CI（GitHub Actions）✅（2026-09-06 完成）

**任务**：`.github/workflows/ci.yml`，ubuntu + .NET 8，两个 job：

- `format-check`：`dotnet tool restore` + `dotnet csharpier . --check`
- `build-and-test`：Release 构建 + `dotnet test`

**验收**：主分支 CI 绿；Godot headless 资源校验列为后续可选项，不进本期。

### 阶段④ 32×32 美术适配（世界 ×2）

**任务**：

1. 改造 `Tools/generate_placeholder_art.py` 生成 32×32 占位图（dust 8×8），重生成全部占位图
2. 更新 `player_frames.tres` / `slime_frames.tres` 帧引用与播放速度
3. `BaseCharacter.tscn` 碰撞等比：body 12×14→24×28、hitbox 14×10→28×20、hurtbox 14×16→28×32，脚底对齐
4. **世界 ×2**：TestLevel 几何全部 ×2（窄坑 16→32px、宽坑 64→128px、平台/墙体×2）
5. **数值 ×2**（时间量不变，长度/速度量×2，重力按公式导出）：
   MaxSpeed 130→260、JumpVelocity -330→-660（GravityScale 1→2，跳跃高度随之 ×2）、
   MaxFallSpeed 520→1040、Dash 420→840、击退 ×2；土狼/缓冲/硬直等时长不变
6. 视口 640×360 → **1280×720**（stretch 模式不变；窗口覆盖实施时定，建议 1920×1080）
7. **AI 感知与像素偏移参数 ×2**：Slime 三根感知射线（WallRay/LedgeNearRay/LedgeFarRay 的
   position/target_position）、ChaseDetector 半径 90→180、命中盒偏移（HitShape position (11,0)→(22,0)）、
   尘土粒子发射点；AI 攻击距离（`SlimeAIInputSource` 内）随 ×2 重新标定
8. **顺带修复已知僵持问题**：史莱姆追击停止距离大于攻击命中距离，导致停在射程外永不攻击
   （2026-09-06 探针实测：停于玩家 26px 处 12s 未出招）；×2 标定后验证追击→攻击→命中闭环
9. 更新 `Tools/headless_probe/Probe.cs` 的世界相关阈值（文件头注释已标注此项）

**验收**：`Tools/headless_probe` 回归全绿——玩家可跳上 ×2 后平台、窄坑（32px）可跳过、
宽坑（128px）AI 调头零落坑、追击/攻击触发正常（含僵持修复后命中玩家 HP 下降）；
画面构图与 16×16 时代一致。

### 阶段⑤ Phantom Camera 接入与震屏打磨

**任务**：

1. `project.godot` 启用插件（editor_plugins 注册）
2. `Player.tscn`：原生 `Camera2D` → `PhantomCamera2D` + 场景加 `PhantomCameraHost`，
   follow 玩家、边界限制对齐 ×2 后的关卡尺寸，阻尼调参对齐现手感（不预设数值）
3. **震屏打磨**：`PhantomCameraNoiseEmitter2D` 由既有事件驱动——出招（Motor.AttackStarted）、
   受击（Health.Damaged）、落地（Motor.Landed）各配一组噪声参数；
   强度/时长写进配置资源，可调可关
4. （可选）删除插件 `examples/` 目录瘦身仓库
5. 新写 `Docs/camera-design.md`：节点结构、与旧 Camera2D 的参数映射、震屏事件接线、边界与限制用法

**验收**：编辑器无插件报错；跟随手感与替换前主观一致（平滑、无越界）；
三处震屏触发正确且不打断跟随；`Docs/camera-design.md` 完成。

### 阶段⑥ 文档体系收尾

**任务**：

1. 新写 `Docs/architecture.md` 总览：目录结构、四层架构图、帧序、装配策略、物理层、
   「如何新增一种敌人/一个状态」扩展指南；索引两份既有设计文档与 camera 文档
2. 修订既有文档过时点：
   - `character-controller-design.md` 文件清单：`CharacterState.cs` 已是抽象基类（原「状态枚举」描述过时）
   - `combat-state-machine-design.md` §1「原 CharacterState 枚举删除」补充说明名称复用为基类
3. 重写根 `README.md`（项目简介/构建/测试/文档索引）与 `Docs/README.md`（文档目录）
4. public API 的 XML doc 查漏补缺

**验收**：按 architecture.md 的「新增一种敌人」指引可在一小时内完成接入且无需读源码内部实现。

## 5. 本期非目标与下一期候选

UI/HUD/血条/菜单/暂停、音频、存档、TileMap 正式关卡、GdUnit4 集成测试、
新敌人种类、连击/蓄力/弹体、本地化、正式美术资源。

**下一期候选（2026-09-06 评审确定，按优先级）：**

1. **UI 框架与开始游戏闭环**：主菜单/暂停/HUD 血条 + 菜单→游玩→死亡→重开的完整闭环
2. **关卡加载与场景切换闭环**（相机边界随关卡走，衔接阶段⑤的相机基建）

音频与正式美术资源随上述两项穿插。

## 6. 风险与备注

- **32×2 后跳跃弧线**：速度×2 + 重力×2 → 滞空时间不变、跳跃高度/距离 ×2，与几何 ×2 自洽；
  探针回归是最终裁决
- **Phantom Camera 与继承场景**：`Player.tscn` 继承自 `BaseCharacter.tscn`，相机节点在子场景，
  替换不触及基座；若遇导出属性前向解析问题，沿用「_Ready 类型发现」装配约定
- **窗口覆盖分辨率**（阶段④-6）实施时按 1080p 屏幕实际观感定，非阻塞项

## 7. 工具与环境备忘

- **探针运行命令**：`"D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  --headless --path . Tools/headless_probe/Probe.tscn`（先 `dotnet build`；5 项全过退出码 0）
- **格式化**：CSharpier 统一 1.3.0（`.config/dotnet-tools.json` 钉版；本机全局也是 1.3.0）。
  提交前必跑 `dotnet csharpier format .` + `dotnet csharpier check .`。
  **注意**：直接新建的文件可能是 CRLF，与 `.gitattributes`（`*.cs eol=lf`）不符，
  且 csharpier 对两种行尾的判定不一致——曾导致"本地通过、CI 失败"。
  新文件提交前用 format 过一遍即可归一（详见 `Docs/code-standards.md` §5）
