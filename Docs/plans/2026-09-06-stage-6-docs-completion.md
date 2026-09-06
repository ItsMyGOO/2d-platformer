# 阶段⑥实施计划：文档体系收尾

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补齐文档体系：新写 `Docs/architecture.md` 架构总览（含「新增一种敌人/一个状态」扩展指南），修订两份既有设计文档的阶段⓪-⑤过时点，重写根 `README.md` 与 `Docs/README.md`（清除模板残留），public API 中文 XML doc 查漏补缺。

**Architecture:** 纯文档 + 注释改动，**零行为变更**。唯一动代码的是补 XML doc 注释（`///`，不改任何语句）。

**Tech Stack:** Markdown、C# XML doc、CSharpier 1.3.0（注释改动会触发格式复核）。

**环境事实（本机已验证，2026-09-06）：**

- 当前基线：25 单元测试 + 探针 8/8 + CI 绿；阶段⓪-⑤ 已全部完成勾记
- 两份设计文档与 README 的过时点已逐条盘点（见 Task 2 清单，比路线图列出的两处多）
- XML doc 缺失扫描已完成：约 110 处，其中引擎 override（`_Ready`/`_Process` 等）与
  `States/*` 子类 override 占一半以上（基类补全即可，子类不重复）——扫描脚本模式已验证可用
  （`rglob('*.cs')` + 前一行非 `///` 判定，排除 addons/obj/Tests）
- `Docs/camera-design.md`（阶段⑤新增）内容与代码一致，无需修订

## 裁量点（审阅时可否决）

1. **设计文档数值直接改正文**（冲刺 420→840 等），并在阶段④变更处标注「世界 ×2 后」——
   设计文档定位为反映现状的活文档，不保留「历史数值+脚注」双轨。
2. **XML doc 补注范围**：引擎 override、`States/*` 子类 override、纯透传属性（如 `VisualState =>`）
   不逐一补注；基类/接口与逻辑层公共面（Motor/Health/DamageInfo/InputIntent/配置类）必须全覆盖。
   探针（Tools/）只补类型级 doc（已有），override 不补。
3. **character-controller-design.md 的「验证记录」保留原文并加历史标注**（16×16 时代的一次验证），
   不删除——它记录了当时的推导逻辑与 AI bug 修复过程。
4. **README 不保留英文段**（模板残留删除，纯中文；将来需要再加）。
5. **验收「一小时新增敌人」由用户实测**：计划完成并勾记后，用户按 architecture.md 指引实际接一个
   测试敌人验证；文档作者自查通过后提交。

---

### Task 1: 新写 Docs/architecture.md（核心交付物）

**Files:**
- Create: `Docs/architecture.md`

章节结构与内容要求（验收：按「新增一种敌人」指引一小时接入、无需读源码内部实现）：

- [ ] **Step 1: 按以下骨架撰写**

```markdown
# 架构总览
> 一句话定位 + 面向读者（第一次接触本仓库的开发者）

## 1. 目录结构
（真实清单：Game/{Art,Config,Gameplay,Scenes}、Tools/、Tests/、Docs/、addons/；
每个目录一句话职责；注明目录即命名空间 GodotGameTemplate.Gameplay.…）

## 2. 四层架构与帧序
（架构图：输入层 InputSource → 意图层 InputIntent → 逻辑层 CharacterMotor+States/Combat
→ 表现层 CharacterPresenter/ScreenShake；编排者 Character : CharacterBody2D 每物理帧按
Poll→Process→MoveAndSlide→PostPhysics→Presenter.Sync→ScreenShake(经事件) 顺序串联；
说明「逻辑层不碰引擎类型（Vector2 除外）」原则）

## 3. 配置双形态
（编辑态 CharacterConfig : Resource ↔ 运行态 CharacterConfigData POCO；
为什么拆：Resource 构造依赖引擎原生运行时，纯 dotnet test 进程无法实例化；
Character._Ready 一次 ToData() 映射；.tres 属性键与 C# 成员名一致）

## 4. 战斗闭环
（Health/Hitbox/Hurtbox/DamageInfo 关系图；命中结算路径 Hitbox._PhysicsProcess→ReceiveHit→OnHurt
→TryApplyDamage→ForceHurt；HitConfirmed 事件供表现层做命中反馈；死亡/重生差异：敌人 QueueFree、
玩家回出生点；物理层 1-4 命名表）

## 5. 表现层
（Presenter：动画/朝向/尘土/无敌闪烁；ScreenShake：命中/受击→噪声发射器→Phantom Camera；
指向 Docs/camera-design.md）

## 6. 装配策略
（Resource 导出 + _Ready 类型发现/约定名发现；C# 导出 NodePath 前向解析失效的坑；
GDScript 导出不受影响——Phantom Camera follow_target 用 NodePath 的原因）

## 7. 测试与验证
（xUnit 纯单测 25 个：CharacterMotor/Health/状态机互斥，TestConfig 用固定尺度数值与运行态 POCO；
headless 探针 8 项 = Tools/headless_probe，运行命令；CI 两 job）

## 8. 扩展指南：新增一种敌人（约 1 小时）
（步骤化 checklist：
① 复制 Game/Config/slime_config.tres 改数值 → <enemy>_config.tres
② 输入源：复用 SlimeAIInputSource 或新写一个继承 InputSource 的 Node 脚本（只产 InputIntent）
③ 新建 <Enemy>.tscn 继承 BaseCharacter.tscn：改 collision_layer/mask（实体层 2、判定层 4 不变则不用动）、
   换 SpriteFrames（frames tres 命名动画 idle/run/jump/fall/attack/hurt/dead）、
   换 _config、挂输入源节点；若用 AI 再加三根感知射线 + ChaseDetector（照抄 Slime.tscn）
④ 实例进 TestLevel（或新关卡）；⑤ dotnet build + 探针回归
「不需要做的事」清单：不改 Motor/States/Character——输入源与数值即差异）

## 9. 扩展指南：新增一个状态
（States/ 加 <New>State.cs 继承 CharacterState（实现 Process/VisualState，数据放 Motor 上下文）→
CharacterVisualState 枚举加值 → Motor 加路由（哪里可进入/被谁打断，参考 Dash/Attack 互斥表）→
frames .tres 加同名动画 → 单测补互斥用例）

## 10. 文档索引
（表：本文件 / character-controller-design.md / combat-state-machine-design.md / camera-design.md /
code-standards.md / engineering-roadmap.md 各一句话定位）
```

