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
    private const int LandFrame = 90; // 阶段一末帧：自然落地
    private const int JumpEndFrame = 200; // 阶段二末帧：跳跃观察窗
    private const int TotalFrames = 920; // 阶段三末帧：史莱姆观察 ~12s

    private Character _player;
    private Character _slime1;
    private Character _slime2;
    private int _frame;

    private float _playerRestY;
    private float _playerMinY = float.MaxValue;
    private float _slimeSpawnY;
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
        // 物理未运行，GlobalPosition 即场景出生点；用它做落坑判定基准
        // （不能用运行中的 y 采样：巡逻跳跃中的史莱姆会污染基准）
        _slimeSpawnY = _slime1.GlobalPosition.Y;
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;

        if (_frame <= LandFrame)
        {
            if (_frame == LandFrame)
            {
                _playerRestY = _player.GlobalPosition.Y;
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
        float fellY = _slimeSpawnY + 25f;
        if (!_slime2Fell && _slime2.GlobalPosition.Y > fellY)
        {
            _slime2Fell = true;
            GD.Print(
                $"[trip] frame={_frame} S2 首次越界 y={_slime2.GlobalPosition.Y:F1} x={_slime2.GlobalPosition.X:F1}"
            );
        }
        if (!_slime1Fell && _slime1.GlobalPosition.Y > fellY)
        {
            _slime1Fell = true;
            GD.Print(
                $"[trip] frame={_frame} S1 首次越界 y={_slime1.GlobalPosition.Y:F1} x={_slime1.GlobalPosition.X:F1}"
            );
        }
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
