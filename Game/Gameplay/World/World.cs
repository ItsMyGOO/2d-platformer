using System;
using System.Collections.Generic;
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 九宫格房间流式管理：世界 = 1280×720 房间网格（Rooms 子节点按网格坐标摆放）。
/// 玩家所在格 ±1 的房间在树，其余 RemoveChild 缓存（节点不销毁——击杀/位置状态保留，
/// 回窗即恢复）。跨格时更新玩家重生点为当前房间的 Spawn 标记。
/// </summary>
public partial class World : Node2D
{
    /// <summary>房间网格尺寸（像素）。</summary>
    [Export]
    public Vector2I RoomSize { get; set; } = new(1280, 720);

    /// <summary>世界相机左边界（像素），由 CameraRig 在装配时应用。</summary>
    [Export]
    public int LimitLeft { get; set; }

    /// <summary>世界上边界（像素）。</summary>
    [Export]
    public int LimitTop { get; set; }

    /// <summary>世界右边界（像素）。宽于视口即水平卷轴。</summary>
    [Export]
    public int LimitRight { get; set; } = 1280;

    /// <summary>世界下边界（像素）。</summary>
    [Export]
    public int LimitBottom { get; set; } = 720;

    private Node2D _rooms;
    private Character _player;
    private readonly Dictionary<Vector2I, Node2D> _roomsByCell = new();
    private Vector2I _currentCell;

    public override void _Ready()
    {
        _rooms = GetNode<Node2D>("Rooms");
        foreach (Node2D room in _rooms.GetChildren())
        {
            _roomsByCell[CellOf(room.Position)] = room;
        }
        _player = GetNode<Character>("Player");
        _currentCell = CellOf(_player.GlobalPosition);
        ApplyWindow();
        if (_roomsByCell.TryGetValue(_currentCell, out Node2D spawnRoom))
        {
            _player.SetSpawnPoint(SpawnOf(spawnRoom));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2I cell = CellOf(_player.GlobalPosition);
        if (cell == _currentCell)
        {
            return;
        }
        _currentCell = cell;
        ApplyWindow();
        if (_roomsByCell.TryGetValue(cell, out Node2D currentRoom))
        {
            _player.SetSpawnPoint(SpawnOf(currentRoom));
        }
    }

    private Vector2I CellOf(Vector2 global) =>
        new((int)(global.X / RoomSize.X), (int)(global.Y / RoomSize.Y));

    private Vector2 SpawnOf(Node2D room) =>
        room.GetNodeOrNull<Marker2D>("Spawn")?.GlobalPosition ?? room.GlobalPosition;

    private void ApplyWindow()
    {
        foreach ((Vector2I cell, Node2D room) in _roomsByCell)
        {
            bool inWindow =
                Math.Abs(cell.X - _currentCell.X) <= 1 && Math.Abs(cell.Y - _currentCell.Y) <= 1;
            bool inTree = room.GetParent() != null;
            if (inWindow && !inTree)
            {
                _rooms.CallDeferred("add_child", room);
            }
            else if (!inWindow && inTree)
            {
                _rooms.CallDeferred("remove_child", room);
            }
        }
    }
}
