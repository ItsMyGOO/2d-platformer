# 第四期实施计划：弹体目标层修复 + TileMap 框架先行

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复弹体目标层反转（子弹只伤敌人不伤玩家，并引发友军火力导致敌人朝向错乱）；随后把三个房间的手摆几何迁移到 TileMapLayer（占位 tileset，与美术素材解耦——到位后只换贴图不动结构），消除「手摆碰撞+手算偏移」这类 bug 的土壤。

**Architecture:** 修复是一行反转 + 测试重写；TileMap 侧新增占位 tileset 图集（美术脚本生成）与 ASCII→tile_data 生成器（沿用本仓库 ASCII 像素画的既有模式），三个房间场景的 StaticBody2D+ColorRect 几何替换为单个 TileMapLayer。实体（敌人/拾取/检查点/KillZone/Spawn/Sky）不动。

**Tech Stack:** Godot 4.6.1 (.NET/mono)、C# / net8.0、Python 3（stdlib）、CSharpier 1.3.0。

**环境事实：**

- 基线：32 单元测试 + 探针 9/9 + CI 绿
- **Bug 根因已定位**：`ProjectileEmitter.cs:59` `_character.IsPlayer ? 4u : 8u` 写反——敌人发射拿到
  8（敌人受击盒），子弹掩码=地形|8，玩家受击盒（4）不在掩码内 → 打不中玩家、只能打中史莱姆
  （友军火力）→ 史莱姆吃击退翻转朝向 → 「玩家在右侧敌人却往左攻击」
- 上一轮弹体 e2e 测试为**假阳性**：玩家掉的血来自巡逻过来的史莱姆接触伤害（测试未隔离史莱姆）
- TileSet 图集序列化格式已从 phantom_camera 示例场景确认；TileMapLayer 的 `tile_map_data` 为
  PackedByteArray：头 2 字节版本（0）+ 每格 12 字节（int16 x, y, source_id, atlas_x, atlas_y,
  alternative，小端）——由 Python 生成器产出
- **网格对齐**：房间高 720 不能被 32 整除（地面顶 656 = 20.5 行）——TileMapLayer 统一
  `position = (0, 16)`，行 20 即世界 656，全几何对齐，游戏物理不变
- 方向决议（2026-09-06）：TileMap 框架先行；P2 完成度批次顺延 backlog

## 裁量点

1. 弹体只修正目标层与测试，不改伤害数值。
2. 迁移范围 = 三间房的地面/平台/墙体；Sky、KillZone、Spawn、实体、门口开口全部保留。
3. 占位 tileset 5 种 tile（地形顶/地形填/墙体/平台顶/平台身），正式美术期只换图集贴图与映射。
4. tile_data 用 Python 生成器产出（ASCII 布局 → 字节），生成器入 `Tools/`。
5. 探针 9 项不动——它就是迁移后的 tile 碰撞物理回归。

### Task 1: 弹体目标层修复 + e2e 测试重写
- `ProjectileEmitter.cs` 反转：`uint targetLayer = _character.IsPlayer ? 8u : 4u;` + 注释更正
- 重写测试：先移走史莱姆排除接触伤害干扰；射手置玩家右侧；断言①玩家被弹体命中掉血
  ②史莱姆满血（友军火力消失）；用后删
- 回归：构建以退出码为准；32 测试；探针 9/9

### Task 2: 占位 tileset 图集 + TileSet 资源
- 美术脚本加 TILES 图集（5×32×32 = 160×32）；产出 `placeholder_tiles.png`
- `Game/Config/placeholder_tiles.tres`：TileSetAtlasSource 5 tile，全格方形碰撞多边形，
  physics_layer_0 collision_layer=1 mask=0

### Task 3: ASCII→tile_data 生成器 `Tools/generate_room_tiles.py`
- 输入 ASCII 40×22 布局（`.`空 `#`地形自动顶/填 `W`墙 `-`平台自动顶/身）
- 输出 tile_map_data PackedByteArray 十进制串；内置 --check 校验
- 对齐约定：TileMapLayer position=(0,16)，world_y → 行 = (y-16)/32

### Task 4: 三房间迁移
- TestLevel/RoomB/RoomC 删手摆几何，加 TileMapLayer + 生成串；保留 Sky/KillZone/Spawn/实体
- 平台顶若遇 16px 半格差（原 576 非格对齐）按 560 落位，以探针余量判定
- 探针 9/9 必须全绿；FAIL 只许修布局不许放宽断言

### Task 5: 文档同步 + 全量回归 + 推送
- architecture §11 房间规范改写（ASCII→生成器→tile_data）；backlog TileMap 挂账改「仅剩贴图」
- CI 等价三连 + 探针 9/9 + 推送

### 实机验收（用户）
① 射手子弹能打中玩家；史莱姆不再被友军火力打飞、朝向不错乱；② tile 视觉/门口可见/朝向正确；
③ 手感与迁移前一致。
