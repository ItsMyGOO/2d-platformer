using System;
using Godot;

namespace GodotGameTemplate.Gameplay.Characters.InputSources;

/// <summary>
/// 史莱姆 AI 输入：三层行为——巡逻（Patrol）、追击（Chase）、攻击（Attack）。
/// 发现玩家（ChaseDetector）后转为追击：朝玩家移动，遇宽沟停下、遇墙跳；
/// 水平距离进入攻击范围且冷却好时发攻击脉冲，原地出招。
/// 玩家脱离视野回到巡逻（撞墙/悬崖调头，窄沟跳过、宽沟调头）。
/// 只产出与玩家完全相同的意图，不触碰任何逻辑层细节。
/// </summary>
public partial class SlimeAIInputSource : InputSource
{
    /// <summary>墙体探测：从身体中心水平指向前方。</summary>
    [Export]
    private RayCast2D _wallRay;

    /// <summary>近端崖沿探测：从身前下方指向前下方，探不到地面说明身前是沟。</summary>
    [Export]
    private RayCast2D _ledgeNearRay;

    /// <summary>远端崖沿探测：探得更远，用来区分「跳得过的窄沟」与「必须停下的宽沟」。</summary>
    [Export]
    private RayCast2D _ledgeFarRay;

    /// <summary>玩家视野探测器（约定名 ChaseDetector，mask=玩家实体层）。</summary>
    [Export]
    private Area2D _chaseDetector;

    /// <summary>初始巡逻方向：1 右，-1 左。</summary>
    [Export]
    private int _initialDirection = -1;

    [Export]
    private float _flipCooldown = 0.25f;

    [Export]
    private float _jumpCooldown = 0.8f;

    /// <summary>
    /// 与玩家的水平距离小于该值时原地出攻击（像素）。
    /// 必须小于命中可达距离 48 = 命中盒偏移 20 + 命中盒半宽 14 + 玩家受击盒半宽 14，
    /// 否则会停在射程外空挥（阶段④前 26 > 24 即僵持根因）。
    /// </summary>
    [Export]
    private float _attackDistance = 40f;

    /// <summary>AI 侧攻击节奏（秒），应不小于角色攻击冷却。</summary>
    [Export]
    private float _attackInterval = 1.2f;

    private int _direction;
    private float _flipTimer;
    private float _jumpTimer;
    private float _attackTimer;
    private bool _jumpPulse;
    private bool _grounded;
    private Node2D _target;

    public override void _Ready()
    {
        _direction = _initialDirection >= 0 ? 1 : -1;
        // 节点引用按约定名在同级节点中发现（C# Node 导出无法解析前向 NodePath）。
        Node parent = GetParent();
        _wallRay ??= parent?.GetNodeOrNull<RayCast2D>("WallRay");
        _ledgeNearRay ??= parent?.GetNodeOrNull<RayCast2D>("LedgeNearRay");
        _ledgeFarRay ??= parent?.GetNodeOrNull<RayCast2D>("LedgeFarRay");
        _chaseDetector ??= parent?.GetNodeOrNull<Area2D>("ChaseDetector");
    }

    public override void NotifyGrounded(bool grounded) => _grounded = grounded;

    public override InputIntent Poll(float delta)
    {
        _flipTimer -= delta;
        _jumpTimer -= delta;
        _attackTimer -= delta;

        _target = FindTarget();
        InputIntent intent =
            _target != null && IsInstanceValid(_target) ? ChaseIntent(delta) : PatrolIntent(delta);
        _jumpPulse = false;
        return intent;
    }

    /// <summary>巡逻：撞墙/悬崖调头，窄沟跳过、宽沟调头。</summary>
    private InputIntent PatrolIntent(float delta)
    {
        if (_grounded && _flipTimer <= 0f)
        {
            AimRays();
            bool wallAhead = _wallRay != null && _wallRay.IsColliding();
            bool gapAhead = _ledgeNearRay != null && !_ledgeNearRay.IsColliding();
            if (wallAhead)
            {
                Turn();
            }
            else if (gapAhead)
            {
                bool gapTooWide = IsGapTooWide();
                if (gapTooWide)
                {
                    Turn();
                }
                else if (_jumpTimer <= 0f)
                {
                    _jumpPulse = true;
                    _jumpTimer = _jumpCooldown;
                }
            }
        }
        return InputIntent.Create(_direction, _jumpPulse, jumpHeld: true);
    }