- [ ] **Step 2: 自查「新增一种敌人」章节**

对照 `Game/Scenes/Slime.tscn` 逐行核对步骤无遗漏（射线约定名、layer/mask、
`_attackDistance` 需按目标受击盒重新标定的提示）；假装从未读过源码，能否只凭文档完成。

- [ ] **Step 3: 提交**

```bash
git add Docs/architecture.md
git commit -m "docs: 新增架构总览（含新增敌人/状态扩展指南）"
```

---

### Task 2: 修订两份设计文档过时点

**Files:**
- Modify: `Docs/character-controller-design.md`
- Modify: `Docs/combat-state-machine-design.md`

- [ ] **Step 1: character-controller-design.md 修订**

| 位置 | 过时内容 | 修订为 |
|---|---|---|
| 文件清单 `CharacterState.cs` | 「状态枚举（推导式）」 | 「状态机抽象基类（见 combat 文档）」——路线图指定的修订点 |
| 文件清单整体 | 缺 `States/`、`Combat/`、`CharacterConfigData.cs`、`DashConfig/AttackConfig`、`ScreenShake.cs`、`Tools/headless_probe/` | 按当前真实结构补全（架构总览 Task 1 的目录清单为基准，此处按原文档粒度保留） |
| 文件清单 TestLevel 注释 | 「窄坑(16px)、宽坑(64px)」 | 「窄坑(32px)、宽坑(128px)」（阶段④ ×2） |
| 逻辑层小节「状态 Idle/Run…推导式」 | 已被状态机取代（头部有声明） | 句尾加「（已被类式状态机取代，保留为历史描述）」 |
| 「操作方式」 | 只有移动/跳跃 | 补冲刺 Shift/X、攻击 J/鼠标左键 |
| 「验证记录」章节 | y=321、16px 等为旧尺度实测 | 章节头加「**历史记录（16×16 时代）**，现验证基准见 Tools/headless_probe」 |
| 「已知边界」 | 「未实现重生逻辑」等已实现 | 逐条核对：已实现的移除或标注，保留仍成立的 |

- [ ] **Step 2: combat-state-machine-design.md 修订**

| 位置 | 过时内容 | 修订为 |
|---|---|---|
| §1 末句 | 「原 CharacterState 枚举删除」 | 补「其名称复用为状态抽象基类 `CharacterState`」——路线图指定的修订点 |
| §3 冲刺数值 | 「速度 420 px/s、时长 0.18s、冷却 0.6s」 | 速度 840（阶段④ ×2，时长不变）；`EndSpeedKeepRatio` 0.4 不变 |
| §4 击退相关 | 旧数值 | 核对 `player_config.tres` 现值（360/240），或删具体数值只留配置指向 |
| §8 验证 | 「headless 探针，验证后移除」 | 改为「持久工具 `Tools/headless_probe`（阶段②重建），当前 8 项检查含状态机互斥、
窄坑/宽沟、平台登顶、命中闭环」 |

- [ ] **Step 3: 提交**

```bash
git add Docs/character-controller-design.md Docs/combat-state-machine-design.md
git commit -m "docs: 修订两份设计文档的阶段⓪-⑤过时点"
```

---

