using System.Collections.Generic;
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Tools;

/// <summary>
/// headless 回归探针：脚本化驱动玩家输入并观察两只史莱姆，
/// 逐项输出 PROBE [PASS/FAIL]，全过退出码 0，否则 1。
/// 运行：$godot --headless --path . Tools/headless_probe/Probe.tscn
/// 阈值针对阶段④ ×2 后的世界尺度：地面站立中心 y≈642、平台顶 560（站立 y≈546）、
/// 窄坑 [512,544]、宽坑 [992,1120]。
/// </summary>
public partial class Probe : Node
{
    private const int LandFrame = 90; // 阶段一末帧：自然落地
    private const int ManeuverEndFrame = 430; // 阶段二末帧：助跑跳平台 + 跑坑死亡重生机动窗
    private const int TotalFrames = 1240; // 阶段三末帧：史莱姆观察 ~13.5s

    // 机动窗按键帧（60fps 物理帧，理论推算值；相关 FAIL 时按实测输出微调）
    private const int RunPressFrame = 91; // 开始向右助跑
    private const int JumpPressFrame = 105; // 助跑 14 帧后满跳（不提前松键）
    private const int JumpReleaseFrame = 125; // 已过最高点，松键不影响高度
    private const int RunReleaseFrame = 165; // 落回地面后停住
    private const int PitRunPressFrame = 180; // 二段助跑：直奔宽坑

    private const float MinJumpRise = 90f; // 满跳理论 ~111px；登平台需 96px
    private const float PlatformTopY = 600f; // 站上平台判定线（介于 546 与地面 642 之间）
    private const float PlatformMinX = 250f;
    private const float PlatformMaxX = 360f;
    private const float PitFallY = 900f; // 触达即死区高度（KillZone 顶 928，身体先入区）
    private const float FellOffsetY = 50f; // 落到出生点下方 50px 视为落坑
    private const float NarrowGapX = 512f; // 窄坑左沿，过线即穿坑

    private Character _player;
    private Character _slime1;
    private Character _slime2;
    private int _frame;

    private float _playerRestY;
    private float _playerMinY = float.MaxValue;
    private float _slimeSpawnY;
    private int _playerMinHP = int.MaxValue;
    private int _slime1AttackCount;
    private bool _playerWasOnPlatform;
    private bool _slime1CrossedNarrowGap;
    private bool _slime2Fell;
    private bool _slime1Fell;
    private bool _playerFellIntoPit;
    private bool _playerRespawnedAfterFall;
    private bool _playerLandedNaturally;
    private readonly List<string> _failures = new();

    public override void _Ready()
    {
        var world = GD.Load<PackedScene>("res://Game/Scenes/World.tscn").Instantiate();
        AddChild(world);
        _player = GetNode<Character>("World/Player");
        _slime1 = GetNode<Character>("World/Rooms/TestLevel/Slime1");
        _slime2 = GetNode<Character>("World/Rooms/TestLevel/Slime2");
        _slime1.Motor.AttackStarted += () => _slime1AttackCount++;
        // 物理未运行，GlobalPosition 即场景出生点；用它做落坑判定基准
        // （不能用运行中的 y 采样：巡逻跳跃中的史莱姆会污染基准）
        _slimeSpawnY = _slime1.GlobalPosition.Y;
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        _playerMinHP = Mathf.Min(_playerMinHP, _player.Health.CurrentHP);

        if (_frame <= LandFrame)
        {
            // 出生后物理沉降应稳定落地（末帧单点采样会被击退滞空打闪，故锁存）
            _playerLandedNaturally |= _player.IsOnFloor();
            if (_frame == LandFrame)
            {
                _playerRestY = _player.GlobalPosition.Y;
            }
            return;
        }

        if (_frame <= ManeuverEndFrame)
        {
            if (_frame == RunPressFrame)
            {
                Input.ActionPress("move_right");
            }
            if (_frame == JumpPressFrame)
            {
                Input.ActionPress("jump");
            }
            if (_frame == JumpReleaseFrame)
            {
                Input.ActionRelease("jump");
            }
            if (_frame == RunReleaseFrame)
            {
                Input.ActionRelease("move_right");
            }
            if (_frame == PitRunPressFrame)
            {
                Input.ActionPress("move_right"); // 二段助跑：直奔宽坑
            }
            _playerMinY = Mathf.Min(_playerMinY, _player.GlobalPosition.Y);
            _playerWasOnPlatform |=
                _player.IsOnFloor()
                && _player.GlobalPosition.Y < PlatformTopY
                && _player.GlobalPosition.X > PlatformMinX
                && _player.GlobalPosition.X < PlatformMaxX;
            SampleFallDeath();
            return;
        }

        // 阶段三：观察史莱姆与战斗闭环（玩家落坑重生兜底采样）
        SampleFallDeath();
        float fellY = _slimeSpawnY + FellOffsetY;
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
        _slime1CrossedNarrowGap |= _slime1.GlobalPosition.X < NarrowGapX;

        if (_frame == TotalFrames)
        {
            Check(_playerLandedNaturally, "玩家自然落地");
            Check(
                _playerRestY - _playerMinY >= MinJumpRise,
                $"跳跃上升 {_playerRestY - _playerMinY:F1}px ≥ {MinJumpRise:F0}px"
            );
            Check(_playerWasOnPlatform, "玩家跳上 ×2 后平台");
            Check(_playerFellIntoPit && _playerRespawnedAfterFall, "玩家落坑即死并重生回出生点");
            Check(!_slime2Fell, "Slime2 在观察期内未落入宽沟");
            Check(!_slime1Fell, "Slime1 在观察期内未落坑");
            Check(_slime1CrossedNarrowGap, "Slime1 在观察期内穿过窄坑 (x<512)");
            Check(_slime1AttackCount > 0, "Slime1 追击中出招");
            Check(_playerMinHP < _player.Health.MaxHP, "史莱姆命中玩家（HP 曾下降）");
            Finish();
        }
    }

    /// <summary>
    /// 落坑死亡与重生采样：触达即死区高度即视为坠落（同时松开方向键，
    /// 防止重生后仍按住右键再次入坑）；此后回到出生点地面且满血即视为重生完成。
    /// </summary>
    private void SampleFallDeath()
    {
        if (!_playerFellIntoPit && _player.GlobalPosition.Y > PitFallY)
        {
            _playerFellIntoPit = true;
            Input.ActionRelease("move_right");
            GD.Print($"[trip] frame={_frame} 玩家触达即死区 y={_player.GlobalPosition.Y:F1}");
        }
        if (
            _playerFellIntoPit
            && !_playerRespawnedAfterFall
            && _player.IsOnFloor()
            && _player.GlobalPosition.Y < 700f
            && _player.GlobalPosition.X < 200f
            && _player.Health.CurrentHP == _player.Health.MaxHP
        )
        {
            _playerRespawnedAfterFall = true;
            GD.Print($"[trip] frame={_frame} 玩家已重生 x={_player.GlobalPosition.X:F1}");
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
        GD.Print($"PROBE SUMMARY {9 - _failures.Count}/9 通过");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}