    /// <summary>追击：朝玩家移动；宽沟前停下、遇墙跳；进入攻击距离原地出招。</summary>
    private InputIntent ChaseIntent(float delta)
    {
        float dx = _target.GlobalPosition.X - ((Node2D)GetParent()).GlobalPosition.X;
        int toward = MathF.Sign(dx);
        if (toward != 0)
        {
            _direction = toward;
        }
        bool inAttackRange = MathF.Abs(dx) <= _attackDistance;

        bool jumpPulse = false;
        if (_grounded && !inAttackRange)
        {
            AimRays();
            bool wallAhead = _wallRay != null && _wallRay.IsColliding();
            bool gapAhead = _ledgeNearRay != null && !_ledgeNearRay.IsColliding();
            if (wallAhead && _jumpTimer <= 0f)
            {
                jumpPulse = true;
                _jumpTimer = _jumpCooldown;
            }
            else if (gapAhead)
            {
                if (IsGapTooWide())
                {
                    return InputIntent.Create(0f, false, jumpHeld: true); // 宽沟：停下对峙
                }
                if (_jumpTimer <= 0f)
                {
                    jumpPulse = true;
                    _jumpTimer = _jumpCooldown;
                }
            }
        }

        bool attackPulse = false;
        if (inAttackRange && _attackTimer <= 0f)
        {
            attackPulse = true;
            _attackTimer = _attackInterval;
        }
        return InputIntent.Create(
            inAttackRange ? 0f : _direction,
            jumpPulse,
            jumpHeld: true,
            attackPressed: attackPulse
        );
    }

    private Node2D FindTarget()
    {
        if (_chaseDetector == null)
        {
            return null;
        }
        foreach (Node2D body in _chaseDetector.GetOverlappingBodies())
        {
            if (body is Character && IsInstanceValid(body))
            {
                return body; // 探测器 mask 只含玩家实体层
            }
        }
        return null;
    }

    private void Turn()
    {
        _direction = -_direction;
        _flipTimer = _flipCooldown;
    }

    /// <summary>
    /// 判断身前的沟是否宽过跳跃能力。远端线必须把起点挪过崖沿再探——
    /// 否则线段起点还在当前一侧的地面上，会先命中近侧地面而永远误判「跳得过」。
    /// </summary>
    private bool IsGapTooWide()
    {
        if (_ledgeFarRay == null)
        {
            return true;
        }
        _ledgeFarRay.Position = new Vector2(16f * _direction, 12f);
        _ledgeFarRay.TargetPosition = new Vector2(52f * _direction, 20f);
        _ledgeFarRay.ForceRaycastUpdate();
        return !_ledgeFarRay.IsColliding();
    }

    /// <summary>按当前方向摆好三根探测线，并强制同帧刷新命中结果。</summary>
    private void AimRays()
    {
        if (_wallRay != null)
        {
            _wallRay.Position = new Vector2(0f, -2f);
            _wallRay.TargetPosition = new Vector2(28f * _direction, 0f);
            _wallRay.ForceRaycastUpdate();
        }
        if (_ledgeNearRay != null)
        {
            _ledgeNearRay.Position = new Vector2(12f * _direction, 12f);
            _ledgeNearRay.TargetPosition = new Vector2(16f * _direction, 20f);
            _ledgeNearRay.ForceRaycastUpdate();
        }
        if (_ledgeFarRay != null)
        {
            _ledgeFarRay.Position = new Vector2(4f * _direction, 12f);
            _ledgeFarRay.TargetPosition = new Vector2(60f * _direction, 20f);
            _ledgeFarRay.ForceRaycastUpdate();
        }
    }
}