### Task 3: 重写 README.md 与 Docs/README.md

**Files:**
- Modify: `README.md`
- Modify: `Docs/README.md`

- [ ] **Step 1: 根 README 重写**（模板残留全部清除，中文单语）

小节：项目一句话简介（2D 平台跳跃 · Godot 4.6 + C# · 工程质量优先路线）；
玩法与操作（移动/跳/冲刺/攻击，命中反馈震屏一句话）；环境要求（.NET 8、Godot 4.6.1 .NET 版）；
快速开始（`dotnet build`、`dotnet test`、编辑器打开运行）；回归探针（命令 + 8 项一句话说明）；
CI（两 job 说明）；文档索引（同 Docs/README 表）；工程路线（指向 engineering-roadmap.md，
阶段⓪-⑥ 已完成一句话）。

- [ ] **Step 2: Docs/README.md 重写为文档目录表**

六份文档（architecture / character-controller-design / combat-state-machine-design /
camera-design / code-standards / engineering-roadmap + plans/ 目录说明）各一行定位。

- [ ] **Step 3: 提交**

```bash
git add README.md Docs/README.md
git commit -m "docs: 重写根 README 与文档目录，清除模板残留"
```

---

### Task 4: public API XML doc 查漏补缺

**Files:**
- Modify: `Game/Gameplay/` 下若干 `.cs`（仅新增 `///` 注释行）

- [ ] **Step 1: 按优先级补注**

1. **逻辑层公共面**：`CharacterMotor` 公共属性/事件（`Jumped`/`Landed`/`Config`/`Velocity`/
   `CurrentState`/`VisualState`/`IsOnFloor`/`TimeSinceJumpPressed`/`TimeSinceLeftFloor`）、
   `Health`（`MaxHP`/`CurrentHP`/`IsInvincible`/`IsDead`/`Died`）、
   `DamageInfo`（属性 + `Create` 约束）、`InputIntent.Create`、`Character.Motor`/`Health`
2. **配置类 `[Export]` 属性**：`CharacterConfig`/`CharacterConfigData`（含嵌套 Dash/AttackData）/
   `DashConfig`/`AttackConfig`——编辑器悬停即文档；注意补「×2 后数值以 .tres 为准」不写死数值
3. **状态基类**：`CharacterState.Enter/Exit/Process/VisualState`（子类 override 不重复补）
4. **不补**：引擎 override（`_Ready` 等）、`States/*` 子类、探针 override、纯透传表达式属性
   （若补基类后仍显眼的透传如 `Motor.VisualState`，一句话即可）

- [ ] **Step 2: 扫描复核 + 格式化 + 构建 + 测试**

```bash
python Tools/xmldoc_scan.py   # 临时脚本照阶段⑥调研时的实现重建，跑完即删
dotnet csharpier format .
dotnet csharpier check .
dotnet build
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj
```
Expected: 剩余未覆盖项均为裁量点 2 的豁免类；构建 0 错误；25 测试全绿（注释不影响行为）。

- [ ] **Step 3: 提交**

```bash
git add Game/Gameplay/
git commit -m "docs: 补全逻辑层与配置类公共 API 中文 XML doc"
```

---

### Task 5: 回归、勾记与推送

- [ ] **Step 1: 全量回归（CI 等价三连 + 探针）**

```bash
dotnet csharpier check .
dotnet build GodotGameTemplate.csproj -c Release
dotnet test Tests/UnitTests/GodotGameTemplate.UnitTests.csproj -c Release
"$GODOT" --headless --path . Tools/headless_probe/Probe.tscn; echo "exit=$?"
```
Expected: 全部通过（探针 8/8、exit=0）。

- [ ] **Step 2: 路线图勾记**

`Docs/engineering-roadmap.md`：头部进度更新为「阶段⓪-⑥ 全部完成」，下一步指向 §5 下一期候选
（UI 框架与开始游戏闭环 / 关卡加载与场景切换）；`### 阶段⑥` 标题追加 `✅（2026-09-06 完成）`。

```bash
git add Docs/engineering-roadmap.md
git commit -m "docs: 勾记路线图阶段⑥完成（工程地基六阶段收官）"
```

- [ ] **Step 3: 推送并确认 CI 绿**

```bash
git push   # 代理不可用时: git -c http.proxy= -c https.proxy= push
gh run view $(gh run list --limit 1 --json databaseId --jq '.[0].databaseId')
```

- [ ] **Step 4: 验收实测（用户参与）**

按 architecture.md §8「新增一种敌人」指引实际接一个测试敌人（可仅编辑器内操作、不入库），
验证「一小时内、无需读源码内部实现」；发现指引缺口回补文档。

---

## 非目标（本期不做）

- 正式美术接入与受击盒收贴剪影（路线图 §5 备注，随下一期候选穿插）
- UI/HUD、音频、存档、关卡加载（下一期候选另行立项计划）
