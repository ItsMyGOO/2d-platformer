using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.InputSources;

/// <summary>
/// 射手 AI 输入：站桩远程敌人——玩家进入视野（ChaseDetector）即朝玩家方向
/// 周期性发出攻击脉冲（由 ProjectileEmitter 转化为弹体）；脱离视野待机。
/// 面向由目标方位实时决定；无移动意图。
/// </summary>
public partial class ShooterAIInputSource : InputSource
{
    /// <summary>玩家视野探测器（约定名 ChaseDetector，mask=玩家实体层）。</summary>
    [Export]
    private Area2D _chaseDetector;

    /// <summary>攻击间隔（秒）；与攻击动画/冷却解耦的 AI 节奏。</summary>
    [Export]
    private float _attackInterval = 1.6f;

    private float _attackTimer;
    private Character _target;

    public override void _Ready()
    {
        // 节点引用按约定名在同级节点中发现（C# Node 导出无法解析前向 NodePath）。
        _chaseDetector ??= GetParent()?.GetNodeOrNull<Area2D>("ChaseDetector");
    }

    public override InputIntent Poll(float delta)
    {
        _attackTimer -= delta;
        _target = FindTarget();

        if (_target == null || !IsInstanceValid(_target))
        {
            return InputIntent.Create(0f, false, jumpHeld: false);
        }

        // 朝向经 MoveAxis 表达（Motor 按 MoveAxis 更新 Facing）；
        // 极小轴值让地面摩擦几乎不产生位移，避免站桩敌人滑步。
        int toward = _target.GlobalPosition.X > ((Node2D)GetParent()).GlobalPosition.X ? 1 : -1;
        bool wantAttack =
            _attackTimer <= 0f && _target is { Health.IsDead: false, Health.IsInvincible: false };
        if (wantAttack)
        {
            _attackTimer = _attackInterval;
        }
        return InputIntent.Create(
            toward * 0.0001f,
            false,
            jumpHeld: false,
            attackPressed: wantAttack
        );
    }

    private Character FindTarget()
    {
        if (_chaseDetector == null)
        {
            return null;
        }
        foreach (Node2D body in _chaseDetector.GetOverlappingBodies())
        {
            if (body is Character character && IsInstanceValid(character))
            {
                return character;
            }
        }
        return null;
    }
}
