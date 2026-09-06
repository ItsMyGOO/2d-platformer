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

- 格式化由 CSharpier 强制（`dotnet csharpier format .`），提交前跑 `dotnet csharpier check .`。
  `addons/` 内的第三方代码已通过 `.csharpierignore` 排除，不参与格式化。
- 提交信息用中文祈使句，前缀 type：`feat:` / `fix:` / `style:` / `refactor:` / `test:` /
  `docs:` / `chore:`。格式化提交独立成 commit，不与功能改动混合。
- 每个逻辑完整的任务单元提交一次；`dotnet build` 零 Error 后才可提交。
