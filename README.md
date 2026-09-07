# 2D Platformer

Godot 4.6 + C#（net8.0）的 2D 横板平台跳跃，按「工程质量优先」路线建设：
输入 → 意图 → 逻辑 → 表现 四层架构，玩家与敌人（史莱姆 AI）共用同一套状态机，
单元测试 + headless 探针 + CI 守护回归。架构入口见 [Docs/architecture.md](Docs/architecture.md)。

## 玩法与操作

| 操作 | 按键 |
|---|---|
| 移动 | A/D 或 ←/→ |
| 跳跃 | 空格 或 ↑（短按小跳、长按大跳） |
| 冲刺 | Shift 或 X |
| 攻击 | J 或 鼠标左键 |
| 暂停 | Esc |

击中敌人会触发相机命中震屏（参数见 `Docs/camera-design.md`，可调可关）；
与敌人身体接触也会受击（无敌帧限频，单位之间可互相穿越）；
掉进坑里即死（不扣血），回当前房间出生点重生、血量回满、相机瞬移。
世界由 1280×720 的房间拼成，跨房间无缝行走（远端房间自动休眠、回来即恢复原状）。

## 环境要求

- .NET SDK 8.0
- Godot 4.6.1 **.NET/mono** 版（本项目用 `D:\Godot_v4.6.1-stable_mono_win64\...`，路径可按需调整）
- Git Bash / PowerShell 均可

## 快速开始

```bash
dotnet build                 # 构建（零 Error 才可提交）
dotnet test                  # 32 个单元测试（纯 C#，无场景依赖）
```

用 Godot 打开项目直接 F5 运行（主场景 `Game/Scenes/Main.tscn`：主菜单 → 开始游戏）。

## 回归探针

headless 探针脚本化驱动输入，9 项检查逐条输出 PASS/FAIL（跳跃高度、跳上平台、
落坑死亡重生、窄坑穿越、宽坑调头、追击出招、命中扣血等），全过退出码 0：

```bash
dotnet build
"D:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" \
    --headless --path . Tools/headless_probe/Probe.tscn
```

行为改动（数值、场景几何、AI、战斗）提交前必须跑一遍。

## CI

GitHub Actions 两个 job：`format-check`（CSharpier 检查）+ `build-and-test`（Release 构建 + 单测）。
提交前本地跑 `dotnet csharpier format .` + `dotnet csharpier check .`（规范见 [Docs/code-standards.md](Docs/code-standards.md)）。

## 文档

| 文档 | 内容 |
|---|---|
| [Docs/architecture.md](Docs/architecture.md) | 架构总览 + 新增敌人/状态扩展指南（入口） |
| [Docs/character-controller-design.md](Docs/character-controller-design.md) | 角色控制器四层设计 |
| [Docs/combat-state-machine-design.md](Docs/combat-state-machine-design.md) | 战斗状态机与战斗闭环 |
| [Docs/camera-design.md](Docs/camera-design.md) | 相机跟随与震屏 |
| [Docs/code-standards.md](Docs/code-standards.md) | 代码规范与提交约定 |
| [Docs/engineering-roadmap.md](Docs/engineering-roadmap.md) | 工程路线图（六阶段已收官，下一期候选见 §5） |

工程履历：工程地基阶段⓪-⑥（资源补齐 → 代码规范 → 测试基建 → CI → 32×32 美术与世界 ×2 →
Phantom Camera 相机与震屏 → 文档收尾）已全部完成；第二期交付 UI 闭环 + 落坑即死 + 战斗代码
审计加固；第三期交付单位穿透与接触伤害 + 九宫格房间流式无缝世界。各期实施计划在 `Docs/plans/`。
